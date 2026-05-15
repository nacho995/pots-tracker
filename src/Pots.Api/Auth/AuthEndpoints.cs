using Pots.Shared.Contracts;

namespace Pots.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/request-link", async (RequestLinkDto dto, AuthService auth, CancellationToken ct) =>
        {
            await auth.RequestMagicLinkAsync(dto.Email, ct);
            // Always the same response: prevents email enumeration.
            return Results.Ok(new { message = "Si el email es válido, recibirás un enlace de inicio de sesión." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-request-link");

        group.MapPost("/verify", async (VerifyDto dto, AuthService auth, CancellationToken ct) =>
        {
            var jwt = await auth.VerifyAsync(dto.Token, ct);
            return jwt is null
                ? Results.Unauthorized()
                : Results.Ok(new VerifyResponse(jwt));
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-verify");
    }
}
