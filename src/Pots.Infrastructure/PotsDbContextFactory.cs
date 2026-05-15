using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pots.Infrastructure;

public sealed class PotsDbContextFactory : IDesignTimeDbContextFactory<PotsDbContext>
{
    public PotsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POTS_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "POTS_CONNECTION_STRING environment variable is not set. " +
                "Required when running EF Core design-time commands. " +
                "See README for local dev setup. " +
                "NEVER use production credentials in this variable; treat it as machine-local.");

        var options = new DbContextOptionsBuilder<PotsDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PotsDbContext(options);
    }
}
