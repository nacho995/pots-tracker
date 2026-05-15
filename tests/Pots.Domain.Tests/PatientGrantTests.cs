using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class PatientGrantTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Grantee = Guid.NewGuid();

    [Fact]
    public void Issue_AcceptsOwnerIssuingValidGrant()
    {
        var grant = PatientGrant.Issue(Patient, Grantee, GrantRole.Viewer, Owner, Owner);
        Assert.Equal(Patient, grant.PatientId);
        Assert.Equal(Grantee, grant.GranteeUserId);
        Assert.Equal(GrantRole.Viewer, grant.Role);
        Assert.True(grant.IsActive);
    }

    [Fact]
    public void Issue_RejectsSelfElevation_GranterNotOwner()
    {
        var attacker = Guid.NewGuid();
        var ex = Assert.Throws<DomainException>(() =>
            PatientGrant.Issue(Patient, Grantee, GrantRole.Editor,
                grantedByUserId: attacker, patientOwnerUserId: Owner));
        Assert.Contains("owner", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Issue_RejectsGrantToOwnerThemselves()
    {
        Assert.Throws<DomainException>(() =>
            PatientGrant.Issue(Patient, granteeUserId: Owner, GrantRole.Viewer, Owner, Owner));
    }

    [Theory]
    [InlineData(0)]   // not defined
    [InlineData(99)]  // not defined
    public void Issue_RejectsUndefinedRole(int roleValue)
    {
        Assert.Throws<DomainException>(() =>
            PatientGrant.Issue(Patient, Grantee, (GrantRole)roleValue, Owner, Owner));
    }

    [Fact]
    public void Revoke_RequiresRevoker()
    {
        var grant = PatientGrant.Issue(Patient, Grantee, GrantRole.Viewer, Owner, Owner);
        Assert.Throws<DomainException>(() => grant.Revoke(Guid.Empty));
    }

    [Fact]
    public void Revoke_RecordsActorAndIsIdempotent()
    {
        var grant = PatientGrant.Issue(Patient, Grantee, GrantRole.Viewer, Owner, Owner);
        grant.Revoke(Owner);
        var firstRevokedAt = grant.RevokedAt;
        Assert.NotNull(firstRevokedAt);
        Assert.Equal(Owner, grant.RevokedByUserId);

        var anotherActor = Guid.NewGuid();
        grant.Revoke(anotherActor);
        Assert.Equal(firstRevokedAt, grant.RevokedAt); // no change
        Assert.Equal(Owner, grant.RevokedByUserId);   // original revoker preserved
        Assert.False(grant.IsActive);
    }
}
