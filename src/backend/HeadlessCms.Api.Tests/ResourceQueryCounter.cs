using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HeadlessCms.Api.Tests;

public sealed class ResourceQueryCounter : DbCommandInterceptor
{
    public int Resources;
    public int Sessions;
    public int Memberships;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM \"Projects\"")) Interlocked.Increment(ref Resources);
        if (command.CommandText.Contains("FROM \"AuthSessions\"")) Interlocked.Increment(ref Sessions);
        if (command.CommandText.Contains("FROM \"WorkspaceMemberships\"")) Interlocked.Increment(ref Memberships);
        return ValueTask.FromResult(result);
    }
}
