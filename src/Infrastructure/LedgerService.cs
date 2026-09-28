using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class LedgerService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    public async Task PostCapturedPayment(Payment payment, CancellationToken ct)
    {
        if (await db.LedgerTransactions.AnyAsync(x => x.PaymentId == payment.Id, ct)) return;
        var accounts = await db.LedgerAccounts.Where(x => x.OrganizationId == payment.OrganizationId).ToDictionaryAsync(x => x.Code, ct);
        db.LedgerTransactions.Add(LedgerTransaction.Capture(payment, accounts, time.GetUtcNow()));
    }

    public async Task<WalletResponse> Wallet(Guid organizationId, Guid userId, CancellationToken ct)
    {
        await RequireFinanceAccess(organizationId, userId, ct);
        var totals = await (from entry in db.LedgerEntries.AsNoTracking()
                            join account in db.LedgerAccounts.AsNoTracking() on entry.LedgerAccountId equals account.Id
                            where entry.OrganizationId == organizationId
                            group new { entry, account } by 1 into all
                            select new
                            {
                                Available = all.Where(x => x.account.Code == LedgerAccountCodes.MerchantAvailable).Sum(x => x.entry.Credit - x.entry.Debit),
                                Received = all.Where(x => x.account.Code == LedgerAccountCodes.CelcoinClearing).Sum(x => x.entry.Debit),
                                Fees = all.Where(x => x.account.Code == LedgerAccountCodes.PlatformFeeRevenue || x.account.Code == LedgerAccountCodes.ProviderFeePayable).Sum(x => x.entry.Credit)
                            }).SingleOrDefaultAsync(ct);
        return new("BRL", Money(totals?.Available ?? 0), Money(0), Money(0), Money(totals?.Received ?? 0), Money(0), Money(totals?.Fees ?? 0));
    }

    public async Task<PageResponse<LedgerTransactionResponse>> History(Guid organizationId, Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await RequireFinanceAccess(organizationId, userId, ct);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new OrganizationException(400, "Paginação inválida.");
        var query = db.LedgerTransactions.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        var total = await query.CountAsync(ct);
        var transactions = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = transactions.Select(x => x.Id).ToArray();
        var entries = await (from entry in db.LedgerEntries.AsNoTracking()
                             join account in db.LedgerAccounts.AsNoTracking() on entry.LedgerAccountId equals account.Id
                             where entry.OrganizationId == organizationId && ids.Contains(entry.LedgerTransactionId)
                             orderby entry.CreatedAt, entry.Id
                             select new { entry.LedgerTransactionId, account.Code, entry.Debit, entry.Credit }).ToListAsync(ct);
        var byTransaction = entries.GroupBy(x => x.LedgerTransactionId).ToDictionary(x => x.Key, x => (IReadOnlyList<LedgerEntryResponse>)x.Select(e => new LedgerEntryResponse(e.Code, Money(e.Debit), Money(e.Credit))).ToList());
        var items = transactions.Select(x => new LedgerTransactionResponse(x.Id, x.PaymentId, x.Type, x.Reference, x.Currency, x.OccurredAt, byTransaction.GetValueOrDefault(x.Id) ?? [])).ToList();
        return new(items, page, pageSize, total);
    }

    private async Task RequireFinanceAccess(Guid organizationId, Guid userId, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(organizationId, userId, ct);
        if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Finance)) throw new OrganizationException(403, "Seu papel não permite acessar dados financeiros.");
    }
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
