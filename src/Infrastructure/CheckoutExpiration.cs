using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace Zyven.Infrastructure;

public sealed class CheckoutExpiration(ZyvenDbContext db, TimeProvider time, ILogger<CheckoutExpiration> logger)
{
    public async Task Run(CancellationToken ct)
    {
        var count = await db.Checkouts.Where(x => x.Status == "CREATED" && x.ExpiresAt <= time.GetUtcNow()).ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, "EXPIRED"), ct);
        logger.LogInformation("Expired {CheckoutCount} checkout sessions", count);
    }
}
