using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class EpisodeEndpoints
{
    public static void MapEpisodeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/episodes").RequireAuthorization();
        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateEpisodeDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        PostureKind? posture = null;
        if (!string.IsNullOrWhiteSpace(dto.PostureBefore))
        {
            if (!Enum.TryParse<PostureKind>(dto.PostureBefore, ignoreCase: false, out var p) || !Enum.IsDefined(p))
                return Results.BadRequest(new { code = "episode.posture_invalid" });
            posture = p;
        }

        if (!Enum.TryParse<EpisodeTrigger>(dto.TriggerSuspected, ignoreCase: false, out var trigger) || !Enum.IsDefined(trigger))
            return Results.BadRequest(new { code = "episode.trigger_invalid" });

        Episode episode;
        try
        {
            episode = Episode.Create(patient.Id, new EpisodeData
            {
                StartTime = dto.StartTime,
                DurationMinutes = dto.DurationMinutes,
                MainSymptom = dto.MainSymptom,
                PostureBefore = posture,
                TriggerSuspected = trigger,
                HrDuringBpm = dto.HrDuringBpm,
                BpDuringSystolic = dto.BpDuringSystolic,
                BpDuringDiastolic = dto.BpDuringDiastolic,
                ActionTaken = dto.ActionTaken,
                RecoveryTimeMinutes = dto.RecoveryTimeMinutes,
                PreventedFainting = dto.PreventedFainting,
                Note = dto.Note,
            });
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "episode.invalid", message = ex.Message });
        }

        db.Episodes.Add(episode);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "episode.created", nameof(Episode), episode.Id, patient.Id,
            $"{{\"trigger\":\"{trigger}\"}}"));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/me/episodes/{episode.Id}", ToDto(episode));
    }

    private static async Task<IResult> ListAsync(
        PotsDbContext db,
        CancellationToken cancellationToken,
        [FromQuery] int take = 50)
    {
        if (take < 1) take = 1;
        if (take > 500) take = 500;
        var rows = await db.Episodes
            .OrderByDescending(e => e.StartTime)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(ToDto).ToList());
    }

    private static EpisodeDto ToDto(Episode e) => new(
        e.Id, e.StartTime, e.DurationMinutes, e.MainSymptom,
        e.PostureBefore?.ToString(), e.TriggerSuspected.ToString(),
        e.HrDuringBpm, e.BpDuringSystolic, e.BpDuringDiastolic,
        e.ActionTaken, e.RecoveryTimeMinutes, e.PreventedFainting,
        e.Note, e.CreatedAt);
}
