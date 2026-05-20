using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

// Endpoints for the Viewer→Editor upgrade flow.
//
//   POST /patients/{patientId}/grant-upgrade-requests
//     Viewer-grant holder asks the patient owner for Editor permissions.
//
//   GET  /me/patient/grant-upgrade-requests
//     Patient owner's inbox: PENDING requests on her patient (joined with
//     requester email via SECURITY DEFINER fn).
//
//   POST /me/patient/grant-upgrade-requests/{requestId}/approve
//     Owner approves: in the same transaction, the underlying PatientGrant
//     is upgraded from Viewer to Editor and the request is marked Approved.
//
//   POST /me/patient/grant-upgrade-requests/{requestId}/deny
//     Owner denies. Grant role unchanged.
//
//   DELETE /me/grant-upgrade-requests/{requestId}
//     Requester withdraws her own PENDING request. Soft-state transition
//     to Cancelled — the row stays for audit; no DB DELETE.
//
// Per CLAUDE.md the patient owns her data: only she may issue the upgrade.
// Editor invitations directly at invite time are now disabled at the API
// layer (see GrantEndpoints.InviteAsync); Editor is reachable only through
// this flow.
public static class GrantUpgradeRequestEndpoints
{
    public static void MapGrantUpgradeRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var patient = app.MapGroup("/patients/{patientId:guid}/grant-upgrade-requests").RequireAuthorization();
        patient.MapPost("", CreateAsync);

        var owner = app.MapGroup("/me/patient/grant-upgrade-requests").RequireAuthorization();
        owner.MapGet("", ListForOwnerAsync);
        owner.MapPost("/{requestId:guid}/approve", ApproveAsync);
        owner.MapPost("/{requestId:guid}/deny", DenyAsync);

        var requester = app.MapGroup("/me/grant-upgrade-requests").RequireAuthorization();
        requester.MapDelete("/{requestId:guid}", CancelAsync);
    }

    private static async Task<IResult> CreateAsync(
        Guid patientId,
        [FromBody] CreateGrantUpgradeRequestDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // Caller's active Viewer grant on this patient. RLS scopes to rows
        // we can see; we additionally pin grantee=us so we cannot request
        // on someone else's grant.
        var grant = await db.PatientGrants
            .FirstOrDefaultAsync(g =>
                g.PatientId == patientId
                && g.GranteeUserId == userId
                && g.RevokedAt == null, cancellationToken);
        if (grant is null)
            return Results.NotFound(new { code = "grant.not_found" });
        if (grant.Role != GrantRole.Viewer)
            return Results.Conflict(new { code = "grant.already_editor" });

        // Duplicate-pending check. The DB has a partial unique index as the
        // hard floor (ix_grant_upgrade_requests_grant_one_pending); we check
        // first to give a friendlier 409 instead of a 500 on unique-violation.
        var pendingExists = await db.GrantUpgradeRequests
            .AnyAsync(r =>
                r.GrantId == grant.Id
                && r.Status == GrantUpgradeRequestStatus.Pending, cancellationToken);
        if (pendingExists)
            return Results.Conflict(new { code = "request.already_pending" });

        GrantUpgradeRequest request;
        try { request = GrantUpgradeRequest.Create(grant, userId, dto.Message); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "request.invalid", message = ex.Message }); }

        db.GrantUpgradeRequests.Add(request);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "grant_upgrade_request.created", nameof(GrantUpgradeRequest),
            request.Id, patientId, null));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The partial unique index `ix_grant_upgrade_requests_grant_one_pending`
            // is the floor against concurrent Pending duplicates (two tabs,
            // mobile + desktop, slow request handler). Convert the 23505 into
            // the same friendly 409 the pre-check would have produced.
            return Results.Conflict(new { code = "request.already_pending" });
        }

        return Results.Created($"/me/grant-upgrade-requests/{request.Id}", new { id = request.Id });
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pg && pg.SqlState == "23505";

    private sealed record OwnerInboxRow(Guid RequestId, Guid GrantId, string RequesterEmail, string? Message, DateTimeOffset RequestedAt);

    private static async Task<IResult> ListForOwnerAsync(
        PotsDbContext db,
        CancellationToken cancellationToken)
    {
        // SECURITY DEFINER function joins users.email which RLS would hide.
        // The function itself verifies app_current_user_id() == patient owner.
        var rows = await db.Database
            .SqlQuery<OwnerInboxRow>($"SELECT * FROM list_pending_upgrade_requests_for_my_patient()")
            .ToListAsync(cancellationToken);
        var dtos = rows.Select(r => new GrantUpgradeRequestDto(
            r.RequestId, r.GrantId, r.RequesterEmail, r.Message, r.RequestedAt)).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> ApproveAsync(
        Guid requestId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // The owner approves. We must:
        //   (1) load the request (RLS exposes it only to owner/requester)
        //   (2) verify the caller is actually the patient owner
        //   (3) flip status to Approved AND upgrade the grant — same tx
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IResult>(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            var request = await db.GrantUpgradeRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request is null) return Results.NotFound();

            var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, cancellationToken);
            if (patient is null || patient.OwnerUserId != userId) return Results.Forbid();

            var grant = await db.PatientGrants.FirstOrDefaultAsync(g => g.Id == request.GrantId, cancellationToken);
            if (grant is null || !grant.IsActive) return Results.Conflict(new { code = "grant.not_active" });

            // Guard against approving a request whose underlying grant was
            // already moved to Editor by an out-of-band path (manual SQL,
            // future API, stale duplicate request). Approving in that state
            // would emit a misleading "grant.role_upgraded to Editor" audit
            // even though nothing changed.
            var wasViewer = grant.Role == GrantRole.Viewer;

            try
            {
                request.Approve(userId);
                grant.UpgradeToEditor();
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { code = "request.invalid", message = ex.Message });
            }

            db.AuditLog.Add(AuditLogEntry.Record(
                userId, "grant_upgrade_request.approved", nameof(GrantUpgradeRequest),
                request.Id, patient.Id, null));
            if (wasViewer)
            {
                db.AuditLog.Add(AuditLogEntry.Record(
                    userId, "grant.role_upgraded", nameof(PatientGrant),
                    grant.Id, patient.Id, "{\"to\":\"Editor\"}"));
            }
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> DenyAsync(
        Guid requestId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var request = await db.GrantUpgradeRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null) return Results.NotFound();

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, cancellationToken);
        if (patient is null || patient.OwnerUserId != userId) return Results.Forbid();

        try { request.Deny(userId); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "request.invalid", message = ex.Message }); }

        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "grant_upgrade_request.denied", nameof(GrantUpgradeRequest),
            request.Id, patient.Id, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelAsync(
        Guid requestId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var request = await db.GrantUpgradeRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null) return Results.NotFound();
        if (request.RequesterUserId != userId) return Results.Forbid();

        try { request.Cancel(userId); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "request.invalid", message = ex.Message }); }

        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "grant_upgrade_request.cancelled", nameof(GrantUpgradeRequest),
            request.Id, request.PatientId, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
