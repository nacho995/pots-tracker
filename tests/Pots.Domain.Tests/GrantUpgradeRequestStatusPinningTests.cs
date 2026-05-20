using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

// Pins the GrantUpgradeRequestStatus enum names to the SQL literals used by:
//   - The partial unique index `ix_grant_upgrade_requests_grant_one_pending`
//     (HasFilter("status = 'Pending'"))
//   - The CASE branches in policy `grant_upgrade_requests_update`
//     ('Approved', 'Denied', 'Cancelled', 'Pending')
// Both live in migration 20260520091641_AddGrantUpgradeRequests. Renaming
// any value here without updating that migration would silently break the
// uniqueness guarantee and the WITH CHECK gate.
public sealed class GrantUpgradeRequestStatusPinningTests
{
    [Fact]
    public void Pending_StableName()
        => Assert.Equal("Pending", GrantUpgradeRequestStatus.Pending.ToString());

    [Fact]
    public void Approved_StableName()
        => Assert.Equal("Approved", GrantUpgradeRequestStatus.Approved.ToString());

    [Fact]
    public void Denied_StableName()
        => Assert.Equal("Denied", GrantUpgradeRequestStatus.Denied.ToString());

    [Fact]
    public void Cancelled_StableName()
        => Assert.Equal("Cancelled", GrantUpgradeRequestStatus.Cancelled.ToString());

    [Fact]
    public void UnexpectedStatusesDoNotExist()
    {
        var declared = Enum.GetNames<GrantUpgradeRequestStatus>();
        Array.Sort(declared);
        Assert.Equal(new[] { "Approved", "Cancelled", "Denied", "Pending" }, declared);
    }
}
