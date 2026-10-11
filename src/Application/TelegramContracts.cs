namespace Zyven.Application;

public sealed record TelegramConnectRequest(string Token);
public sealed record TelegramIntegrationResponse(Guid Id, long BotId, string BotName, string BotUsername, string TokenFingerprint, string Status);
public sealed record TelegramFulfillmentRequest(string ChatId, string Name);
public sealed record TelegramFulfillmentResponse(Guid Id, string Type, string Name, string ChatId, string BotUsername, string Status);
public sealed record TelegramLinkResponse(string Url, DateTimeOffset ExpiresAt);
