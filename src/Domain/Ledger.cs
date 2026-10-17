namespace Zyven.Domain;

public static class LedgerAccountCodes
{
    public const string PaymentProcessorClearing = "PAYMENT_PROCESSOR_CLEARING";
    public const string MerchantAvailable = "MERCHANT_AVAILABLE";
    public const string PlatformFeeRevenue = "PLATFORM_FEE_REVENUE";
    public const string ProviderFeePayable = "PROVIDER_FEE_PAYABLE";
}

public sealed class LedgerAccount
{
    private LedgerAccount() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Type { get; private set; } = "";
    public string NormalSide { get; private set; } = "";
    public string Currency { get; private set; } = "BRL";
    public DateTimeOffset CreatedAt { get; private set; }
    public static IReadOnlyList<LedgerAccount> CreateChart(Guid organizationId, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        return
        [
            New(organizationId, LedgerAccountCodes.PaymentProcessorClearing, "Payment processor clearing", "ASSET", "DEBIT", now),
            New(organizationId, LedgerAccountCodes.MerchantAvailable, "Merchant available", "LIABILITY", "CREDIT", now),
            New(organizationId, LedgerAccountCodes.PlatformFeeRevenue, "Platform fee revenue", "REVENUE", "CREDIT", now),
            New(organizationId, LedgerAccountCodes.ProviderFeePayable, "Provider fee payable", "LIABILITY", "CREDIT", now)
        ];
    }
    private static LedgerAccount New(Guid organizationId, string code, string name, string type, string side, DateTimeOffset now) => new() { OrganizationId = organizationId, Code = code, Name = name, Type = type, NormalSide = side, CreatedAt = now };
}

public sealed class LedgerTransaction
{
    private readonly List<LedgerEntry> entries = [];
    private LedgerTransaction() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string Type { get; private set; } = "PAYMENT_CAPTURED";
    public string Reference { get; private set; } = "";
    public string Currency { get; private set; } = "BRL";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<LedgerEntry> Entries => entries;

    public static LedgerTransaction Capture(Payment payment, IReadOnlyDictionary<string, LedgerAccount> accounts, DateTimeOffset now)
    {
        if (payment.Status != "PAID" || payment.PaidAt is null) throw new InvalidOperationException("Only a confirmed payment can be posted.");
        var required = new[] { LedgerAccountCodes.PaymentProcessorClearing, LedgerAccountCodes.MerchantAvailable, LedgerAccountCodes.PlatformFeeRevenue, LedgerAccountCodes.ProviderFeePayable };
        if (required.Any(code => !accounts.TryGetValue(code, out var account) || account.OrganizationId != payment.OrganizationId)) throw new InvalidOperationException("The complete organization ledger chart is required.");
        var transaction = new LedgerTransaction { OrganizationId = payment.OrganizationId, PaymentId = payment.Id, Reference = payment.ExternalReference, Currency = payment.Currency, OccurredAt = payment.PaidAt.Value, CreatedAt = now };
        transaction.entries.Add(LedgerEntry.CreateDebit(transaction, accounts[LedgerAccountCodes.PaymentProcessorClearing], payment.GrossAmount, now));
        transaction.entries.Add(LedgerEntry.CreateCredit(transaction, accounts[LedgerAccountCodes.MerchantAvailable], payment.NetAmount, now));
        if (payment.PlatformFee > 0) transaction.entries.Add(LedgerEntry.CreateCredit(transaction, accounts[LedgerAccountCodes.PlatformFeeRevenue], payment.PlatformFee, now));
        if (payment.ProviderFee > 0) transaction.entries.Add(LedgerEntry.CreateCredit(transaction, accounts[LedgerAccountCodes.ProviderFeePayable], payment.ProviderFee, now));
        if (transaction.entries.Sum(x => x.Debit) != transaction.entries.Sum(x => x.Credit)) throw new InvalidOperationException("Ledger transaction must balance.");
        return transaction;
    }
}

public sealed class LedgerEntry
{
    private LedgerEntry() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid LedgerTransactionId { get; private set; }
    public Guid LedgerAccountId { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public DateTimeOffset CreatedAt { get; private set; }
    internal static LedgerEntry CreateDebit(LedgerTransaction transaction, LedgerAccount account, decimal amount, DateTimeOffset now) => New(transaction, account, amount, 0, now);
    internal static LedgerEntry CreateCredit(LedgerTransaction transaction, LedgerAccount account, decimal amount, DateTimeOffset now) => New(transaction, account, 0, amount, now);
    private static LedgerEntry New(LedgerTransaction transaction, LedgerAccount account, decimal debit, decimal credit, DateTimeOffset now)
    {
        if (account.OrganizationId != transaction.OrganizationId || debit < 0 || credit < 0 || (debit == 0) == (credit == 0) || !PaymentMoney.IsValid(debit) || !PaymentMoney.IsValid(credit)) throw new InvalidOperationException("A ledger entry must have one valid side in the transaction organization.");
        return new() { OrganizationId = transaction.OrganizationId, LedgerTransactionId = transaction.Id, LedgerAccountId = account.Id, Debit = debit, Credit = credit, Currency = transaction.Currency, CreatedAt = now };
    }
}
