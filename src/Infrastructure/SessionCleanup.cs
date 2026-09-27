using Microsoft.EntityFrameworkCore;
namespace Zyven.Infrastructure;

public sealed class SessionCleanup(ZyvenDbContext db, TimeProvider clock)
{
    // Retain all consumed hashes until absolute family expiry so replay remains detectable.
    public Task<int> Run(CancellationToken ct) => db.Sessions.Where(x => x.ExpiresAt <= clock.GetUtcNow()).ExecuteDeleteAsync(ct);
}
