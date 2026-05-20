using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me").RequireAuthorization();

        me.MapGet("/patient", GetMyPatientAsync);
        me.MapPost("/patient", CreateMyPatientAsync);
        me.MapPut("/patient", RenameMyPatientAsync);
    }

    private static async Task<IResult> GetMyPatientAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        return patient is null
            ? Results.NotFound(new { code = "patient.not_provisioned" })
            : Results.Ok(new PatientDto(patient.Id, patient.Name));
    }

    private static async Task<IResult> CreateMyPatientAsync(
        [FromBody] CreatePatientDto dto,
        HttpContext http,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var existing = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (existing is not null)
            return Results.Conflict(new { code = "patient.already_exists" });

        // Phase 6 trap-close: refuse to create a Patient for a user who is
        // already an active caregiver on someone else's patient. Pre-Phase-6
        // the /profile and /welcome flows let a caregiver accidentally
        // create their own Patient by typing their display name; users got
        // stuck as both, the routing prioritised /today, and they never
        // reached the shared dashboard they were invited to. Caregivers
        // who genuinely also have POTS go through an explicit "I also have
        // POTS" confirmation that sends `?confirmAlsoPatient=true`, which
        // re-opens this path.
        var hasActiveGrant = await db.PatientGrants
            .AnyAsync(g => g.GranteeUserId == userId && g.RevokedAt == null, cancellationToken);
        if (hasActiveGrant)
        {
            var confirm = http.Request.Query["confirmAlsoPatient"].FirstOrDefault();
            if (!string.Equals(confirm, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new
                {
                    code = "patient.caregiver_first",
                    message = "You're already a caregiver. Confirm explicitly that you also have POTS."
                });
            }
        }

        Patient patient;
        try { patient = Patient.Create(userId, dto.Name); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "patient.invalid", message = ex.Message }); }

        db.Patients.Add(patient);
        db.AuditLog.Add(AuditLogEntry.Record(userId, "patient.created", nameof(Patient), patient.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/me/patient", new PatientDto(patient.Id, patient.Name));
    }

    private static async Task<IResult> RenameMyPatientAsync(
        [FromBody] UpdatePatientDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        try { patient.Rename(dto.Name); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "patient.invalid", message = ex.Message }); }

        db.AuditLog.Add(AuditLogEntry.Record(userId, "patient.renamed", nameof(Patient), patient.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PatientDto(patient.Id, patient.Name));
    }
}
