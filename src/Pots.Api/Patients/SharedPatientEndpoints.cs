using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

// Cross-patient READ endpoints for the caregiver experience.
//
// Why a separate file: the `/me/...` endpoints assume the caller is the
// owner of the patient. Caregivers (active grantees) need to read another
// patient's data without going through `/me/...` — that endpoint group
// resolves the patient by `OwnerUserId == userId`, which would return 404
// for grantees.
//
// Access control: RLS on PotsDbContext already filters every query to rows
// the calling user can see. We do not duplicate the access check at the
// application layer — the `FirstOrDefault` returning `null` IS the access
// check. If RLS hides the patient row, the caregiver gets 404,
// indistinguishable from "patient doesn't exist" (anti-enumeration).
//
// Audit: every cross-user read appends a `patient.read_by_grantee` entry
// to the audit log. Required for GDPR Subject Access Request scenarios
// ("who accessed my data?") and for the patient's ability to revoke access
// with confidence. The audit_self_insert RLS policy already allows this
// because the caregiver has has_patient_access on the patient.
//
// Phase 2 scope: only the three endpoints required to render `/shared/{id}`
// and `/shared/{id}/episodes`. Trends and Report mirrors are Phase 2.5.
public static class SharedPatientEndpoints
{
    public static void MapSharedPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/patients/{patientId:guid}").RequireAuthorization();
        group.MapGet("", GetPatientAsync);
        group.MapGet("/status/today", GetTodayStatusAsync);
        group.MapGet("/episodes", GetEpisodesAsync);
        // Phase 5: Editor (or owner) records status on behalf of the
        // patient. RLS gates write access via has_patient_edit_access.
        group.MapPost("/status", RecordStatusAsync);
    }

    private static async Task<IResult> RecordStatusAsync(
        Guid patientId,
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

        // Confirm patient is accessible (RLS would 404 if not). We need the
        // entity to detect "is owner vs editor" so the audit reflects
        // who-acted-on-whose-behalf.
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

        DailyStatusEntry entry;
        try
        {
            entry = DailyStatusEntry.Create(
                patient.Id, userId, status, posture,
                dto.Activity, dto.LocationNote, dto.Note, dto.EpisodeOccurred);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "status.invalid", message = ex.Message });
        }

        db.DailyStatusEntries.Add(entry);

        // Keep the action tag stable across owner-self and editor-on-behalf
        // recordings — `status.{kind}` — so the Doctor Report can filter by
        // colour without re-joining `daily_status_entries`. The "who
        // actually pressed it" lives in the details JSON when caller ≠
        // owner; combined with `entry.RecordedByUserId` on the row itself,
        // this preserves both the colour and the editor attribution.
        var actionTag = $"status.{status.ToString().ToLowerInvariant()}";
        var details = patient.OwnerUserId != userId
            ? "{\"recorded_by_editor\":true}"
            : null;
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, actionTag, nameof(DailyStatusEntry),
            entry.Id, patient.Id, details));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/patients/{patientId}/status/{entry.Id}", new DailyStatusDto(
            entry.Id, entry.Status.ToString(), entry.Posture?.ToString(),
            entry.Activity, entry.LocationNote, entry.Note,
            entry.EpisodeOccurred, entry.CreatedAt));
    }

    private static async Task<IResult> GetPatientAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // RLS gates this: returns null if the user is neither the owner nor
        // an active grantee. Caregivers and owners reach this point — we
        // don't distinguish in the response (anti-enumeration).
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

        // Resolve the caller's role so the client can render the right
        // chrome (request-editor button for Viewer, write controls for
        // Editor in Phase 5, none for Owner).
        string callerRole;
        Guid? pendingRequestId = null;
        if (patient.OwnerUserId == userId)
        {
            callerRole = "Owner";
        }
        else
        {
            var grant = await db.PatientGrants
                .FirstOrDefaultAsync(g =>
                    g.PatientId == patient.Id
                    && g.GranteeUserId == userId
                    && g.RevokedAt == null, cancellationToken);
            // RLS already filtered: if we reached this branch a grant exists.
            // The null-check is defense-in-depth for a race with revocation.
            if (grant is null) return Results.NotFound();
            callerRole = grant.Role.ToString();

            // Surface any in-flight upgrade request so the UI can render
            // "request pending" instead of the "request access" button.
            if (grant.Role == GrantRole.Viewer)
            {
                pendingRequestId = await db.GrantUpgradeRequests
                    .Where(r =>
                        r.GrantId == grant.Id
                        && r.Status == GrantUpgradeRequestStatus.Pending)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Only audit cross-user reads. Owner self-reads would flood the
            // audit log. The audit_self_insert policy permits this insert
            // because the grantee has has_patient_access on the patient.
            db.AuditLog.Add(AuditLogEntry.Record(
                userId, "patient.read_by_grantee", nameof(Patient), patient.Id, patient.Id, null));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new SharedPatientContextDto(
            patient.Id, patient.Name, callerRole, pendingRequestId));
    }

    private static async Task<IResult> GetTodayStatusAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // No pre-existence check: RLS makes the WHERE clause itself the
        // access gate. Empty list means "no access OR no entries"; either
        // way 200-with-[] is a correct response. The /shared/{id} page
        // disambiguates via the GetPatientAsync call it issues first.
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var rows = await db.DailyStatusEntries
            .Where(e => e.PatientId == patientId && e.CreatedAt >= since)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Audit only when there's something to audit — i.e. when RLS
        // actually returned data for a patient that is NOT the owner's own.
        // We check ownership via a separate query rather than embed in the
        // first query to avoid a JOIN in the hot path.
        if (rows.Count > 0)
        {
            var isOwner = await db.Patients
                .AnyAsync(p => p.Id == patientId && p.OwnerUserId == userId, cancellationToken);
            if (!isOwner)
            {
                db.AuditLog.Add(AuditLogEntry.Record(
                    userId, "patient.read_by_grantee", nameof(DailyStatusEntry), patientId, patientId,
                    $"{{\"endpoint\":\"status/today\",\"count\":{rows.Count}}}"));
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var dtos = rows.Select(e => new DailyStatusDto(
            e.Id,
            e.Status.ToString(),
            e.Posture?.ToString(),
            e.Activity,
            e.LocationNote,
            e.Note,
            e.EpisodeOccurred,
            e.CreatedAt)).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetEpisodesAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int take = 50)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        if (take < 1) take = 1;
        if (take > 500) take = 500;

        var rows = await db.Episodes
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.StartTime)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            var isOwner = await db.Patients
                .AnyAsync(p => p.Id == patientId && p.OwnerUserId == userId, cancellationToken);
            if (!isOwner)
            {
                db.AuditLog.Add(AuditLogEntry.Record(
                    userId, "patient.read_by_grantee", nameof(Episode), patientId, patientId,
                    $"{{\"endpoint\":\"episodes\",\"count\":{rows.Count}}}"));
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var dtos = rows.Select(e => new EpisodeDto(
            e.Id, e.StartTime, e.DurationMinutes, e.MainSymptom,
            e.PostureBefore?.ToString(), e.TriggerSuspected.ToString(),
            e.HrDuringBpm, e.BpDuringSystolic, e.BpDuringDiastolic,
            e.ActionTaken, e.RecoveryTimeMinutes, e.PreventedFainting,
            e.Note, e.CreatedAt)).ToList();

        return Results.Ok(dtos);
    }
}
