namespace Pots.Infrastructure.RowLevelSecurity;

// Provides the authenticated user id for the current scope (typically an HTTP
// request). The RLS interceptor reads this and pins `app.current_user_id` on
// the Postgres session before every command. When no user is authenticated
// (anonymous endpoints, background jobs not tied to a user), CurrentUserId is
// null and RLS effectively denies access to user-data tables — which is the
// safe default.
public interface IUserContext
{
    Guid? CurrentUserId { get; }
}
