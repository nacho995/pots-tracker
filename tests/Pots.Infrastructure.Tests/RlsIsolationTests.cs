using Microsoft.EntityFrameworkCore;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Infrastructure.Tests;

public sealed class RlsIsolationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public RlsIsolationTests(PostgresFixture fx) { _fx = fx; }

    private async Task<(Guid aliceId, Guid bobId, Guid aliceP, Guid bobP)> SeedTwoPatientsAsync()
    {
        // The PostgresFixture is shared across the test class, so emails must
        // be unique per call to avoid the unique-index collision.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var aliceEmail = $"alice-{suffix}@test.com";
        var bobEmail = $"bob-{suffix}@test.com";

        await using var admin = _fx.CreateAdminContext();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var alicePatient = Guid.NewGuid();
        var bobPatient = Guid.NewGuid();

        await admin.Database.ExecuteSqlRawAsync(
            @"SELECT set_config('app.current_user_id', {0}, false);
              INSERT INTO users (id, email, created_at) VALUES ({1}, {6}, NOW());
              INSERT INTO patients (id, owner_user_id, name, created_at, updated_at)
                VALUES ({2}, {1}, 'Alice', NOW(), NOW());
              SELECT set_config('app.current_user_id', {3}, false);
              INSERT INTO users (id, email, created_at) VALUES ({4}, {7}, NOW());
              INSERT INTO patients (id, owner_user_id, name, created_at, updated_at)
                VALUES ({5}, {4}, 'Bob', NOW(), NOW());",
            alice.ToString(), alice, alicePatient,
            bob.ToString(), bob, bobPatient,
            aliceEmail, bobEmail);

        return (alice, bob, alicePatient, bobPatient);
    }

    [Fact]
    public async Task AppContext_WithoutUserId_SeesNothing()
    {
        await SeedTwoPatientsAsync();
        await using var anonymous = _fx.CreateAppContext(actingUserId: null);
        var count = await anonymous.Patients.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AppContext_AliceSeesOnlyAlicePatient()
    {
        var (alice, _, alicePid, _) = await SeedTwoPatientsAsync();
        await using var aliceCtx = _fx.CreateAppContext(alice);
        var visible = await aliceCtx.Patients.Select(p => p.Id).ToListAsync();
        Assert.Single(visible);
        Assert.Equal(alicePid, visible[0]);
    }

    [Fact]
    public async Task AppContext_BobCannotSeeAlicePatient()
    {
        var (_, bob, alicePid, bobPid) = await SeedTwoPatientsAsync();
        await using var bobCtx = _fx.CreateAppContext(bob);
        var visible = await bobCtx.Patients.Select(p => p.Id).ToListAsync();
        Assert.Single(visible);
        Assert.Equal(bobPid, visible[0]);
        Assert.DoesNotContain(alicePid, visible);
    }

    // Note on FORCE ROW LEVEL SECURITY: it only binds table owners that are
    // NOT superusers. The Postgres docker image makes POSTGRES_USER a
    // superuser, so pots_dev bypasses RLS regardless of FORCE in this dev
    // setup. In production the migration role MUST be created without
    // SUPERUSER (e.g. owned tables + LOGIN + CREATE, no superuser bit) for
    // FORCE RLS to do its job. The relevant runtime guarantee — that
    // pots_app is fully bound by RLS — is covered by the other tests above.

    [Fact]
    public async Task PatientGrant_OwnerIssueAndGranteeReadsViaRls()
    {
        var (alice, bob, alicePid, _) = await SeedTwoPatientsAsync();

        // Alice issues a Viewer grant to Bob.
        await using (var aliceCtx = _fx.CreateAppContext(alice))
        {
            var grant = PatientGrant.Issue(alicePid, bob, GrantRole.Viewer,
                grantedByUserId: alice, patientOwnerUserId: alice);
            aliceCtx.PatientGrants.Add(grant);
            await aliceCtx.SaveChangesAsync();
        }

        // Bob can now see Alice's patient via the grant.
        await using (var bobCtx = _fx.CreateAppContext(bob))
        {
            var visible = await bobCtx.Patients.Select(p => p.Id).ToListAsync();
            Assert.Contains(alicePid, visible);
        }
    }
}
