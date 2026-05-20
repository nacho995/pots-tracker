using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Auth;

// Phase 6 — user account endpoints. Separates "your name" (User.DisplayName)
// from "your POTS profile" (Patient.Name).
//
// The previous /me/patient flow was the only writable identity surface; a
// caregiver who wanted a name to appear in attribution was forced to type
// it into the patient-name field, which created a Patient row and turned
// them into a POTS patient by accident. /me/account lets users set their
// own display name without becoming a patient.
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/account").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync);
    }

    private static async Task<IResult> GetAsync(
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return Results.NotFound();

        var isPatient = await db.Patients.AnyAsync(p => p.OwnerUserId == userId, cancellationToken);
        var hasShared = await db.PatientGrants
            .AnyAsync(g => g.GranteeUserId == userId && g.RevokedAt == null, cancellationToken);

        return Results.Ok(new AccountDto(user.Email, user.DisplayName, isPatient, hasShared));
    }

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateAccountDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return Results.NotFound();

        try { user.Rename(dto.DisplayName); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "account.invalid", message = ex.Message }); }

        await db.SaveChangesAsync(cancellationToken);

        var isPatient = await db.Patients.AnyAsync(p => p.OwnerUserId == userId, cancellationToken);
        var hasShared = await db.PatientGrants
            .AnyAsync(g => g.GranteeUserId == userId && g.RevokedAt == null, cancellationToken);

        return Results.Ok(new AccountDto(user.Email, user.DisplayName, isPatient, hasShared));
    }
}
