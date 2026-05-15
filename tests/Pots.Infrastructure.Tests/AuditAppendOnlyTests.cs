using Microsoft.EntityFrameworkCore;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Infrastructure.Tests;

public sealed class AuditAppendOnlyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public AuditAppendOnlyTests(PostgresFixture fx) { _fx = fx; }

    [Fact]
    public async Task DbContext_RejectsAuditModifiedState()
    {
        var actor = Guid.NewGuid();
        var email = $"audittest-{Guid.NewGuid():N}@test.com";
        await using (var admin = _fx.CreateAdminContext())
        {
            await admin.Database.ExecuteSqlRawAsync(
                @"SELECT set_config('app.current_user_id', {0}, false);
                  INSERT INTO users (id, email, created_at) VALUES ({1}, {2}, NOW());
                  INSERT INTO audit_log (id, actor_user_id, action, entity_type, created_at)
                    VALUES (gen_random_uuid(), {1}, 'seed', 'Test', NOW());",
                actor.ToString(), actor, email);
        }

        await using var ctx = _fx.CreateAppContext(actor);
        var entry = await ctx.AuditLog.FirstAsync();
        // Trying to "rewrite history" via EF must fail at SaveChanges.
        ctx.Entry(entry).Property("Action").CurrentValue = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task DbContext_RejectsAuditDeletedState()
    {
        var actor = Guid.NewGuid();
        var email = $"audittest-{Guid.NewGuid():N}@test.com";
        await using (var admin = _fx.CreateAdminContext())
        {
            await admin.Database.ExecuteSqlRawAsync(
                @"SELECT set_config('app.current_user_id', {0}, false);
                  INSERT INTO users (id, email, created_at) VALUES ({1}, {2}, NOW());
                  INSERT INTO audit_log (id, actor_user_id, action, entity_type, created_at)
                    VALUES (gen_random_uuid(), {1}, 'seed', 'Test', NOW());",
                actor.ToString(), actor, email);
        }

        await using var ctx = _fx.CreateAppContext(actor);
        var entry = await ctx.AuditLog.FirstAsync();
        ctx.AuditLog.Remove(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task DbTrigger_RejectsRawUpdate_EvenForAdmin()
    {
        var actor = Guid.NewGuid();
        var email = $"audittest-{Guid.NewGuid():N}@test.com";
        await using var admin = _fx.CreateAdminContext();
        await admin.Database.ExecuteSqlRawAsync(
            @"SELECT set_config('app.current_user_id', {0}, false);
              INSERT INTO users (id, email, created_at) VALUES ({1}, {2}, NOW());
              INSERT INTO audit_log (id, actor_user_id, action, entity_type, created_at)
                VALUES (gen_random_uuid(), {1}, 'seed', 'Test', NOW());",
            actor.ToString(), actor, email);

        // The DB-level trigger fires for ANY UPDATE on audit_log, including
        // the table owner (pots_dev). This is the floor under the EF guard.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            await admin.Database.ExecuteSqlRawAsync("UPDATE audit_log SET action = 'tampered'"));
    }
}
