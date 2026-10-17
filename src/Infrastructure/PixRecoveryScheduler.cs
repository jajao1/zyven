using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class PixRecoveryScheduler(ZyvenDbContext db, TimeProvider time)
{
    public async Task Schedule(Payment payment, CancellationToken ct) { if (await db.PixRecoveryExecutions.AnyAsync(x => x.PaymentId == payment.Id, ct)) return; var config = await db.PixRecoveryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == payment.OrganizationId && x.OfferId == payment.OfferId && x.Enabled && x.PublishedVersion > 0, ct); if (config is null) return; var first = await db.PixRecoveryMessages.AsNoTracking().Where(x => x.ConfigurationId == config.Id && x.Version == config.PublishedVersion).OrderBy(x => x.Position).FirstAsync(ct); var telegram = await db.BotCheckoutBindings.AnyAsync(x => x.CheckoutId == payment.CheckoutSessionId, ct); var now = time.GetUtcNow(); db.Add(new PixRecoveryExecution { OrganizationId = payment.OrganizationId, ConfigurationId = config.Id, PaymentId = payment.Id, Version = config.PublishedVersion, Channel = telegram ? "TELEGRAM" : "EMAIL", NextRunAt = payment.CreatedAt.AddMinutes(first.DelayMinutes), CreatedAt = now, UpdatedAt = now }); }
    public async Task CompletePaid(Guid paymentId, CancellationToken ct) { var e = await db.PixRecoveryExecutions.SingleOrDefaultAsync(x => x.PaymentId == paymentId && x.Status == "SCHEDULED", ct); if (e is null) return; e.Status = "COMPLETED_PAYMENT"; e.CompletedAt = e.UpdatedAt = time.GetUtcNow(); }
}
