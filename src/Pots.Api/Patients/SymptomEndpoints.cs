using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class SymptomEndpoints
{
    public static void MapSymptomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/symptoms").RequireAuthorization();
        group.MapPost("", RecordAsync);
    }

    private static async Task<IResult> RecordAsync(
        [FromBody] RecordSymptomsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        GiBowelState? bowel = null;
        if (!string.IsNullOrWhiteSpace(dto.Bowel))
        {
            if (!Enum.TryParse<GiBowelState>(dto.Bowel, ignoreCase: false, out var b) || !Enum.IsDefined(b))
                return Results.BadRequest(new { code = "symptoms.bowel_invalid" });
            bowel = b;
        }

        SymptomLog log;
        try
        {
            log = SymptomLog.Create(patient.Id, new SymptomData
            {
                RecordedAt = dto.RecordedAt,
                Dizziness = dto.Dizziness,
                Palpitations = dto.Palpitations,
                TachycardiaSensation = dto.TachycardiaSensation,
                ChestDiscomfort = dto.ChestDiscomfort,
                ShortnessOfBreath = dto.ShortnessOfBreath,
                NearFainting = dto.NearFainting,
                FaintingEpisode = dto.FaintingEpisode,
                BloodPooling = dto.BloodPooling,
                BrainFog = dto.BrainFog,
                Headache = dto.Headache,
                VisualDisturbance = dto.VisualDisturbance,
                Tremor = dto.Tremor,
                Weakness = dto.Weakness,
                Fatigue = dto.Fatigue,
                Sleepiness = dto.Sleepiness,
                Nausea = dto.Nausea,
                AbdominalPain = dto.AbdominalPain,
                Bloating = dto.Bloating,
                Bowel = bowel,
                AppetiteLevel = dto.AppetiteLevel,
                HeatIntolerance = dto.HeatIntolerance,
                Sweating = dto.Sweating,
                Chills = dto.Chills,
                Flushing = dto.Flushing,
                ColdExtremities = dto.ColdExtremities,
                Anxiety = dto.Anxiety,
                Mood = dto.Mood,
                AbilityToWork = dto.AbilityToWork,
                AbilityToWalk = dto.AbilityToWalk,
                SocialTolerance = dto.SocialTolerance,
            });
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "symptoms.invalid", message = ex.Message });
        }

        db.SymptomLogs.Add(log);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "symptoms.recorded", nameof(SymptomLog), log.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/me/symptoms/{log.Id}", new SymptomLogDto(log.Id, log.RecordedAt));
    }
}
