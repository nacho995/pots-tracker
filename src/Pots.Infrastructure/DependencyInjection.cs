using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pots.Infrastructure.RowLevelSecurity;

namespace Pots.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPotsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Fallback so anonymous/background contexts get safe (deny-all) RLS
        // behaviour even if the host forgets to register a request-scoped
        // IUserContext. Consumers (e.g. Pots.Api) MUST override this with a
        // request-scoped implementation that reads from HttpContext claims.
        services.TryAddScoped<IUserContext, NullUserContext>();
        services.AddScoped<RlsCommandInterceptor>();

        services.AddDbContext<PotsDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<RlsCommandInterceptor>());
            // EnableSensitiveDataLogging is intentionally NOT enabled here.
            // If toggled in Development for debugging, must NEVER be enabled
            // in non-Development environments — health data is special-category GDPR.
        });

        return services;
    }
}
