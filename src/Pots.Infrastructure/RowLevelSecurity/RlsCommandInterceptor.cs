using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pots.Infrastructure.RowLevelSecurity;

// Pins the Postgres session variable `app.current_user_id` before every command
// issued through EF. The RLS policies (migration EnableRowLevelSecurity) read
// it via app_current_user_id() to authorize row visibility and mutation.
//
// Why this many overrides: EF dispatches Reader/NonQuery/Scalar separately,
// and each has sync + async overloads — six entry points total. Collapsing
// them is not possible without breaking interception.
//
// Why prepend-via-separate-command instead of merging into the original command
// text: prepending into a multi-statement batch makes EF's reader consume the
// SET result set as the query result. We pay an extra round-trip per command
// in exchange for correctness.
//
// On connection pooling: Npgsql's default pool-release behaviour runs
// `DISCARD ALL`, which clears session settings including `app.current_user_id`.
// Combined with re-pinning on every command, a returned-to-pool connection
// cannot leak a stale uid to the next request.
//
// On the empty-string marker: CurrentUserId == null → '' on the parameter →
// NULLIF in app_current_user_id() → NULL → every RLS comparison `= NULL`
// is false → no rows visible. SECURITY: do NOT "normalize" this to a zero
// Guid; the empty/NULL mapping is the anonymous denial contract.
public sealed class RlsCommandInterceptor : DbCommandInterceptor
{
    private readonly IUserContext _userContext;

    public RlsCommandInterceptor(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SetUserContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await SetUserContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SetUserContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await SetUserContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        SetUserContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await SetUserContextAsync(command, cancellationToken);
        return result;
    }

    private void SetUserContext(DbCommand command)
    {
        var connection = RequireConnection(command);
        if (connection.State != ConnectionState.Open) connection.Open();
        using var setCommand = CreateSetCommand(command, connection);
        setCommand.ExecuteNonQuery();
    }

    private async ValueTask SetUserContextAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var connection = RequireConnection(command);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var setCommand = CreateSetCommand(command, connection);
        await setCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbConnection RequireConnection(DbCommand command)
        => command.Connection
            ?? throw new InvalidOperationException(
                "RlsCommandInterceptor: DbCommand has no associated connection. " +
                "This indicates an EF diagnostic path that bypasses the normal " +
                "connection lifecycle; refusing to proceed to avoid leaking a " +
                "stale user_id pin into a pooled connection.");

    private DbCommand CreateSetCommand(DbCommand command, DbConnection connection)
    {
        var setCommand = connection.CreateCommand();
        setCommand.Transaction = command.Transaction;
        setCommand.CommandText = "SELECT set_config('app.current_user_id', @uid, false)";
        var p = setCommand.CreateParameter();
        p.ParameterName = "uid";
        p.Value = _userContext.CurrentUserId?.ToString() ?? string.Empty;
        setCommand.Parameters.Add(p);
        return setCommand;
    }
}
