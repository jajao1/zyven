using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;

namespace Zyven.Infrastructure;

public sealed class SalesService(ZyvenDbContext db, TenantAuthorization tenants, LedgerService ledger)
{
    private static readonly string[] Statuses = ["PENDING", "PROCESSING", "PAID", "EXPIRED", "FAILED", "CANCELLED", "REFUNDED", "CHARGEBACK"];

    public async Task<SalesSummaryResponse> Summary(Guid organizationId, Guid userId, CancellationToken ct)
    {
        await RequireFinanceAccess(organizationId, userId, ct);
        var wallet = await ledger.Wallet(organizationId, userId, ct);
        var aggregate = await db.Payments.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .GroupBy(_ => 1).Select(all => new
            {
                Total = all.Count(),
                Paid = all.Count(x => x.Status == "PAID"),
                Pending = all.Count(x => x.Status == "PENDING" || x.Status == "PROCESSING"),
                Expired = all.Count(x => x.Status == "EXPIRED"),
                Failed = all.Count(x => x.Status == "FAILED" || x.Status == "CANCELLED" || x.Status == "REFUNDED" || x.Status == "CHARGEBACK"),
                NetPaid = all.Where(x => x.Status == "PAID").Sum(x => x.NetAmount)
            }).SingleOrDefaultAsync(ct);
        return new(wallet.Currency, wallet.AvailableBalance, wallet.TotalReceived, wallet.TotalFees, Money(aggregate?.NetPaid ?? 0), aggregate?.Total ?? 0, aggregate?.Paid ?? 0, aggregate?.Pending ?? 0, aggregate?.Expired ?? 0, aggregate?.Failed ?? 0);
    }

    public async Task<PageResponse<SaleListItemResponse>> List(Guid organizationId, Guid userId, int page, int pageSize, string? status, CancellationToken ct)
    {
        await RequireFinanceAccess(organizationId, userId, ct);
        var offset = ((long)page - 1) * pageSize;
        if (page < 1 || pageSize is < 1 or > 100 || offset > int.MaxValue) throw new OrganizationException(400, "Paginação inválida.");
        var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (normalized is not null && !Statuses.Contains(normalized)) throw new OrganizationException(400, "Status de pagamento inválido.");
        var query = from payment in db.Payments.AsNoTracking()
                    join customer in db.Customers.AsNoTracking() on new { payment.CustomerId, payment.OrganizationId } equals new { CustomerId = customer.Id, customer.OrganizationId }
                    join offer in db.Offers.AsNoTracking() on new { payment.OfferId, payment.OrganizationId } equals new { OfferId = offer.Id, offer.OrganizationId }
                    where payment.OrganizationId == organizationId && (normalized == null || payment.Status == normalized)
                    select new { payment, customer, offer };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.payment.PaidAt).ThenByDescending(x => x.payment.CreatedAt).ThenByDescending(x => x.payment.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(x => new SaleListItemResponse(x.payment.Id, x.payment.Status, x.customer.Name, x.customer.Email, x.offer.Name, x.payment.PaymentMethod, x.payment.Provider, x.payment.Currency, Money(x.payment.GrossAmount), Money(x.payment.PlatformFee), Money(x.payment.ProviderFee), Money(x.payment.NetAmount), x.payment.CreatedAt, x.payment.PaidAt)).ToList();
        return new(items, page, pageSize, total);
    }

    public async Task<SaleDetailResponse> Detail(Guid organizationId, Guid paymentId, Guid userId, CancellationToken ct)
    {
        await RequireFinanceAccess(organizationId, userId, ct);
        var row = await (from payment in db.Payments.AsNoTracking()
                         join customer in db.Customers.AsNoTracking() on new { payment.CustomerId, payment.OrganizationId } equals new { CustomerId = customer.Id, customer.OrganizationId }
                         join offer in db.Offers.AsNoTracking() on new { payment.OfferId, payment.OrganizationId } equals new { OfferId = offer.Id, offer.OrganizationId }
                         where payment.OrganizationId == organizationId && payment.Id == paymentId
                         select new { payment, customer, offer }).SingleOrDefaultAsync(ct) ?? throw new OrganizationException(404, "Venda não encontrada.");
        var entitlement = await db.Entitlements.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.PaymentId == paymentId).Select(x => new { x.Id, x.Status }).SingleOrDefaultAsync(ct);
        var fulfillment = entitlement is null ? null : await db.FulfillmentExecutions.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.EntitlementId == entitlement.Id).Select(x => x.Status).FirstOrDefaultAsync(ct);
        var p = row.payment;
        return new(p.Id, p.CheckoutSessionId, p.CustomerId, p.OfferId, p.Status, row.customer.Name, row.customer.Email, row.offer.Name, p.PaymentMethod, p.Provider, p.Currency, Money(p.GrossAmount), Money(p.PlatformFee), Money(p.ProviderFee), Money(p.NetAmount), p.ExternalReference, p.ProviderTransactionId, p.EndToEndId, p.CreatedAt, p.ExpiresAt, p.PaidAt, entitlement?.Status, fulfillment);
    }

    private async Task RequireFinanceAccess(Guid organizationId, Guid userId, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(organizationId, userId, ct);
        if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Finance)) throw new OrganizationException(403, "Seu papel não permite acessar dados financeiros.");
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
