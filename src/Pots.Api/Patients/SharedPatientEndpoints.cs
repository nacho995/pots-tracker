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
        // Phase 7.2.b: cross-patient READ for symptoms, vitals, actions.
        // Viewer-level grant is enough — RLS on these tables uses
        // has_patient_access, not has_patient_edit_access.
        group.MapGet("/symptoms", GetSymptomsAsync);
        group.MapGet("/vitals", GetVitalsAsync);
        group.MapGet("/actions", GetActionsAsync);
        // Phase 7.2.c: Editor (or Owner) writes the rest of the patient's
        // data on her behalf. RLS gates via has_patient_edit_access. No
        // DELETE exposed — Editor mistakes shouldn't be irreversible.
        group.MapPost("/symptoms", RecordSymptomsAsync);
        group.MapPost("/vitals", RecordVitalsAsync);
        group.MapPost("/actions", UpsertActionsAsync);
        group.MapPost("/episodes", CreateEpisodeAsync);
        // Phase 7.2.c: caregiver-side READ of aggregated trends + report,
        // explicitly filtered by patient.Id so a multi-patient grantee
        // sees only the requested patient.
        group.MapGet("/trends", GetTrendsAsync);
        group.MapGet("/report", GetReportAsync);
    }

    private static async Task<IResult> GetTrendsAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int days = 30)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists) return Results.NotFound();
        var trends = await TrendsEndpoints.ComputeAsync(db, patientId, days, cancellationToken);
        return Results.Ok(trends);
    }

    private static async Task<IResult> GetReportAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();
        var rangeTo = to ?? DateTimeOffset.UtcNow;
        var rangeFrom = from ?? rangeTo.AddDays(-30);
        var report = await ReportEndpoints.ComputeAsync(db, patient.Id, patient.Name, rangeFrom, rangeTo, cancellationToken);
        return Results.Ok(report);
    }

    private static async Task<IResult> RecordSymptomsAsync(
        Guid patientId,
        [FromBody] RecordSymptomsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

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
            log = SymptomLog.Create(patient.Id, userId, new SymptomData
            {
                RecordedAt = dto.RecordedAt,
                Dizziness = dto.Dizziness, Palpitations = dto.Palpitations,
                TachycardiaSensation = dto.TachycardiaSensation, ChestDiscomfort = dto.ChestDiscomfort,
                ShortnessOfBreath = dto.ShortnessOfBreath, NearFainting = dto.NearFainting,
                FaintingEpisode = dto.FaintingEpisode, BloodPooling = dto.BloodPooling,
                BrainFog = dto.BrainFog, Headache = dto.Headache, VisualDisturbance = dto.VisualDisturbance,
                Tremor = dto.Tremor, Weakness = dto.Weakness, Fatigue = dto.Fatigue, Sleepiness = dto.Sleepiness,
                Nausea = dto.Nausea, AbdominalPain = dto.AbdominalPain, Bloating = dto.Bloating,
                Bowel = bowel, AppetiteLevel = dto.AppetiteLevel,
                HeatIntolerance = dto.HeatIntolerance, Sweating = dto.Sweating,
                Chills = dto.Chills, Flushing = dto.Flushing, ColdExtremities = dto.ColdExtremities,
                Anxiety = dto.Anxiety, Mood = dto.Mood,
                AbilityToWork = dto.AbilityToWork, AbilityToWalk = dto.AbilityToWalk,
                SocialTolerance = dto.SocialTolerance,
            });
        }
        catch (DomainException ex) { return Results.BadRequest(new { code = "symptoms.invalid", message = ex.Message }); }

        db.SymptomLogs.Add(log);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId,
            patient.OwnerUserId == userId ? "symptoms.recorded" : "symptoms.recorded_by_editor",
            nameof(SymptomLog), log.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/patients/{patientId}/symptoms/{log.Id}", new SymptomLogDto(log.Id, log.RecordedAt));
    }

    private static async Task<IResult> RecordVitalsAsync(
        Guid patientId,
        [FromBody] RecordVitalsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

        VitalSignLog log;
        try
        {
            log = VitalSignLog.Create(patient.Id, userId, new VitalSignData
            {
                RecordedAt = dto.RecordedAt,
                RestingHrBpm = dto.RestingHrBpm,
                StandingHrBpm2Min = dto.StandingHrBpm2Min, StandingHrBpm5Min = dto.StandingHrBpm5Min,
                StandingHrBpm10Min = dto.StandingHrBpm10Min,
                BpLyingSystolic = dto.BpLyingSystolic, BpLyingDiastolic = dto.BpLyingDiastolic,
                BpSittingSystolic = dto.BpSittingSystolic, BpSittingDiastolic = dto.BpSittingDiastolic,
                BpStandingSystolic = dto.BpStandingSystolic, BpStandingDiastolic = dto.BpStandingDiastolic,
                Spo2Percent = dto.Spo2Percent, WeightKg = dto.WeightKg,
                MenstrualCycleDay = dto.MenstrualCycleDay,
                SleepDurationMinutes = dto.SleepDurationMinutes, SleepQuality = dto.SleepQuality,
                Steps = dto.Steps, ExerciseMinutes = dto.ExerciseMinutes,
                TimeUprightMinutes = dto.TimeUprightMinutes, TimeLyingMinutes = dto.TimeLyingMinutes,
                AmbientTempC = dto.AmbientTempC,
            });
        }
        catch (DomainException ex) { return Results.BadRequest(new { code = "vitals.invalid", message = ex.Message }); }

        db.VitalSignLogs.Add(log);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId,
            patient.OwnerUserId == userId ? "vitals.recorded" : "vitals.recorded_by_editor",
            nameof(VitalSignLog), log.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/patients/{patientId}/vitals/{log.Id}", new VitalLogDto(log.Id, log.RecordedAt));
    }

    private static async Task<IResult> UpsertActionsAsync(
        Guid patientId,
        [FromBody] UpsertActionsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

        // Salt gate (CLAUDE.md §2): salt fields only allowed if patient has
        // clinician-prescribed salt target on PatientTargets.
        var targets = await db.PatientTargets.FirstOrDefaultAsync(t => t.PatientId == patient.Id, cancellationToken);
        var saltTargetAllowed = targets?.SaltTargetEnabled ?? false;

        if (!Enum.TryParse<CaffeineLevel>(dto.CaffeineLevel, ignoreCase: false, out var caffeine) || !Enum.IsDefined(caffeine))
            return Results.BadRequest(new { code = "actions.caffeine_invalid" });

        UrineColor? urine = null;
        if (!string.IsNullOrWhiteSpace(dto.UrineColor))
        {
            if (!Enum.TryParse<UrineColor>(dto.UrineColor, ignoreCase: false, out var u) || !Enum.IsDefined(u))
                return Results.BadRequest(new { code = "actions.urine_invalid" });
            urine = u;
        }

        var data = new PreventiveActionData
        {
            FluidMl = dto.FluidMl, ElectrolyteTaken = dto.ElectrolyteTaken,
            MorningWaterBeforeStanding = dto.MorningWaterBeforeStanding, UrineColor = urine,
            SaltTargetReached = dto.SaltTargetReached,
            RegularMeals = dto.RegularMeals, SkippedBreakfast = dto.SkippedBreakfast,
            SmallFrequentMeals = dto.SmallFrequentMeals, AvoidedLargeHighCarbMeal = dto.AvoidedLargeHighCarbMeal,
            AdequateProtein = dto.AdequateProtein, AlcoholAvoided = dto.AlcoholAvoided,
            CaffeineLevel = caffeine,
            CompressionSocks = dto.CompressionSocks, WaistHighCompression = dto.WaistHighCompression,
            AbdominalCompression = dto.AbdominalCompression, CompressionHoursWorn = dto.CompressionHoursWorn,
            RecumbentExercise = dto.RecumbentExercise, Walking = dto.Walking, Strength = dto.Strength,
            Stretching = dto.Stretching, PtExercises = dto.PtExercises,
            ExerciseDurationMinutes = dto.ExerciseDurationMinutes, ExerciseIntensity = dto.ExerciseIntensity,
            PostExerciseSymptoms = dto.PostExerciseSymptoms,
            PlannedRestBreaks = dto.PlannedRestBreaks, AvoidedOverexertion = dto.AvoidedOverexertion,
            UsedActivityPacing = dto.UsedActivityPacing, AvoidedLongStanding = dto.AvoidedLongStanding,
            SatDuringShowerCooking = dto.SatDuringShowerCooking, MobilityAid = dto.MobilityAid,
            AvoidedHeat = dto.AvoidedHeat, UsedCoolingVestFan = dto.UsedCoolingVestFan,
            ColdShower = dto.ColdShower, AvoidedHotBathSauna = dto.AvoidedHotBathSauna,
            StayedInShadeAc = dto.StayedInShadeAc,
            SleptEnough = dto.SleptEnough, SleepQuality = dto.SleepQuality,
            ConsistentBedtimes = dto.ConsistentBedtimes, NapTaken = dto.NapTaken,
            WokeRefreshed = dto.WokeRefreshed,
            MedicationTakenAsPrescribed = dto.MedicationTakenAsPrescribed,
            MissedDose = dto.MissedDose, SideEffects = dto.SideEffects,
            NewMedicationOrSupplement = dto.NewMedicationOrSupplement,
            RescueMedicationUsed = dto.RescueMedicationUsed,
        };

        var existing = await db.PreventiveActionLogs
            .FirstOrDefaultAsync(e => e.PatientId == patient.Id && e.Day == dto.Day, cancellationToken);
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
        catch (DomainException ex) { return Results.BadRequest(new { code = "actions.invalid", message = ex.Message }); }

        db.AuditLog.Add(AuditLogEntry.Record(
            userId,
            patient.OwnerUserId == userId ? "actions.upserted" : "actions.upserted_by_editor",
            nameof(PreventiveActionLog), existing.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ActionLogDto(existing.Id, existing.Day));
    }

    private static async Task<IResult> CreateEpisodeAsync(
        Guid patientId,
        [FromBody] CreateEpisodeDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient is null) return Results.NotFound();

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
            episode = Episode.Create(patient.Id, userId, new EpisodeData
            {
                StartTime = dto.StartTime, DurationMinutes = dto.DurationMinutes,
                MainSymptom = dto.MainSymptom, PostureBefore = posture, TriggerSuspected = trigger,
                HrDuringBpm = dto.HrDuringBpm,
                BpDuringSystolic = dto.BpDuringSystolic, BpDuringDiastolic = dto.BpDuringDiastolic,
                ActionTaken = dto.ActionTaken, RecoveryTimeMinutes = dto.RecoveryTimeMinutes,
                PreventedFainting = dto.PreventedFainting, Note = dto.Note,
            });
        }
        catch (DomainException ex) { return Results.BadRequest(new { code = "episode.invalid", message = ex.Message }); }

        db.Episodes.Add(episode);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId,
            patient.OwnerUserId == userId ? "episode.created" : "episode.created_by_editor",
            nameof(Episode), episode.Id, patient.Id,
            $"{{\"trigger\":\"{trigger}\"}}"));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/patients/{patientId}/episodes/{episode.Id}", new { id = episode.Id });
    }

    private static async Task<IResult> GetSymptomsAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int take = 30)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        if (take < 1) take = 1;
        if (take > 200) take = 200;

        var rows = await db.SymptomLogs
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.RecordedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ownerUserId = await db.Patients
            .Where(p => p.Id == patientId).Select(p => p.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var nameMap = await ResolveRecorderNamesAsync(db, patientId,
            rows.Where(r => r.RecordedByUserId != ownerUserId).Select(r => r.RecordedByUserId), cancellationToken);

        var dtos = rows.Select(s => new SymptomLogFullDto(
            s.Id, s.RecordedAt,
            s.RecordedByUserId == ownerUserId ? null : nameMap.GetValueOrDefault(s.RecordedByUserId),
            s.Dizziness, s.Palpitations, s.TachycardiaSensation,
            s.ChestDiscomfort, s.ShortnessOfBreath, s.NearFainting,
            s.FaintingEpisode, s.BloodPooling,
            s.BrainFog, s.Headache, s.VisualDisturbance,
            s.Tremor, s.Weakness, s.Fatigue, s.Sleepiness,
            s.Nausea, s.AbdominalPain, s.Bloating,
            s.Bowel?.ToString(), s.AppetiteLevel,
            s.HeatIntolerance, s.Sweating, s.Chills, s.Flushing, s.ColdExtremities,
            s.Anxiety, s.Mood,
            s.AbilityToWork, s.AbilityToWalk, s.SocialTolerance)).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetVitalsAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int take = 30)
    {
        if (take < 1) take = 1;
        if (take > 200) take = 200;

        var rows = await db.VitalSignLogs
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.RecordedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ownerUserId = await db.Patients
            .Where(p => p.Id == patientId).Select(p => p.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var nameMap = await ResolveRecorderNamesAsync(db, patientId,
            rows.Where(r => r.RecordedByUserId != ownerUserId).Select(r => r.RecordedByUserId), cancellationToken);

        var dtos = rows.Select(v => new VitalLogFullDto(
            v.Id, v.RecordedAt,
            v.RecordedByUserId == ownerUserId ? null : nameMap.GetValueOrDefault(v.RecordedByUserId),
            v.RestingHrBpm, v.StandingHrBpm2Min, v.StandingHrBpm5Min, v.StandingHrBpm10Min,
            v.BpLyingSystolic, v.BpLyingDiastolic,
            v.BpSittingSystolic, v.BpSittingDiastolic,
            v.BpStandingSystolic, v.BpStandingDiastolic,
            v.Spo2Percent, v.WeightKg, v.MenstrualCycleDay,
            v.SleepDurationMinutes, v.SleepQuality,
            v.Steps, v.ExerciseMinutes,
            v.TimeUprightMinutes, v.TimeLyingMinutes,
            v.AmbientTempC)).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetActionsAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int take = 30)
    {
        if (take < 1) take = 1;
        if (take > 200) take = 200;

        var rows = await db.PreventiveActionLogs
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.Day)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ownerUserId = await db.Patients
            .Where(p => p.Id == patientId).Select(p => p.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var nameMap = await ResolveRecorderNamesAsync(db, patientId,
            rows.Where(r => r.RecordedByUserId != ownerUserId).Select(r => r.RecordedByUserId), cancellationToken);

        var dtos = rows.Select(a => new ActionLogFullDto(
            a.Id, a.Day,
            a.RecordedByUserId == ownerUserId ? null : nameMap.GetValueOrDefault(a.RecordedByUserId),
            a.FluidMl, a.ElectrolyteTaken, a.MorningWaterBeforeStanding, a.UrineColor?.ToString(),
            a.SaltTargetReached,
            a.RegularMeals, a.SkippedBreakfast, a.SmallFrequentMeals,
            a.AvoidedLargeHighCarbMeal, a.AdequateProtein, a.AlcoholAvoided,
            a.CaffeineLevel.ToString(),
            a.CompressionSocks, a.WaistHighCompression, a.AbdominalCompression,
            a.CompressionHoursWorn,
            a.RecumbentExercise, a.Walking, a.Strength, a.Stretching, a.PtExercises,
            a.ExerciseDurationMinutes, a.ExerciseIntensity, a.PostExerciseSymptoms,
            a.PlannedRestBreaks, a.AvoidedOverexertion, a.UsedActivityPacing,
            a.AvoidedLongStanding, a.SatDuringShowerCooking, a.MobilityAid,
            a.AvoidedHeat, a.UsedCoolingVestFan, a.ColdShower,
            a.AvoidedHotBathSauna, a.StayedInShadeAc,
            a.SleptEnough, a.SleepQuality, a.ConsistentBedtimes, a.NapTaken, a.WokeRefreshed,
            a.MedicationTakenAsPrescribed, a.MissedDose,
            a.SideEffects, a.NewMedicationOrSupplement, a.RescueMedicationUsed)).ToList();

        return Results.Ok(dtos);
    }

    private sealed record EmailRow(Guid UserId, string Email, string? DisplayName);

    private static async Task<Dictionary<Guid, string>> ResolveRecorderNamesAsync(
        PotsDbContext db, Guid patientId, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var distinct = userIds.Distinct().ToList();
        if (distinct.Count == 0) return new();
        var rows = await db.Database
            .SqlQuery<EmailRow>($"SELECT * FROM list_patient_user_emails({patientId})")
            .ToListAsync(ct);
        return rows
            .Where(r => distinct.Contains(r.UserId))
            .ToDictionary(
                r => r.UserId,
                r => string.IsNullOrWhiteSpace(r.DisplayName) ? r.Email : r.DisplayName!);
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

        // Phase 6 attribution. Look up the patient's owner user_id once to
        // know which entries need a RecorderName surfaced (those NOT
        // recorded by the owner).
        var ownerUserId = await db.Patients
            .Where(p => p.Id == patientId)
            .Select(p => p.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var nameMap = await StatusEndpoints.BuildRecorderNameMapAsync(
            db, patientId, rows, ownerUserId, cancellationToken);

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

        var dtos = rows
            .Select(e => StatusEndpoints.MapDtoWithRecorder(e, ownerUserId, nameMap))
            .ToList();

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
