using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class VitalEndpoints
{
    public static void MapVitalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/vitals").RequireAuthorization();
        group.MapPost("", RecordAsync);
    }

    private static async Task<IResult> RecordAsync(
        [FromBody] RecordVitalsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        VitalSignLog log;
        try
        {
            log = VitalSignLog.Create(patient.Id, new VitalSignData
            {
                RecordedAt = dto.RecordedAt,
                RestingHrBpm = dto.RestingHrBpm,
                StandingHrBpm2Min = dto.StandingHrBpm2Min,
                StandingHrBpm5Min = dto.StandingHrBpm5Min,
                StandingHrBpm10Min = dto.StandingHrBpm10Min,
                BpLyingSystolic = dto.BpLyingSystolic,
                BpLyingDiastolic = dto.BpLyingDiastolic,
                BpSittingSystolic = dto.BpSittingSystolic,
                BpSittingDiastolic = dto.BpSittingDiastolic,
                BpStandingSystolic = dto.BpStandingSystolic,
                BpStandingDiastolic = dto.BpStandingDiastolic,
                Spo2Percent = dto.Spo2Percent,
                WeightKg = dto.WeightKg,
                MenstrualCycleDay = dto.MenstrualCycleDay,
                SleepDurationMinutes = dto.SleepDurationMinutes,
                SleepQuality = dto.SleepQuality,
                Steps = dto.Steps,
                ExerciseMinutes = dto.ExerciseMinutes,
                TimeUprightMinutes = dto.TimeUprightMinutes,
                TimeLyingMinutes = dto.TimeLyingMinutes,
                AmbientTempC = dto.AmbientTempC,
            });
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "vitals.invalid", message = ex.Message });
        }

        db.VitalSignLogs.Add(log);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "vitals.recorded", nameof(VitalSignLog), log.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/me/vitals/{log.Id}", new VitalLogDto(log.Id, log.RecordedAt));
    }
}
