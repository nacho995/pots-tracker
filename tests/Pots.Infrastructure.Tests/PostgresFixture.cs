using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pots.Infrastructure;
using Pots.Infrastructure.RowLevelSecurity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pots.Infrastructure.Tests;

// Class fixture: starts a Postgres container once, creates the pots_app role,
// applies all EF migrations as pots_dev, and exposes both connection strings.
// Test classes inject this fixture via IClassFixture<PostgresFixture>.
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AdminUser = "pots_dev";
    private const string AdminPassword = "pots_dev_test";
    private const string AppUser = "pots_app";
    private const string AppPassword = "pots_app_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pots")
        .WithUsername(AdminUser)
        .WithPassword(AdminPassword)
        .Build();

    public string AdminConnectionString { get; private set; } = null!;
    public string AppConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();

        // Create the app role. We do this manually (Testcontainers doesn't
        // run our /docker-init scripts) so the migration's GRANT statements
        // have a real target.
        await using (var conn = new NpgsqlConnection(AdminConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                CREATE ROLE {AppUser} LOGIN PASSWORD '{AppPassword}';
                GRANT CONNECT ON DATABASE pots TO {AppUser};
                GRANT USAGE ON SCHEMA public TO {AppUser};";
            await cmd.ExecuteNonQueryAsync();
        }

        AppConnectionString = AdminConnectionString
            .Replace($"Username={AdminUser}", $"Username={AppUser}")
            .Replace($"Password={AdminPassword}", $"Password={AppPassword}");

        // Apply migrations as the admin role.
        var adminOptions = new DbContextOptionsBuilder<PotsDbContext>()
            .UseNpgsql(AdminConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var migrate = new PotsDbContext(adminOptions);
        await migrate.Database.MigrateAsync();
    }

    public PotsDbContext CreateAdminContext()
    {
        var options = new DbContextOptionsBuilder<PotsDbContext>()
            .UseNpgsql(AdminConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PotsDbContext(options);
    }

    public PotsDbContext CreateAppContext(Guid? actingUserId)
    {
        var userContext = new FixedUserContext(actingUserId);
        var interceptor = new RlsCommandInterceptor(userContext);
        var options = new DbContextOptionsBuilder<PotsDbContext>()
            .UseNpgsql(AppConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;
        return new PotsDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private sealed class FixedUserContext : IUserContext
    {
        public FixedUserContext(Guid? id) { CurrentUserId = id; }
        public Guid? CurrentUserId { get; }
    }
}
