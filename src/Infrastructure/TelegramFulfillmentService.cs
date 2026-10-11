using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class TelegramFulfillmentService(ZyvenDbContext db, TelegramCredentialVault vault, ITelegramClient telegram, AutomationRunner automations, TimeProvider time)
{
    public async Task<TelegramLinkResponse> BuyerLink(Guid executionId, string email, CancellationToken ct)
    {
        var row = await (from x in db.FulfillmentExecutions join e in db.Entitlements on new { x.EntitlementId, x.OrganizationId } equals new { EntitlementId = e.Id, e.OrganizationId } join p in db.Payments on new { e.PaymentId, e.OrganizationId } equals new { PaymentId = p.Id, p.OrganizationId } join c in db.Customers on new { p.CustomerId, p.OrganizationId } equals new { CustomerId = c.Id, c.OrganizationId } join i in db.TelegramIntegrations on x.TelegramIntegrationId equals i.Id where x.Id == executionId && x.Type == "TELEGRAM_ACCESS" && e.Status == "ACTIVE" && p.Status == "PAID" && c.NormalizedEmail == email select new { x, i }).SingleOrDefaultAsync(ct) ?? throw new OrganizationException(404, "Entrega não encontrada.");
        return await Create(row.x, row.i, ct);
    }
    private async Task<TelegramLinkResponse> Create(FulfillmentExecution execution, TelegramIntegration integration, CancellationToken ct)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(); var expiry = time.GetUtcNow().AddMinutes(15);
        db.TelegramLinkTokens.Add(new() { OrganizationId = execution.OrganizationId, FulfillmentExecutionId = execution.Id, Hash = TelegramCredentialVault.Hash(raw), CreatedAt = time.GetUtcNow(), ExpiresAt = expiry });
        await db.SaveChangesAsync(ct); return new($"https://t.me/{integration.BotUsername}?start={raw}", expiry);
    }
    public async Task ProcessWebhook(Guid integrationId, string pathSecret, string? headerSecret, JsonDocument update, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(headerSecret) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(pathSecret)), Convert.FromHexString(TelegramCredentialVault.Hash(headerSecret)))) throw new OrganizationException(404, "Integração não encontrada.");
        var integration = await db.TelegramIntegrations.SingleOrDefaultAsync(x => x.Id == integrationId && x.WebhookSecretHash == TelegramCredentialVault.Hash(pathSecret) && x.Status == "ACTIVE", ct) ?? throw new OrganizationException(404, "Integração não encontrada.");
        if (!update.RootElement.TryGetProperty("message", out var message) || message.GetProperty("chat").GetProperty("type").GetString() != "private" || !message.TryGetProperty("text", out var textElement)) return;
        var text = textElement.GetString() ?? ""; if (!text.StartsWith("/start ", StringComparison.Ordinal)) return; var raw = text[7..].Trim(); if (raw.Length != 48) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var hash = TelegramCredentialVault.Hash(raw); var link = await db.TelegramLinkTokens.FromSqlInterpolated($"SELECT * FROM \"TelegramLinkTokens\" WHERE \"Hash\" = {hash} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (link is null || link.ConsumedAt is not null || link.ExpiresAt <= time.GetUtcNow() || link.OrganizationId != integration.OrganizationId) return;
        var execution = await db.FulfillmentExecutions.SingleAsync(x => x.Id == link.FulfillmentExecutionId && x.TelegramIntegrationId == integration.Id, ct); var userId = message.GetProperty("from").GetProperty("id").GetInt64();
        var grant = await db.TelegramAccessGrants.SingleOrDefaultAsync(x => x.FulfillmentExecutionId == execution.Id, ct);
        if (grant is not null && grant.TelegramUserId != userId) { await telegram.SendMessage(Decrypt(integration), userId, "Esta compra já foi vinculada a outra conta Telegram.", ct); return; }
        var token = Decrypt(integration);
        if (grant is null) db.TelegramAccessGrants.Add(new() { OrganizationId = execution.OrganizationId, FulfillmentExecutionId = execution.Id, TelegramUserId = userId, GrantedAt = time.GetUtcNow() });
        link.ConsumedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        if (!await automations.Start(execution.EntitlementId, userId, ct)) { var invite = await telegram.CreateInvite(token, execution.TelegramChatId, time.GetUtcNow().AddMinutes(15), ct); await telegram.SendMessage(token, userId, $"Seu acesso está liberado. Este convite é individual e expira em 15 minutos:\n{invite}", ct); }
    }
    private string Decrypt(TelegramIntegration i) => vault.Decrypt(new(i.TokenCiphertext, i.TokenNonce, i.TokenTag, i.TokenFingerprint));
}
