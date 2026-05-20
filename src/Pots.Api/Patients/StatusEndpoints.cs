using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/status").RequireAuthorization();
        group.MapPost("", RecordAsync);
        group.MapGet("/today", GetTodayAsync);
        group.MapPatch("/{statusId:guid}", UpdateDetailAsync);
        group.MapDelete("/{statusId:guid}", DeleteAsync);
    }

    // Owner-only delete for misclick recovery. CLAUDE.md §6: patient owns
    // their data; they may delete. The deletion is audited (append-only)
    // so a future review can see the entry existed and was removed.
    private static async Task<IResult> DeleteAsync(
        Guid statusId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var entry = await db.DailyStatusEntries.FirstOrDefaultAsync(e => e.Id == statusId, cancellationToken);
        if (entry is null) return Results.NotFound();
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == entry.PatientId, cancellationToken);
        if (patient is null || patient.OwnerUserId != userId)
            return Results.Forbid();

        db.DailyStatusEntries.Remove(entry);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "status.deleted", nameof(DailyStatusEntry), entry.Id, patient.Id,
            $"{{\"status\":\"{entry.Status}\"}}"));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecordAsync(
        [FromBody] RecordStatusDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        if (!Enum.TryParse<DailyStatusKind>(dto.Status, out var status) || !Enum.IsDefined(status))
            return Results.BadRequest(new { code = "status.invalid_status" });

        PostureKind? posture = null;
        if (!string.IsNullOrEmpty(dto.Posture))
        {
            if (!Enum.TryParse<PostureKind>(dto.Posture, out var p) || !Enum.IsDefined(p))
                return Results.BadRequest(new { code = "status.invalid_posture" });
            posture = p;
        }

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        DailyStatusEntry entry;
        try
        {
            entry = DailyStatusEntry.Create(
                patient.Id, userId, status, posture, dto.Activity, dto.LocationNote, dto.Note, dto.EpisodeOccurred);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "status.invalid", message = ex.Message });
        }

        db.DailyStatusEntries.Add(entry);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId,
            $"status.{status.ToString().ToLowerInvariant()}",
            nameof(DailyStatusEntry),
            entry.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/me/status/{entry.Id}", MapDto(entry));
    }

    private static async Task<IResult> GetTodayAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        // 24h sliding window in UTC. Client converts to local TZ for display.
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var rows = await db.DailyStatusEntries
            .Where(e => e.PatientId == patient.Id && e.CreatedAt >= since)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Phase 6 attribution: surface RecorderName for entries logged by an
        // Editor on the patient's behalf. Owner self-records leave RecorderName
        // null. The lookup uses the SECURITY DEFINER fn so cross-user names
        // (which RLS on users would hide) become visible to the legitimate
        // owner without weakening users_self_select.
        var nameMap = await BuildRecorderNameMapAsync(db, patient.Id, rows, patient.OwnerUserId, cancellationToken);
        return Results.Ok(rows.Select(e => MapDtoWithRecorder(e, patient.OwnerUserId, nameMap)).ToList());
    }

    private sealed record UserEmailRow(Guid UserId, string Email, string? DisplayName);

    internal static async Task<Dictionary<Guid, string>> BuildRecorderNameMapAsync(
        PotsDbContext db,
        Guid patientId,
        IEnumerable<DailyStatusEntry> entries,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        // Only resolve names for entries NOT recorded by the owner. The owner's
        // own entries don't need attribution rendered.
        var nonOwnerRecorders = entries
            .Where(e => e.RecordedByUserId != ownerUserId)
            .Select(e => e.RecordedByUserId)
            .Distinct()
            .ToList();
        if (nonOwnerRecorders.Count == 0) return new();

        var rows = await db.Database
            .SqlQuery<UserEmailRow>($"SELECT * FROM list_patient_user_emails({patientId})")
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => nonOwnerRecorders.Contains(r.UserId))
            .ToDictionary(
                r => r.UserId,
                r => string.IsNullOrWhiteSpace(r.DisplayName) ? r.Email : r.DisplayName!);
    }

    internal static DailyStatusDto MapDtoWithRecorder(
        DailyStatusEntry e,
        Guid ownerUserId,
        IReadOnlyDictionary<Guid, string> nameMap)
    {
        string? recorderName = null;
        if (e.RecordedByUserId != ownerUserId && nameMap.TryGetValue(e.RecordedByUserId, out var name))
            recorderName = name;
        return new DailyStatusDto(
            e.Id, e.Status.ToString(), e.Posture?.ToString(),
            e.Activity, e.LocationNote, e.Note,
            e.EpisodeOccurred, e.CreatedAt, recorderName);
    }

    private static async Task<IResult> UpdateDetailAsync(
        Guid statusId,
        [FromBody] UpdateStatusDetailDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        PostureKind? posture = null;
        if (!string.IsNullOrEmpty(dto.Posture))
        {
            if (!Enum.TryParse<PostureKind>(dto.Posture, out var p) || !Enum.IsDefined(p))
                return Results.BadRequest(new { code = "status.invalid_posture" });
            posture = p;
        }

        // RLS guarantees we only see entries on patients we have access to;
        // we additionally check this is the user's OWN patient because
        // "I felt X at time Y" is the patient's own statement and shouldn't
        // be rewritten by editor grantees.
        var entry = await db.DailyStatusEntries.FirstOrDefaultAsync(e => e.Id == statusId, cancellationToken);
        if (entry is null) return Results.NotFound();
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == entry.PatientId, cancellationToken);
        if (patient is null || patient.OwnerUserId != userId)
            return Results.Forbid();

        try
        {
            entry.UpdateDetail(posture, dto.Activity, dto.LocationNote, dto.Note, dto.EpisodeOccurred);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "status.invalid", message = ex.Message });
        }

        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "status.detail_added", nameof(DailyStatusEntry), entry.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(MapDto(entry));
    }

    private static DailyStatusDto MapDto(DailyStatusEntry e) => new(
        e.Id,
        e.Status.ToString(),
        e.Posture?.ToString(),
        e.Activity,
        e.LocationNote,
        e.Note,
        e.EpisodeOccurred,
        e.CreatedAt);
}
