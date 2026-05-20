using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Api.Auth;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class GrantEndpoints
{
    public static void MapGrantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/patient/grants").RequireAuthorization();

        group.MapGet("", ListGrantsAsync);
        group.MapPost("", InviteAsync);
        group.MapDelete("{grantId:guid}", RevokeAsync);
    }

    private static async Task<IResult> ListGrantsAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        // Use the SECURITY DEFINER function list_my_patient_grants() so the
        // grantee's email (which RLS on users would hide) can be returned to
        // the patient owner. The function verifies app_current_user_id() =
        // patient owner internally.
        // SqlQuery returns rows mapped by EF's snake_case convention: function
        // columns (grant_id, grantee_email, role_name, granted_at) map to the
        // PascalCase record properties below.
        var rows = await db.Database
            .SqlQuery<GrantListRow>($"SELECT * FROM list_my_patient_grants()")
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(r => new GrantDto(r.GrantId, r.GranteeEmail, r.RoleName, r.GrantedAt)).ToList();
        return Results.Ok(dtos);
    }

    private sealed record GrantListRow(Guid GrantId, string GranteeEmail, string RoleName, DateTimeOffset GrantedAt);

    private static async Task<IResult> InviteAsync(
        [FromBody] InviteGrantDto dto,
        HttpContext http,
        PotsDbContext db,
        IUserContext ctx,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");
        var inviterEmail = http.User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? "Alguien";

        // Phase 3 invariant: all NEW invitations are Viewer. Editor is no
        // longer reachable at invite time; a Viewer must explicitly request
        // an upgrade via /patients/{id}/grant-upgrade-requests and the patient
        // owner must approve it. This makes the act of granting write access
        // a deliberate per-person decision rather than a tickbox on an
        // invite form.
        // We accept either "Viewer" or omitted/null for backward-compat with
        // any caller (tests, scripts) still sending the field — anything else
        // is rejected with 400, never silently downgraded.
        if (!string.IsNullOrEmpty(dto.Role) && dto.Role != nameof(GrantRole.Viewer))
            return Results.BadRequest(new { code = "grant.role_locked_to_viewer" });
        var role = GrantRole.Viewer;

        string normalizedEmail;
        try { normalizedEmail = EmailValidator.Normalize(dto.GranteeEmail); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "grant.invalid_email", message = ex.Message }); }

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        // Auto-provision the grantee account if it doesn't exist. The
        // SECURITY DEFINER function is idempotent: returns the existing
        // user_id if present, creates one (and audits user.provisioned) if not.
        // The grantee then receives a magic-link email below — they don't need
        // to sign up separately before the inviter can issue the grant.
        var granteeIds = await db.Database
            .SqlQuery<Guid>($"SELECT auth_provision_user({normalizedEmail}::citext) AS \"Value\"")
            .ToListAsync(cancellationToken);
        var granteeUserId = granteeIds.Single();

        if (granteeUserId == userId)
            return Results.BadRequest(new { code = "grant.self_grant_not_allowed" });

        PatientGrant grant;
        try
        {
            grant = PatientGrant.Issue(
                patientId: patient.Id,
                granteeUserId: granteeUserId,
                role: role,
                grantedByUserId: userId,
                patientOwnerUserId: patient.OwnerUserId);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { code = "grant.invalid", message = ex.Message });
        }

        db.PatientGrants.Add(grant);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "grant.issued", nameof(PatientGrant), grant.Id, patient.Id,
            $"{{\"role\":\"{role}\",\"grantee_email\":\"{normalizedEmail}\"}}"));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(new { code = "grant.already_active" });
        }

        // Fire-and-forget the invitation email. Failure to send shouldn't
        // unwind the grant — the inviter can always re-send via a future
        // "resend invitation" UX without re-creating the grant.
        try
        {
            await auth.SendInvitationAsync(normalizedEmail, inviterEmail, role.ToString(), cancellationToken);
        }
        catch
        {
            /* swallow: grant is created, email failure is recoverable */
        }

        return Results.Created($"/me/patient/grants/{grant.Id}",
            new GrantDto(grant.Id, normalizedEmail, role.ToString(), grant.GrantedAt));
    }

    private static async Task<IResult> RevokeAsync(
        Guid grantId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (patient is null) return Results.NotFound(new { code = "patient.not_provisioned" });

        var grant = await db.PatientGrants.FirstOrDefaultAsync(
            g => g.Id == grantId && g.PatientId == patient.Id, cancellationToken);
        if (grant is null || grant.RevokedAt is not null) return Results.NotFound();

        grant.Revoke(userId);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "grant.revoked", nameof(PatientGrant), grant.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
}
