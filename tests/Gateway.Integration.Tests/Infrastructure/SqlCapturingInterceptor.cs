using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gateway.Integration.Tests.Infrastructure;

/// <summary>
/// Records the SQL EF actually sends.
/// <para>
/// Asserting on results alone would not notice a query that quietly stopped filtering on the
/// discriminator, or one that fell back to client evaluation - both return the right answer on a
/// seed this small and the wrong one, or none at all, on a real table.
/// </para>
/// <para>
/// An interceptor rather than LogTo: the log templates carry an elapsed time and a local
/// timestamp, so anything asserted against them is flaky by construction.
/// </para>
/// </summary>
internal sealed class SqlCapturingInterceptor : DbCommandInterceptor
{
    /// <summary>
    /// Gets the command text of every reader executed through this context.
    /// </summary>
    public List<string> Commands { get; } = [];

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Commands.Add(command.CommandText);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        // The async overload is the one that fires: the query service is async throughout.
        Commands.Add(command.CommandText);

        return ValueTask.FromResult(result);
    }
}
