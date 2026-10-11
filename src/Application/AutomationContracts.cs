namespace Zyven.Application;

public sealed record AutomationStepRequest(string Type, string? Text = null, string? Url = null, int? WaitMinutes = null, string? Condition = null);
public sealed record AutomationDraftRequest(IReadOnlyList<AutomationStepRequest> Steps);
public sealed record AutomationStepResponse(Guid Id, int Position, string Type, string? Text, string? Url, int? WaitMinutes, string? Condition);
public sealed record AutomationResponse(Guid Id, int PublishedVersion, DateTimeOffset? PublishedAt, IReadOnlyList<AutomationStepResponse> Steps);
