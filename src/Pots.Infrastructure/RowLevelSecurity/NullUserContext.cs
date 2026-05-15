namespace Pots.Infrastructure.RowLevelSecurity;

// Default implementation used during design-time (EF tooling) and any other
// path where no HTTP request scope provides an identity. RLS will reject all
// user-data reads/writes — that's the secure-by-default behaviour.
public sealed class NullUserContext : IUserContext
{
    public Guid? CurrentUserId => null;
}
