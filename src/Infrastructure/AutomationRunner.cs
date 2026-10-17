using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class AutomationRunner(ZyvenDbContext db, TelegramCredentialVault vault, ITelegramClient telegram, TelegramOptions options, BotCheckoutReferenceService botReferences, TimeProvider time)
{
    private sealed record StepConfig(string? Text, string? Url, int? WaitMinutes, string? Condition);

    public async Task<bool> Start(Guid entitlementId, long telegramUserId, CancellationToken ct)
    {
        var entitlement = await db.Entitlements.AsNoTracking().SingleAsync(x => x.Id == entitlementId, ct);
        var automation = await db.Automations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == entitlement.OrganizationId && x.OfferId == entitlement.OfferId && x.PublishedVersion > 0, ct);
        if (automation is null) return false;
        var execution = await db.AutomationExecutions.SingleOrDefaultAsync(x => x.EntitlementId == entitlementId && x.AutomationId == automation.Id && x.Version == automation.PublishedVersion, ct);
        if (execution is null)
        {
            var now = time.GetUtcNow(); execution = new() { OrganizationId = entitlement.OrganizationId, AutomationId = automation.Id, EntitlementId = entitlementId, Version = automation.PublishedVersion, TelegramUserId = telegramUserId, CreatedAt = now, UpdatedAt = now }; db.AutomationExecutions.Add(execution); await db.SaveChangesAsync(ct);
        }
        if (execution.TelegramUserId != telegramUserId || execution.Status == "COMPLETED") return true;
        await Run(execution, ct); return true;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ResumeDue(CancellationToken ct)
    {
        var ids = await db.AutomationExecutions.AsNoTracking().Where(x => x.Status == "WAITING" && x.NextRunAt <= time.GetUtcNow()).OrderBy(x => x.NextRunAt).Select(x => x.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids) { var execution = await db.AutomationExecutions.SingleAsync(x => x.Id == id, ct); execution.Status = "RUNNING"; execution.NextRunAt = null; execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); await Run(execution, ct); }
    }

    private async Task Run(AutomationExecution execution, CancellationToken ct)
    {
        var context = await (from e in db.Entitlements.AsNoTracking() join c in db.Customers.AsNoTracking() on new { e.CustomerId, e.OrganizationId } equals new { CustomerId = c.Id, c.OrganizationId } join o in db.Offers.AsNoTracking() on new { e.OfferId, e.OrganizationId } equals new { OfferId = o.Id, o.OrganizationId } join i in db.TelegramIntegrations.AsNoTracking() on e.OrganizationId equals i.OrganizationId where e.Id == execution.EntitlementId select new { Entitlement = e, Customer = c, Offer = o, Integration = i }).SingleAsync(ct);
        var fulfillment = await db.FulfillmentExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.EntitlementId == execution.EntitlementId && x.Type == "TELEGRAM_ACCESS", ct);
        var token = vault.Decrypt(new(context.Integration.TokenCiphertext, context.Integration.TokenNonce, context.Integration.TokenTag, context.Integration.TokenFingerprint));
        var steps = await db.AutomationSteps.AsNoTracking().Where(x => x.AutomationId == execution.AutomationId && x.Version == execution.Version).OrderBy(x => x.Position).ToListAsync(ct);
        for (var count = 0; count < 30 && execution.CurrentPosition < steps.Count; count++)
        {
            var step = steps[execution.CurrentPosition]; var config = JsonSerializer.Deserialize<StepConfig>(step.ConfigurationJson)!;
            if (step.Type == "WAIT") { execution.CurrentPosition++; execution.Status = "WAITING"; execution.NextRunAt = time.GetUtcNow().AddMinutes(config.WaitMinutes!.Value); execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return; }
            if (step.Type == "CONDITION" && ((config.Condition == "PAYMENT_CONFIRMED" && context.Entitlement.Status != "ACTIVE") || (config.Condition == "ENTITLEMENT_ACTIVE" && context.Entitlement.Status != "ACTIVE"))) { execution.Status = "COMPLETED"; execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return; }
            if (step.Type == "END") { execution.CurrentPosition++; execution.Status = "COMPLETED"; execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return; }
            // Keep the cursor on the current side effect until Telegram confirms it. A transient
            // failure is resumed by the worker instead of consuming the buyer's one-time webhook.
            execution.Status = "WAITING"; execution.NextRunAt = time.GetUtcNow().AddMinutes(1); execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct);
            if (step.Type == "SEND_TELEGRAM_MESSAGE") await telegram.SendMessage(token, execution.TelegramUserId, Expand(config.Text!, context.Customer.Name, context.Customer.Email, context.Offer.Name, OfferUrl(config.Url, context.Offer.Slug)), ct);
            else if (step.Type is "SEND_OFFER" or "CHECKOUT") { var url = OfferUrl(config.Url, context.Offer.Slug); if (string.IsNullOrWhiteSpace(config.Url)) { var reference = await botReferences.Create(execution.OrganizationId, context.Offer.Id, context.Integration.Id, execution.TelegramUserId, ct); url = $"{url}?r={reference}"; } await telegram.SendMessage(token, execution.TelegramUserId, step.Type == "SEND_OFFER" ? $"Conheça a oferta {context.Offer.Name}:\n{url}" : $"Continue para o checkout:\n{url}", ct); }
            else if (step.Type == "TELEGRAM_ACCESS" && fulfillment is not null) { var invite = await telegram.CreateInvite(token, fulfillment.TelegramChatId, time.GetUtcNow().AddMinutes(15), ct); await telegram.SendMessage(token, execution.TelegramUserId, $"Seu acesso está liberado. Este convite é individual e expira em 15 minutos:\n{invite}", ct); }
            execution.CurrentPosition++; execution.Status = "RUNNING"; execution.NextRunAt = null; execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct);
        }
        if (execution.CurrentPosition >= steps.Count) { execution.Status = "COMPLETED"; execution.UpdatedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); }
    }
    private string OfferUrl(string? configured, string slug) => !string.IsNullOrWhiteSpace(configured) ? configured : $"{options.PublicApiBaseUrl.TrimEnd('/')}/o/{Uri.EscapeDataString(slug)}";
    private static string Expand(string text, string name, string email, string offer, string url) => text.Replace("{{buyer_name}}", name, StringComparison.Ordinal).Replace("{{buyer_email}}", email, StringComparison.Ordinal).Replace("{{offer_name}}", offer, StringComparison.Ordinal).Replace("{{checkout_url}}", url, StringComparison.Ordinal);
}
