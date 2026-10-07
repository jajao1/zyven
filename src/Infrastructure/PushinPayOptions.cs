namespace Zyven.Infrastructure;

public sealed class PushinPayOptions
{
    public const string SectionName = "Payments:PushinPay";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api-sandbox.pushinpay.com.br/api/";
    public string PlatformAccountId { get; set; } = "";
    public int MaxSplitPercent { get; set; } = 50;
}
