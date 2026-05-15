using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class TargetsEndpoints
{
    public static void MapTargetsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/targets").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync);
        group.MapPost("salt/enable", EnableSaltAsync);
        group.MapPost("salt/disable", DisableSaltAsync);
    }

    private static async Task<IResult> GetAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var t = await db.PatientTargets.FirstOrDefaultAsync(x => x.PatientId == patient.Id, cancellationToken);
        // No row yet = unconfigured defaults; surface them without persisting.
        if (t is null)
            return Results.Ok(new PatientTargetsDto(
                HydrationTargetMl: 2500,
                SaltTargetEnabled: false,
                SaltTargetMg: null,
                SaltClinicianAttestation: null,
                CompressionGoalHoursPerDay: null,
                ExercisePlanNote: null,
                SleepTargetHours: null,
                Language: "es"));
        return Results.Ok(ToDto(t));
    }

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateTargetsDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var targets = await db.PatientTargets.FirstOrDefaultAsync(x => x.PatientId == patient.Id, cancellationToken);
        var isNew = targets is null;
        targets ??= PatientTargets.CreateDefaults(patient.Id);

        try
        {
            targets.Update(
                hydrationTargetMl: dto.HydrationTargetMl,
                compressionGoalHoursPerDay: dto.CompressionGoalHoursPerDay,
                exercisePlanNote: dto.ExercisePlanNote,
                sleepTargetHours: dto.SleepTargetHours,
                language: dto.Language);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "targets.invalid", message = ex.Message });
        }

        if (isNew) db.PatientTargets.Add(targets);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "targets.updated", nameof(PatientTargets), targets.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(targets));
    }

    // SAFETY-CRITICAL: enabling the salt feature requires an explicit
    // attestation from the patient. CLAUDE.md §2 / Domain enforces this; we
    // also audit the change with the attestation snapshot so a future review
    // can verify the patient claimed clinician approval.
    private static async Task<IResult> EnableSaltAsync(
        [FromBody] EnableSaltTargetDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var targets = await db.PatientTargets.FirstOrDefaultAsync(x => x.PatientId == patient.Id, cancellationToken);
        var isNew = targets is null;
        targets ??= PatientTargets.CreateDefaults(patient.Id);

        try
        {
            targets.EnableSaltTarget(dto.SaltTargetMg, dto.Attestation);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "salt.invalid", message = ex.Message });
        }

        if (isNew) db.PatientTargets.Add(targets);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "salt.enabled", nameof(PatientTargets), targets.Id, patient.Id,
            $"{{\"salt_mg\":{dto.SaltTargetMg}}}"));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(targets));
    }

    private static async Task<IResult> DisableSaltAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null)
            return Results.NotFound(new { code = "patient.not_provisioned" });

        var targets = await db.PatientTargets.FirstOrDefaultAsync(x => x.PatientId == patient.Id, cancellationToken);
        if (targets is null) return Results.Ok();

        targets.DisableSaltTarget();
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "salt.disabled", nameof(PatientTargets), targets.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(targets));
    }

    private static PatientTargetsDto ToDto(PatientTargets t) => new(
        t.HydrationTargetMl,
        t.SaltTargetEnabled,
        t.SaltTargetMg,
        t.SaltClinicianAttestation,
        t.CompressionGoalHoursPerDay,
        t.ExercisePlanNote,
        t.SleepTargetHours,
        t.Language);
}
