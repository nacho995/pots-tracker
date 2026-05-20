namespace Pots.Domain.Entities;

// Persisted as string via GrantUpgradeRequestConfiguration.HasConversion<string>().
// Renaming any value is a schema change — update the migration AND any rows
// in flight together.
public enum GrantUpgradeRequestStatus
{
    Pending = 1,
    Approved = 2,
    Denied = 3,
    Cancelled = 4,
}
