using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class PreventiveActionEndpoints
{
    public static void MapPreventiveActionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/actions").RequireAuthorization();
        group.MapPut("", UpsertAsync);
        group.MapGet("{day}", GetByDayAsync);
    }

    private static async Task<IResult> UpsertAsync(
        [FromBody] UpsertActionsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var targets = await db.PatientTargets.FirstOrDefaultAsync(t => t.PatientId == patient.Id, cancellationToken);
        var saltTargetAllowed = targets?.SaltTargetEnabled ?? false;

        UrineColor? urineColor = null;
        if (!string.IsNullOrWhiteSpace(dto.UrineColor))
        {
            if (!Enum.TryParse<UrineColor>(dto.UrineColor, ignoreCase: false, out var u) || !Enum.IsDefined(u))
                return Results.BadRequest(new { code = "actions.urine_color_invalid" });
            urineColor = u;
        }

        if (!Enum.TryParse<CaffeineLevel>(dto.CaffeineLevel, ignoreCase: false, out var caffeine) ||
            !Enum.IsDefined(caffeine))
            return Results.BadRequest(new { code = "actions.caffeine_invalid" });

        var data = new PreventiveActionData
        {
            FluidMl = dto.FluidMl,
            ElectrolyteTaken = dto.ElectrolyteTaken,
            MorningWaterBeforeStanding = dto.MorningWaterBeforeStanding,
            UrineColor = urineColor,
            SaltTargetReached = dto.SaltTargetReached,
            RegularMeals = dto.RegularMeals,
            SkippedBreakfast = dto.SkippedBreakfast,
            SmallFrequentMeals = dto.SmallFrequentMeals,
            AvoidedLargeHighCarbMeal = dto.AvoidedLargeHighCarbMeal,
            AdequateProtein = dto.AdequateProtein,
            AlcoholAvoided = dto.AlcoholAvoided,
            CaffeineLevel = caffeine,
            CompressionSocks = dto.CompressionSocks,
            WaistHighCompression = dto.WaistHighCompression,
            AbdominalCompression = dto.AbdominalCompression,
            CompressionHoursWorn = dto.CompressionHoursWorn,
            RecumbentExercise = dto.RecumbentExercise,
            Walking = dto.Walking,
            Strength = dto.Strength,
            Stretching = dto.Stretching,
            PtExercises = dto.PtExercises,
            ExerciseDurationMinutes = dto.ExerciseDurationMinutes,
            ExerciseIntensity = dto.ExerciseIntensity,
            PostExerciseSymptoms = dto.PostExerciseSymptoms,
            PlannedRestBreaks = dto.PlannedRestBreaks,
            AvoidedOverexertion = dto.AvoidedOverexertion,
            UsedActivityPacing = dto.UsedActivityPacing,
            AvoidedLongStanding = dto.AvoidedLongStanding,
            SatDuringShowerCooking = dto.SatDuringShowerCooking,
            MobilityAid = dto.MobilityAid,
            AvoidedHeat = dto.AvoidedHeat,
            UsedCoolingVestFan = dto.UsedCoolingVestFan,
            ColdShower = dto.ColdShower,
            AvoidedHotBathSauna = dto.AvoidedHotBathSauna,
            StayedInShadeAc = dto.StayedInShadeAc,
            SleptEnough = dto.SleptEnough,
            SleepQuality = dto.SleepQuality,
            ConsistentBedtimes = dto.ConsistentBedtimes,
            NapTaken = dto.NapTaken,
            WokeRefreshed = dto.WokeRefreshed,
            MedicationTakenAsPrescribed = dto.MedicationTakenAsPrescribed,
            MissedDose = dto.MissedDose,
            SideEffects = dto.SideEffects,
            NewMedicationOrSupplement = dto.NewMedicationOrSupplement,
            RescueMedicationUsed = dto.RescueMedicationUsed,
        };

        var existing = await db.PreventiveActionLogs.FirstOrDefaultAsync(
            e => e.PatientId == patient.Id && e.Day == dto.Day, cancellationToken);

        try
        {
            if (existing is null)
            {
                var entry = PreventiveActionLog.Create(patient.Id, userId, dto.Day, saltTargetAllowed, data);
                db.PreventiveActionLogs.Add(entry);
                existing = entry;
            }
            else
            {
                existing.Update(saltTargetAllowed, data);
            }
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "actions.invalid", message = ex.Message });
        }

        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "actions.upserted", nameof(PreventiveActionLog), existing.Id, patient.Id,
            $"{{\"day\":\"{dto.Day:yyyy-MM-dd}\"}}"));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ActionLogDto(existing.Id, existing.Day));
    }

    private static async Task<IResult> GetByDayAsync(
        string day,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(day, out var d))
            return Results.BadRequest(new { code = "actions.day_invalid" });

        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var existing = await db.PreventiveActionLogs.FirstOrDefaultAsync(
            e => e.PatientId == patient.Id && e.Day == d, cancellationToken);
        if (existing is null) return Results.NotFound();
        return Results.Ok(new ActionLogDto(existing.Id, existing.Day));
    }
}
