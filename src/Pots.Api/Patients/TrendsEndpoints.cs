using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class TrendsEndpoints
{
    public static void MapTrendsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/trends").RequireAuthorization();
        group.MapGet("", GetAsync);
    }

    private static async Task<IResult> GetAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int days = 30)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });
        return Results.Ok(await ComputeAsync(db, patient.Id, days, cancellationToken));
    }

    // Internal so SharedPatientEndpoints can call this for caregiver-side
    // cross-patient trends (Phase 7.2.c).
    internal static async Task<TrendsDto> ComputeAsync(
        PotsDbContext db, Guid patientId, int days, CancellationToken cancellationToken)
    {
        if (days < 1) days = 1;
        if (days > 365) days = 365;
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var sinceDay = DateOnly.FromDateTime(since.UtcDateTime);

        // Phase 7.2.c: explicit patient.Id filter so a caregiver with
        // access to multiple patients gets trends for ONE patient at a time.
        var statuses = await db.DailyStatusEntries
            .Where(e => e.PatientId == patientId && e.CreatedAt >= since)
            .Select(e => new { e.Status, e.CreatedAt, e.EpisodeOccurred })
            .ToListAsync(cancellationToken);

        var episodes = await db.Episodes
            .Where(e => e.PatientId == patientId && e.StartTime >= since)
            .Select(e => new { e.StartTime, e.TriggerSuspected })
            .ToListAsync(cancellationToken);

        var symptoms = await db.SymptomLogs
            .Where(e => e.PatientId == patientId && e.RecordedAt >= since)
            .Select(e => new
            {
                e.RecordedAt,
                e.Fatigue,
                e.BrainFog,
                e.Dizziness
            })
            .ToListAsync(cancellationToken);

        var vitals = await db.VitalSignLogs
            .Where(e => e.PatientId == patientId && e.RecordedAt >= since)
            .Select(e => new
            {
                e.RecordedAt,
                e.SleepQuality,
                e.AmbientTempC,
                e.MenstrualCycleDay
            })
            .ToListAsync(cancellationToken);

        var actions = await db.PreventiveActionLogs
            .Where(e => e.PatientId == patientId && e.Day >= sinceDay)
            .ToListAsync(cancellationToken);

        var greenCount  = statuses.Count(s => s.Status == DailyStatusKind.Green);
        var orangeCount = statuses.Count(s => s.Status == DailyStatusKind.Orange);
        var redCount    = statuses.Count(s => s.Status == DailyStatusKind.Red);

        var perWeek = episodes
            .GroupBy(e => StartOfIsoWeek(e.StartTime.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new EpisodesPerWeekRow(g.Key.ToString("yyyy-MM-dd"), g.Count()))
            .ToList();

        var topTriggers = episodes
            .GroupBy(e => e.TriggerSuspected.ToString())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TriggerCountRow(g.Key, g.Count()))
            .ToList();

        var avgFatigue   = Avg(symptoms.Where(s => s.Fatigue.HasValue).Select(s => (double)s.Fatigue!.Value));
        var avgBrainFog  = Avg(symptoms.Where(s => s.BrainFog.HasValue).Select(s => (double)s.BrainFog!.Value));
        var avgDizziness = Avg(symptoms.Where(s => s.Dizziness.HasValue).Select(s => (double)s.Dizziness!.Value));

        var buckets = statuses
            .GroupBy(s => DateOnly.FromDateTime(s.CreatedAt.LocalDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyStatusBucket(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(s => s.Status == DailyStatusKind.Green),
                g.Count(s => s.Status == DailyStatusKind.Orange),
                g.Count(s => s.Status == DailyStatusKind.Red)))
            .ToList();

        // ------------------------------------------------------------------
        // Correlations. All averages are over the SymptomLog rows on matching
        // days; matching is by LOCAL date (DateOnly of CreatedAt/RecordedAt).
        // Language strictly uses "asociado con" (UI side) — never "causa".
        // ------------------------------------------------------------------
        var symptomsByDay = symptoms
            .GroupBy(s => DateOnly.FromDateTime(s.RecordedAt.LocalDateTime))
            .ToDictionary(
                g => g.Key,
                g => AvgBurden(g.Select(s => (s.Fatigue, s.BrainFog, s.Dizziness))));

        double? BurdenForDay(DateOnly day) => symptomsByDay.TryGetValue(day, out var v) ? v : null;

        // Best vs worst days by net (Green - Red) score.
        BestWorstDays? bestWorst = null;
        if (buckets.Count > 0)
        {
            var ranked = buckets
                .OrderByDescending(b => b.Green - b.Red)
                .ToList();
            var best = ranked.First();
            var worst = ranked.Last();
            bestWorst = new BestWorstDays(
                BestDay: best.Day, BestGreen: best.Green, BestRed: best.Red,
                WorstDay: worst.Day, WorstRed: worst.Red, WorstGreen: worst.Green);
        }

        // Sleep quality vs symptoms — bucketed.
        var sleepRows = vitals
            .Where(v => v.SleepQuality.HasValue)
            .Select(v => (Quality: v.SleepQuality!.Value, Burden: BurdenForDay(DateOnly.FromDateTime(v.RecordedAt.LocalDateTime))))
            .Where(x => x.Burden.HasValue)
            .ToList();
        var sleepVsSymptoms = BucketAvg(sleepRows.Select(x => (BucketLabel: SleepBucket(x.Quality), Value: x.Burden!.Value)));

        // Hydration vs symptoms — bucketed by ml/day from action logs.
        var hydrationRows = actions
            .Where(a => a.FluidMl.HasValue)
            .Select(a => (Bucket: HydrationBucket(a.FluidMl!.Value), Burden: BurdenForDay(a.Day)))
            .Where(x => x.Burden.HasValue)
            .ToList();
        var hydrationVsSymptoms = BucketAvg(hydrationRows.Select(x => (x.Bucket, Value: x.Burden!.Value)));

        // Compression vs no-compression — single binary split per day.
        var compressionPairs = actions
            .Select(a => (Used: a.CompressionSocks || a.WaistHighCompression || a.AbdominalCompression, Burden: BurdenForDay(a.Day)))
            .Where(x => x.Burden.HasValue)
            .ToList();
        AssociationRow? compressionVs = AvgFor(compressionPairs.Where(x => x.Used).Select(x => x.Burden!.Value), "Con compresión");
        AssociationRow? noCompressionVs = AvgFor(compressionPairs.Where(x => !x.Used).Select(x => x.Burden!.Value), "Sin compresión");

        // Skip breakfast vs regular breakfast.
        var breakfastPairs = actions
            .Select(a => (Skipped: a.SkippedBreakfast, Burden: BurdenForDay(a.Day)))
            .Where(x => x.Burden.HasValue)
            .ToList();
        AssociationRow? skipBreakfast = AvgFor(breakfastPairs.Where(x => x.Skipped).Select(x => x.Burden!.Value), "Saltarse desayuno");
        AssociationRow? regularBreakfast = AvgFor(breakfastPairs.Where(x => !x.Skipped).Select(x => x.Burden!.Value), "Desayuno normal");

        // Ambient temperature vs symptoms.
        var tempRows = vitals
            .Where(v => v.AmbientTempC.HasValue)
            .Select(v => (Bucket: TempBucket(v.AmbientTempC!.Value), Burden: BurdenForDay(DateOnly.FromDateTime(v.RecordedAt.LocalDateTime))))
            .Where(x => x.Burden.HasValue)
            .ToList();
        var tempVsSymptoms = BucketAvg(tempRows.Select(x => (x.Bucket, Value: x.Burden!.Value)));

        // Menstrual phase vs symptoms.
        var phaseRows = vitals
            .Where(v => v.MenstrualCycleDay.HasValue && v.MenstrualCycleDay >= 1 && v.MenstrualCycleDay <= 35)
            .Select(v => (Bucket: PhaseBucket(v.MenstrualCycleDay!.Value), Burden: BurdenForDay(DateOnly.FromDateTime(v.RecordedAt.LocalDateTime))))
            .Where(x => x.Burden.HasValue)
            .ToList();
        var phaseVsSymptoms = BucketAvg(phaseRows.Select(x => (x.Bucket, Value: x.Burden!.Value)));

        // Exercise tolerance.
        var exDays = actions.Where(a =>
            a.RecumbentExercise || a.Walking || a.Strength || a.Stretching || a.PtExercises).ToList();
        var exWithSym = exDays.Count(a => !string.IsNullOrWhiteSpace(a.PostExerciseSymptoms));
        var exDayBurdens = exDays.Select(a => BurdenForDay(a.Day)).Where(b => b.HasValue).Select(b => b!.Value).ToList();
        var nonExDayBurdens = actions
            .Where(a => !(a.RecumbentExercise || a.Walking || a.Strength || a.Stretching || a.PtExercises))
            .Select(a => BurdenForDay(a.Day))
            .Where(b => b.HasValue)
            .Select(b => b!.Value)
            .ToList();
        ExerciseToleranceRow? exTolerance = exDays.Count > 0
            ? new ExerciseToleranceRow(
                TotalSessions: exDays.Count,
                SessionsWithPostSymptoms: exWithSym,
                SymptomBurdenWhenExercised: AvgOrNull(exDayBurdens),
                SymptomBurdenWhenNotExercised: AvgOrNull(nonExDayBurdens))
            : null;

        // Actions vs Red episodes — for each preventive flag, compute %Red when done vs when not.
        // Red day = day with at least one Red status entry.
        var redDayLocal = statuses
            .Where(s => s.Status == DailyStatusKind.Red)
            .Select(s => DateOnly.FromDateTime(s.CreatedAt.LocalDateTime))
            .ToHashSet();

        var flagDefinitions = new (string Name, Func<PreventiveActionLog, bool> Pred)[]
        {
            ("Bebí electrolitos", a => a.ElectrolyteTaken),
            ("Agua antes de levantarme", a => a.MorningWaterBeforeStanding),
            ("Compresión puesta", a => a.CompressionSocks || a.WaistHighCompression || a.AbdominalCompression),
            ("Pacing planificado", a => a.PlannedRestBreaks || a.AvoidedOverexertion || a.UsedActivityPacing),
            ("Evité calor", a => a.AvoidedHeat || a.UsedCoolingVestFan || a.ColdShower || a.AvoidedHotBathSauna),
            ("Sueño suficiente", a => a.SleptEnough),
            ("Horarios consistentes", a => a.ConsistentBedtimes),
            ("Medicación según pauta", a => a.MedicationTakenAsPrescribed),
            ("Sin desayuno saltado", a => !a.SkippedBreakfast),
            ("Sin alcohol", a => a.AlcoholAvoided),
        };

        var actionsVsRed = flagDefinitions
            .Select(def =>
            {
                var when = actions.Where(a => def.Pred(a)).ToList();
                var whenNot = actions.Where(a => !def.Pred(a)).ToList();
                double? redRateWhen = when.Count == 0 ? null : Math.Round(100.0 * when.Count(a => redDayLocal.Contains(a.Day)) / when.Count, 1);
                double? redRateWhenNot = whenNot.Count == 0 ? null : Math.Round(100.0 * whenNot.Count(a => redDayLocal.Contains(a.Day)) / whenNot.Count, 1);
                double? delta = (redRateWhen.HasValue && redRateWhenNot.HasValue)
                    ? Math.Round(redRateWhenNot.Value - redRateWhen.Value, 1)
                    : null;
                return new ActionVsRedRow(def.Name, when.Count, whenNot.Count, redRateWhen, redRateWhenNot, delta);
            })
            .Where(r => r.DeltaPct.HasValue && r.DeltaPct.Value > 0)  // only show actions that LOOK protective
            .OrderByDescending(r => r.DeltaPct ?? -999)
            .Take(5)
            .ToList();

        return new TrendsDto(
            RangeDays: days,
            GreenCount: greenCount,
            OrangeCount: orangeCount,
            RedCount: redCount,
            EpisodeCount: episodes.Count,
            EpisodesPerWeek: perWeek,
            TopTriggers: topTriggers,
            AvgFatigue: avgFatigue,
            AvgBrainFog: avgBrainFog,
            AvgDizziness: avgDizziness,
            DailyStatusBuckets: buckets,
            BestWorst: bestWorst,
            SleepQualityVsSymptoms: sleepVsSymptoms,
            HydrationVsSymptoms: hydrationVsSymptoms,
            CompressionVsSymptoms: compressionVs,
            NoCompressionVsSymptoms: noCompressionVs,
            SkipBreakfastVsSymptoms: skipBreakfast,
            RegularBreakfastVsSymptoms: regularBreakfast,
            AmbientTempVsSymptoms: tempVsSymptoms,
            MenstrualPhaseVsSymptoms: phaseVsSymptoms,
            ExerciseTolerance: exTolerance,
            ActionsAssociatedWithFewerReds: actionsVsRed);
    }

    private static double? Avg(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        return Math.Round(list.Average(), 1);
    }

    private static double? AvgOrNull(IEnumerable<double> values) => Avg(values);

    private static double AvgBurden(IEnumerable<(int? F, int? B, int? D)> rows)
    {
        var vals = new List<double>();
        foreach (var r in rows)
        {
            var parts = new List<int>();
            if (r.F.HasValue) parts.Add(r.F.Value);
            if (r.B.HasValue) parts.Add(r.B.Value);
            if (r.D.HasValue) parts.Add(r.D.Value);
            if (parts.Count > 0) vals.Add(parts.Average());
        }
        return vals.Count == 0 ? 0 : Math.Round(vals.Average(), 1);
    }

    private static List<AssociationRow> BucketAvg(IEnumerable<(string Bucket, double Value)> rows)
    {
        return rows
            .GroupBy(r => r.Bucket)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new AssociationRow(g.Key, g.Count(), Math.Round(g.Average(x => x.Value), 1)))
            .ToList();
    }

    private static AssociationRow? AvgFor(IEnumerable<double> values, string bucket)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        return new AssociationRow(bucket, list.Count, Math.Round(list.Average(), 1));
    }

    private static string SleepBucket(int quality) =>
        quality <= 3 ? "1-baja (0-3)" : quality <= 6 ? "2-media (4-6)" : "3-alta (7-10)";

    private static string HydrationBucket(int ml) =>
        ml < 1500 ? "1-baja (<1500ml)" : ml <= 2500 ? "2-media (1500-2500ml)" : "3-alta (>2500ml)";

    private static string TempBucket(decimal c) =>
        c < 18 ? "1-fresco (<18°)" : c <= 24 ? "2-templado (18-24°)" : c <= 30 ? "3-cálido (24-30°)" : "4-caliente (>30°)";

    private static string PhaseBucket(int day) =>
        day <= 5 ? "1-menstrual (1-5)" :
        day <= 13 ? "2-folicular (6-13)" :
        day <= 16 ? "3-ovulación (14-16)" :
        "4-lútea (17+)";

    private static DateOnly StartOfIsoWeek(DateTime utc)
    {
        var d = utc.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return DateOnly.FromDateTime(d.AddDays(-diff));
    }
}
