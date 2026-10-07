using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;

namespace Zyven.Infrastructure;

public enum WebhookApplyResult { Applied, Duplicate, Ignored, Rejected }

public sealed class PushinPayWebhookService(ZyvenDbContext db, IPaymentProcessor provider, PushinPayCredentialVault credentialVault, LedgerService ledger, FulfillmentService fulfillment, TimeProvider time)
{
    public async Task<WebhookApplyResult> Apply(Guid merchantId, string secret, byte[] payload, CancellationToken ct)
    {
        if (secret.Length != 64) return WebhookApplyResult.Ignored;
        using var json = JsonDocument.Parse(payload, new() { MaxDepth = 24 });
        var root = json.RootElement;
        var providerId = Text(root, "id");
        var status = Text(root, "status")?.Trim().ToLowerInvariant();
        var callbackEndToEnd = Text(root, "end_to_end_id");
        var callbackCents = Cents(root, "value");
        if (string.IsNullOrWhiteSpace(providerId) || status != "paid" || string.IsNullOrWhiteSpace(callbackEndToEnd) || callbackCents is null)
            return WebhookApplyResult.Ignored;

        var callbackHash = PushinPayCredentialVault.HashCallbackSecret(secret);
        var merchant = await db.MerchantAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == merchantId && x.Provider == "PUSHINPAY" && x.Status == "ACTIVE" && x.CallbackSecretHash == callbackHash, ct);
        if (merchant is null) return WebhookApplyResult.Ignored;

        var identity = $"{merchant.Id:N}:{providerId}:{status}:{callbackEndToEnd}";
        var externalEventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lockKey = "PUSHINPAY:" + externalEventId;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
        var existingEvent = await db.PaymentWebhookEvents.SingleOrDefaultAsync(x => x.Provider == "PUSHINPAY" && x.ExternalEventId == externalEventId, ct);
        if (existingEvent?.Status == "APPLIED")
        {
            await transaction.CommitAsync(ct);
            return WebhookApplyResult.Duplicate;
        }

        var evt = existingEvent ?? new PaymentWebhookEvent { Provider = "PUSHINPAY", ExternalEventId = externalEventId, EventType = "pix.paid", ReceivedAt = time.GetUtcNow() };
        evt.PayloadHash = Convert.ToHexString(SHA256.HashData(payload));
        if (existingEvent is null) db.PaymentWebhookEvents.Add(evt);
        var payment = await db.Payments.FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"MerchantAccountId\" = {merchant.Id} AND \"Provider\" = 'PUSHINPAY' AND \"ProviderTransactionId\" = {providerId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (payment is null) return await Finish(evt, WebhookApplyResult.Ignored, transaction, ct);
        evt.PaymentId = payment.Id;

        var token = credentialVault.Decrypt(new(merchant.CredentialCiphertext!, merchant.CredentialNonce!, merchant.CredentialTag!, merchant.CredentialFingerprint!));
        var lookup = await provider.QueryAsync(new(merchant.Id, "PUSHINPAY", payment.ExternalReference, providerId, token), ct);
        var state = lookup.State;
        var callbackAmount = callbackCents.Value / 100m;
        if (lookup.Error is not null || state is null || state.Status != "PAID" || state.ProviderTransactionId != providerId ||
            state.GrossAmount != payment.GrossAmount || callbackAmount != payment.GrossAmount || string.IsNullOrWhiteSpace(state.EndToEndId) ||
            !string.Equals(state.EndToEndId, callbackEndToEnd, StringComparison.Ordinal))
            return await Finish(evt, WebhookApplyResult.Rejected, transaction, ct);

        try
        {
            payment.ConfirmPaid(state.EndToEndId, state.GrossAmount, state.PaidAt ?? time.GetUtcNow());
            var checkout = await db.Checkouts.SingleAsync(x => x.Id == payment.CheckoutSessionId && x.OrganizationId == payment.OrganizationId, ct);
            checkout.Status = "COMPLETED";
            await ledger.PostCapturedPayment(payment, ct);
            await fulfillment.FulfillPaidPayment(payment, ct);
            return await Finish(evt, WebhookApplyResult.Applied, transaction, ct);
        }
        catch (InvalidOperationException)
        {
            return await Finish(evt, WebhookApplyResult.Rejected, transaction, ct);
        }
    }

    private async Task<WebhookApplyResult> Finish(PaymentWebhookEvent evt, WebhookApplyResult result, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken ct)
    {
        evt.Status = result.ToString().ToUpperInvariant();
        evt.ProcessedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static string? Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null;
    private static int? Cents(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : null;
    }
}
