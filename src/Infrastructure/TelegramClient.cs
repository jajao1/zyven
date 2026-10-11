using System.Net.Http.Json;
using System.Text.Json;
using Zyven.Application;
namespace Zyven.Infrastructure;

public sealed record TelegramBot(long Id, string FirstName, string Username);
public sealed record TelegramChat(string Id, string Title, string Type);
public interface ITelegramClient
{
    Task<TelegramBot> GetMe(string token, CancellationToken ct);
    Task<TelegramChat> GetChat(string token, string chatId, CancellationToken ct);
    Task<bool> IsAdministrator(string token, string chatId, long botId, CancellationToken ct);
    Task SetWebhook(string token, string url, string secret, CancellationToken ct);
    Task<string> CreateInvite(string token, string chatId, DateTimeOffset expires, CancellationToken ct);
    Task SendMessage(string token, long userId, string text, CancellationToken ct);
}
public sealed class TelegramClient(HttpClient http) : ITelegramClient
{
    public async Task<TelegramBot> GetMe(string token, CancellationToken ct) { var r = await Call(token, "getMe", null, ct); return new(r.GetProperty("id").GetInt64(), r.GetProperty("first_name").GetString()!, r.GetProperty("username").GetString()!); }
    public async Task<TelegramChat> GetChat(string token, string chatId, CancellationToken ct) { var r = await Call(token, "getChat", new { chat_id = chatId }, ct); return new(r.GetProperty("id").GetInt64().ToString(), r.TryGetProperty("title", out var t) ? t.GetString() ?? chatId : chatId, r.GetProperty("type").GetString()!); }
    public async Task<bool> IsAdministrator(string token, string chatId, long botId, CancellationToken ct) { var r = await Call(token, "getChatMember", new { chat_id = chatId, user_id = botId }, ct); return r.GetProperty("status").GetString() is "administrator" or "creator"; }
    public async Task SetWebhook(string token, string url, string secret, CancellationToken ct) => _ = await Call(token, "setWebhook", new { url, secret_token = secret, allowed_updates = new[] { "message" }, drop_pending_updates = true }, ct);
    public async Task<string> CreateInvite(string token, string chatId, DateTimeOffset expires, CancellationToken ct) { var r = await Call(token, "createChatInviteLink", new { chat_id = chatId, expire_date = expires.ToUnixTimeSeconds(), member_limit = 1, name = "Zyven" }, ct); return r.GetProperty("invite_link").GetString()!; }
    public async Task SendMessage(string token, long userId, string text, CancellationToken ct) => _ = await Call(token, "sendMessage", new { chat_id = userId, text, disable_web_page_preview = true }, ct);
    private async Task<JsonElement> Call(string token, string method, object? body, CancellationToken ct) { using var response = await http.PostAsJsonAsync($"bot{token}/{method}", body ?? new { }, ct); using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct) ?? throw new OrganizationException(503, "Telegram indisponível."); if (!response.IsSuccessStatusCode || !json.RootElement.GetProperty("ok").GetBoolean()) throw new OrganizationException(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? 400 : 503, "Não foi possível validar ou operar o bot Telegram."); return json.RootElement.GetProperty("result").Clone(); }
}
