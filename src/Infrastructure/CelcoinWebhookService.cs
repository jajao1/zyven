using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public enum WebhookApplyResult { Applied, Duplicate, Ignored, Rejected }

public sealed class CelcoinWebhookService(ZyvenDbContext db, TimeProvider time)
{
    public async Task<WebhookApplyResult> Apply(byte[] payload, CancellationToken ct)
    {
        using var json = JsonDocument.Parse(payload, new() { MaxDepth = 24 }); var root = json.RootElement;
        var body = Property(root, "RequestBody", "body");
        var eventId = Text(root, "webhookId") ?? Text(body, "id");
        var entity = Text(root, "entity") ?? (Property(root, "RequestBody").ValueKind != JsonValueKind.Undefined ? "pix-payment-in" : null);
        if (string.IsNullOrWhiteSpace(eventId) || entity != "pix-payment-in") return WebhookApplyResult.Ignored;
        if (await db.PaymentWebhookEvents.AnyAsync(x => x.Provider == "CELCOIN" && x.ExternalEventId == eventId, ct)) return WebhookApplyResult.Duplicate;
        var evt = new PaymentWebhookEvent { ExternalEventId = eventId, EventType = entity, PayloadHash = Convert.ToHexString(SHA256.HashData(payload)), ReceivedAt = time.GetUtcNow() };
        db.PaymentWebhookEvents.Add(evt);
        var reference = Text(body, "ClientRequestId", "clientRequestId");
        var transactionId = Text(body, "transactionIdBRCode", "TransactionId", "transactionId");
        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(reference)) payment = await db.Payments.SingleOrDefaultAsync(x => x.ExternalReference == reference, ct);
        if (payment is null) { evt.Status = "IGNORED"; evt.ProcessedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return WebhookApplyResult.Ignored; }
        evt.PaymentId = payment.Id;
        var amount = Decimal(body, "Amount", "amount"); var endToEnd = Text(body, "EndToEndId", "endToEndId");
        try
        {
            if (payment.ProviderTransactionId is not null && transactionId != payment.ProviderTransactionId) throw new InvalidOperationException("Provider transaction does not match.");
            payment.ConfirmPaid(endToEnd ?? "", amount ?? -1m, time.GetUtcNow());
            var checkout = await db.Checkouts.SingleAsync(x => x.Id == payment.CheckoutSessionId && x.OrganizationId == payment.OrganizationId, ct); checkout.Status = "COMPLETED";
            evt.Status = "APPLIED"; evt.ProcessedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return WebhookApplyResult.Applied;
        }
        catch (InvalidOperationException) { evt.Status = "REJECTED"; evt.ProcessedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return WebhookApplyResult.Rejected; }
    }

    private static JsonElement Property(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) return default;
        foreach (var property in item.EnumerateObject()) if (names.Any(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase))) return property.Value;
        return default;
    }
    private static string? Text(JsonElement item, params string[] names) { var value = Property(item, names); return value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null; }
    private static decimal? Decimal(JsonElement item, params string[] names) { var value = Property(item, names); return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null; }
}
