using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/report").RequireAuthorization();
        group.MapGet("", GetReportAsync);
        group.MapGet("csv", GetCsvAsync);
    }

    // Internal so SharedPatientEndpoints can reuse the report computation
    // when serving caregiver-side cross-patient reads (Phase 7.2.c).
    internal static async Task<DoctorReportDto> ComputeAsync(
        PotsDbContext db,
        Guid patientId,
        string patientName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // Phase 7.2.c: filter explicitly by patient.Id so cross-patient
        // callers (a caregiver with access to multiple patients) see only
        // the requested one. Pre-Phase-7.2 these queries relied on RLS to
        // narrow to the caller's accessible set, which worked when the
        // caller was the owner of exactly one patient. Now that grantees
        // span multiple patients, the explicit filter is the floor.
        var statuses = await db.DailyStatusEntries
            .Where(e => e.PatientId == patientId && e.CreatedAt >= from && e.CreatedAt <= to)
            .Select(e => new { e.Status, e.Note, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var episodes = await db.Episodes
            .Where(e => e.PatientId == patientId && e.StartTime >= from && e.StartTime <= to)
            .Select(e => new { e.HrDuringBpm, e.TriggerSuspected, e.PreventedFainting, e.Note })
            .ToListAsync(cancellationToken);

        var vitals = await db.VitalSignLogs
            .Where(e => e.PatientId == patientId && e.RecordedAt >= from && e.RecordedAt <= to)
            .ToListAsync(cancellationToken);

        var symptoms = await db.SymptomLogs
            .Where(e => e.PatientId == patientId && e.RecordedAt >= from && e.RecordedAt <= to)
            .ToListAsync(cancellationToken);

        var actions = await db.PreventiveActionLogs
            .Where(e => e.PatientId == patientId
                     && e.Day >= DateOnly.FromDateTime(from.UtcDateTime)
                     && e.Day <= DateOnly.FromDateTime(to.UtcDateTime))
            .ToListAsync(cancellationToken);

        double? Avg(IEnumerable<int?> xs)
        {
            var vs = xs.Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
            return vs.Count == 0 ? null : Math.Round(vs.Average(), 1);
        }

        var candidateAverages = new (string Name, double? Avg)[]
        {
            ("Mareo", Avg(symptoms.Select(s => s.Dizziness))),
            ("Palpitaciones", Avg(symptoms.Select(s => s.Palpitations))),
            ("Niebla mental", Avg(symptoms.Select(s => s.BrainFog))),
            ("Fatiga", Avg(symptoms.Select(s => s.Fatigue))),
            ("Dolor de cabeza", Avg(symptoms.Select(s => s.Headache))),
            ("Náuseas", Avg(symptoms.Select(s => s.Nausea))),
        };
        var topSymptoms = candidateAverages
            .Where(x => x.Avg.HasValue)
            .OrderByDescending(x => x.Avg!.Value)
            .Take(5)
            .Select(x => new SymptomAverage(x.Name, x.Avg!.Value))
            .ToList();

        var topTriggers = episodes
            .GroupBy(e => e.TriggerSuspected.ToString())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TriggerCountRow(g.Key, g.Count()))
            .ToList();

        var notes = statuses.Where(s => !string.IsNullOrWhiteSpace(s.Note))
            .Select(s => s.Note!.Trim())
            .Concat(episodes.Where(e => !string.IsNullOrWhiteSpace(e.Note)).Select(e => e.Note!.Trim()))
            .Take(20)
            .ToList();

        // Adherence: percentage of days in the range where the action was checked.
        double? PctTrue(IEnumerable<bool> xs)
        {
            var list = xs.ToList();
            if (list.Count == 0) return null;
            return Math.Round(100.0 * list.Count(b => b) / list.Count, 1);
        }

        var exerciseSessions = actions.Count(a =>
            a.RecumbentExercise || a.Walking || a.Strength || a.Stretching || a.PtExercises);
        var exerciseWithSymptoms = actions.Count(a =>
            (a.RecumbentExercise || a.Walking || a.Strength || a.Stretching || a.PtExercises)
            && !string.IsNullOrWhiteSpace(a.PostExerciseSymptoms));

        return new DoctorReportDto(
            From: from,
            To: to,
            PatientName: patientName,
            GreenCount: statuses.Count(s => s.Status == DailyStatusKind.Green),
            OrangeCount: statuses.Count(s => s.Status == DailyStatusKind.Orange),
            RedCount: statuses.Count(s => s.Status == DailyStatusKind.Red),
            EpisodeCount: episodes.Count,
            AvgRestingHrBpm: Avg(vitals.Select(v => v.RestingHrBpm)),
            AvgStandingHrBpm: Avg(vitals.Select(v => v.StandingHrBpm5Min ?? v.StandingHrBpm10Min ?? v.StandingHrBpm2Min)),
            AvgBpLyingSys: Avg(vitals.Select(v => v.BpLyingSystolic)),
            AvgBpLyingDia: Avg(vitals.Select(v => v.BpLyingDiastolic)),
            AvgBpSittingSys: Avg(vitals.Select(v => v.BpSittingSystolic)),
            AvgBpSittingDia: Avg(vitals.Select(v => v.BpSittingDiastolic)),
            AvgBpStandingSys: Avg(vitals.Select(v => v.BpStandingSystolic)),
            AvgBpStandingDia: Avg(vitals.Select(v => v.BpStandingDiastolic)),
            MaxHrInEpisodeBpm: episodes.Select(e => e.HrDuringBpm).Where(v => v.HasValue).DefaultIfEmpty(null).Max(),
            TopSymptoms: topSymptoms,
            TopTriggers: topTriggers,
            AvgDailyFluidMl: Avg(actions.Select(a => a.FluidMl)),
            AvgSaltMgPerDay: null, // salt tracking is binary (target reached y/n) — no mg total per day in v1
            CompressionAdherencePct: PctTrue(actions.Select(a => a.CompressionSocks || a.WaistHighCompression || a.AbdominalCompression)),
            ExerciseAdherencePct: PctTrue(actions.Select(a => a.RecumbentExercise || a.Walking || a.Strength || a.Stretching || a.PtExercises)),
            MedicationAdherencePct: PctTrue(actions.Select(a => a.MedicationTakenAsPrescribed)),
            EpisodesPreventedFainting: episodes.Count(e => e.PreventedFainting == true),
            ExerciseSessions: exerciseSessions,
            ExerciseSessionsWithPostSymptoms: exerciseWithSymptoms,
            PatientNotes: notes);
    }

    private static async Task<IResult> GetReportAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var userId = ctx.CurrentUserId ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        var rangeTo = to ?? DateTimeOffset.UtcNow;
        var rangeFrom = from ?? rangeTo.AddDays(-30);
        var report = await ComputeAsync(db, patient.Id, patient.Name, rangeFrom, rangeTo, cancellationToken);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetCsvAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var userId = ctx.CurrentUserId ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        var rangeTo = to ?? DateTimeOffset.UtcNow;
        var rangeFrom = from ?? rangeTo.AddDays(-30);
        var r = await ComputeAsync(db, patient.Id, patient.Name, rangeFrom, rangeTo, cancellationToken);

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("clave,valor");
        sb.AppendLine($"paciente,\"{Csv(r.PatientName)}\"");
        sb.AppendLine($"desde,{r.From:O}");
        sb.AppendLine($"hasta,{r.To:O}");
        sb.AppendLine($"dias_verde,{r.GreenCount}");
        sb.AppendLine($"dias_naranja,{r.OrangeCount}");
        sb.AppendLine($"dias_rojo,{r.RedCount}");
        sb.AppendLine($"episodios,{r.EpisodeCount}");
        sb.AppendLine($"hr_reposo_media_bpm,{r.AvgRestingHrBpm?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"hr_de_pie_media_bpm,{r.AvgStandingHrBpm?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"bp_tumbada_sis,{r.AvgBpLyingSys?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"bp_tumbada_dia,{r.AvgBpLyingDia?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"bp_de_pie_sis,{r.AvgBpStandingSys?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"bp_de_pie_dia,{r.AvgBpStandingDia?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"hr_max_en_episodio_bpm,{r.MaxHrInEpisodeBpm?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"adherencia_compresion_pct,{r.CompressionAdherencePct?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"adherencia_ejercicio_pct,{r.ExerciseAdherencePct?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"adherencia_medicacion_pct,{r.MedicationAdherencePct?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"liquido_diario_media_ml,{r.AvgDailyFluidMl?.ToString(inv) ?? string.Empty}");
        sb.AppendLine($"episodios_con_desmayo_evitado,{r.EpisodesPreventedFainting}");
        sb.AppendLine();
        sb.AppendLine("sintoma,media_0_10");
        foreach (var s in r.TopSymptoms) sb.AppendLine($"\"{Csv(s.Name)}\",{s.Average.ToString(inv)}");
        sb.AppendLine();
        sb.AppendLine("desencadenante,episodios");
        foreach (var t in r.TopTriggers) sb.AppendLine($"\"{Csv(t.Trigger)}\",{t.Count}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var name = $"pots-report-{r.From:yyyyMMdd}-{r.To:yyyyMMdd}.csv";
        return Results.File(bytes, "text/csv", name);
    }

    private static string Csv(string s) => s.Replace("\"", "\"\"");
}
