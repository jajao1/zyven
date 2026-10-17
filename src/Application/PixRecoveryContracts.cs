namespace Zyven.Application;

public sealed record PixRecoveryMessageRequest(int DelayMinutes, string EmailText, string TelegramText);
public sealed record PixRecoveryRequest(bool Enabled, IReadOnlyList<PixRecoveryMessageRequest> Messages);
public sealed record PixRecoveryMessageResponse(int Position, int DelayMinutes, string EmailText, string TelegramText);
public sealed record PixRecoveryResponse(Guid Id, bool Enabled, int PublishedVersion, DateTimeOffset? PublishedAt, IReadOnlyList<PixRecoveryMessageResponse> Messages);
