using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pots.Domain;
using Pots.Domain.Entities;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

// Caregiver notes — short text messages visible to everyone with access to
// a patient (the patient herself plus active grantees). Designed for
// observations like "Hoy te he visto pálida" without polluting the
// clinical data tables.
//
// Endpoints:
//   POST   /patients/{patientId}/caregiver-notes          create
//   GET    /patients/{patientId}/caregiver-notes          list (excludes deleted)
//   DELETE /patients/{patientId}/caregiver-notes/{noteId} soft-delete (author or patient owner)
//
// Access control is layered: RLS at the DB floor + an explicit
// `has_patient_access` SECURITY DEFINER check inside the list function.
// Soft-delete authorisation (author OR patient owner) is enforced at the
// app layer; the RLS UPDATE policy is a defensive duplicate.
public static class CaregiverNoteEndpoints
{
    public static void MapCaregiverNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/patients/{patientId:guid}/caregiver-notes").RequireAuthorization();
        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapDelete("/{noteId:guid}", DeleteAsync);
    }

    private static async Task<IResult> CreateAsync(
        Guid patientId,
        [FromBody] CreateCaregiverNoteDto dto,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // Existence + access in one query. RLS hides patients we cannot see;
        // an empty `Patients.AnyAsync` is indistinguishable from 404 by design.
        var patientAccessible = await db.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientAccessible) return Results.NotFound();

        CaregiverNote note;
        try { note = CaregiverNote.Create(patientId, userId, dto.Body); }
        catch (DomainException ex) { return Results.BadRequest(new { code = "note.invalid", message = ex.Message }); }

        db.CaregiverNotes.Add(note);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "caregiver_note.created", nameof(CaregiverNote),
            note.Id, patientId, null));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/patients/{patientId}/caregiver-notes/{note.Id}", new { id = note.Id });
    }

    private sealed record NoteRow(
        Guid NoteId, Guid AuthorUserId, string AuthorEmail, string Body,
        DateTimeOffset CreatedAt, bool IsDeleted, bool CallerIsOwner);

    private static async Task<IResult> ListAsync(
        Guid patientId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken,
        [FromQuery] int take = 100)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        // SECURITY DEFINER fn returns:
        //   - rows the caller can see (anti-enumeration: empty if no access)
        //   - the caller_is_owner flag computed once inside the function,
        //     eliminating the second round-trip the previous shape required
        //   - the take cap is clamped inside the function (1..500)
        // RLS on users would hide cross-user emails for SELECT, which is
        // exactly why we use SECURITY DEFINER here.
        var rows = await db.Database
            .SqlQuery<NoteRow>($"SELECT * FROM list_caregiver_notes({patientId}, {take})")
            .ToListAsync(cancellationToken);

        // Audit only when there's something to disclose AND the caller is
        // not the owner — owner self-reads would flood the log. Pattern
        // mirrors SharedPatientEndpoints. If `rows` is empty (no access or
        // empty thread), nothing is logged.
        if (rows.Count > 0 && !rows[0].CallerIsOwner)
        {
            db.AuditLog.Add(AuditLogEntry.Record(
                userId, "caregiver_note.read_by_grantee", nameof(CaregiverNote),
                patientId, patientId,
                $"{{\"count\":{rows.Count}}}"));
            await db.SaveChangesAsync(cancellationToken);
        }

        var dtos = rows.Select(r => new CaregiverNoteDto(
            r.NoteId,
            r.AuthorUserId,
            r.AuthorEmail,
            r.Body,
            r.CreatedAt,
            CanDelete: r.AuthorUserId == userId || r.CallerIsOwner)).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> DeleteAsync(
        Guid patientId,
        Guid noteId,
        PotsDbContext db,
        IUserContext ctx,
        CancellationToken cancellationToken)
    {
        var userId = ctx.CurrentUserId
            ?? throw new InvalidOperationException("Authenticated endpoint without user id.");

        var note = await db.CaregiverNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.PatientId == patientId, cancellationToken);
        if (note is null || note.IsDeleted) return Results.NotFound();

        // Author OR patient owner can soft-delete. Other grantees cannot.
        var isOwner = await db.Patients
            .AnyAsync(p => p.Id == patientId && p.OwnerUserId == userId, cancellationToken);
        if (note.AuthorUserId != userId && !isOwner) return Results.Forbid();

        note.SoftDelete(userId);
        db.AuditLog.Add(AuditLogEntry.Record(
            userId, "caregiver_note.deleted", nameof(CaregiverNote),
            note.Id, patientId, null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
