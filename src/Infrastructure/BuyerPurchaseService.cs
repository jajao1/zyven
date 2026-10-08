using Microsoft.EntityFrameworkCore;
using Zyven.Application;
namespace Zyven.Infrastructure;

public sealed class BuyerPurchaseService(ZyvenDbContext db)
{
    public async Task<IReadOnlyList<BuyerPurchaseItemResponse>> List(string email, CancellationToken ct) =>
        await (from p in db.Payments.AsNoTracking()
               join c in db.Customers.AsNoTracking() on new { p.CustomerId, p.OrganizationId } equals new { CustomerId = c.Id, c.OrganizationId }
               join e in db.Entitlements.AsNoTracking() on new { PaymentId = p.Id, p.OrganizationId } equals new { e.PaymentId, e.OrganizationId }
               join o in db.Offers.AsNoTracking() on new { p.OfferId, p.OrganizationId } equals new { OfferId = o.Id, o.OrganizationId }
               join org in db.Organizations.AsNoTracking() on p.OrganizationId equals org.Id
               where c.NormalizedEmail == email && p.Status == "PAID" && e.Status == "ACTIVE"
               orderby p.PaidAt descending
               select new BuyerPurchaseItemResponse(p.Id, o.Name, org.Name, p.Currency, p.GrossAmount.ToString("0.00"), p.PaidAt!.Value, e.Status)).ToListAsync(ct);

    public async Task<BuyerPurchaseDetailResponse?> Detail(string email, Guid paymentId, CancellationToken ct)
    {
        var purchase = await (from p in db.Payments.AsNoTracking()
                              join c in db.Customers.AsNoTracking() on new { p.CustomerId, p.OrganizationId } equals new { CustomerId = c.Id, c.OrganizationId }
                              join e in db.Entitlements.AsNoTracking() on new { PaymentId = p.Id, p.OrganizationId } equals new { e.PaymentId, e.OrganizationId }
                              join o in db.Offers.AsNoTracking() on new { p.OfferId, p.OrganizationId } equals new { OfferId = o.Id, o.OrganizationId }
                              join org in db.Organizations.AsNoTracking() on p.OrganizationId equals org.Id
                              where p.Id == paymentId && c.NormalizedEmail == email && p.Status == "PAID" && e.Status == "ACTIVE"
                              select new { p, e, o, org }).SingleOrDefaultAsync(ct);
        if (purchase is null) return null;
        var items = await db.FulfillmentExecutions.AsNoTracking().Where(x => x.OrganizationId == purchase.p.OrganizationId && x.EntitlementId == purchase.e.Id && x.Status == "COMPLETED").OrderBy(x => x.CreatedAt).Select(x => new DeliveryItemResponse(x.Id, x.Type, x.Name, x.ExternalUrl, x.CompletedAt)).ToListAsync(ct);
        return new(purchase.p.Id, purchase.o.Name, purchase.org.Name, purchase.p.Currency, purchase.p.GrossAmount.ToString("0.00"), purchase.p.PaidAt!.Value, items);
    }
}
