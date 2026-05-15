using Microsoft.EntityFrameworkCore;
using Pots.Infrastructure;
using Pots.Shared.Contracts;

namespace Pots.Api.Patients;

public static class SharedEndpoints
{
    public static void MapSharedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/shared").RequireAuthorization();
        group.MapGet("", ListAsync);
    }

    private static async Task<IResult> ListAsync(
        PotsDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQuery<SharedRow>($"SELECT * FROM list_my_shared_patients()")
            .ToListAsync(cancellationToken);
        var dtos = rows.Select(r => new SharedPatientDto(
            r.PatientId, r.PatientName, r.OwnerEmail, r.RoleName, r.GrantedAt)).ToList();
        return Results.Ok(dtos);
    }

    private sealed record SharedRow(Guid PatientId, string PatientName, string OwnerEmail, string RoleName, DateTimeOffset GrantedAt);
}
