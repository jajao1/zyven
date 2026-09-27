using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace Zyven.Infrastructure;

public sealed class SessionCleanup(ZyvenDbContext db, TimeProvider clock, ILogger<SessionCleanup>? logger = null)
{
    // Retain all consumed hashes until absolute family expiry so replay remains detectable.
    public async Task<int> Run(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var count = await db.Sessions.Where(x => x.ExpiresAt <= clock.GetUtcNow()).ExecuteDeleteAsync(ct);
        logger?.LogInformation("Expired session cleanup completed: {DeletedSessions} families in {ElapsedMilliseconds} ms", count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return count;
    }
}
