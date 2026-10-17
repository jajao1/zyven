namespace Zyven.Application;

public sealed record WalletResponse(
    string Currency,
    string AvailableBalance,
    string PendingBalance,
    string ReservedBalance,
    string TotalReceived,
    string TotalWithdrawn,
    string TotalFees);

public sealed record LedgerEntryResponse(string AccountCode, string Debit, string Credit);

public sealed record LedgerTransactionResponse(
    Guid Id,
    Guid PaymentId,
    string Type,
    string Reference,
    string Currency,
    DateTimeOffset OccurredAt,
    IReadOnlyList<LedgerEntryResponse> Entries);
