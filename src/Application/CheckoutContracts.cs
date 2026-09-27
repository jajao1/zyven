using FluentValidation;
using System.Text.RegularExpressions;
namespace Zyven.Application;

public sealed record PageFaq(string Question, string Answer);
public sealed record PageTestimonial(string Name, string Text);
public sealed record CheckoutField(string Key, string Label, string Type, bool Required);
public sealed record PageContent
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string Color { get; init; } = "#002fa7";
    public string Description { get; init; } = "";
    public string[] Benefits { get; init; } = [];
    public PageTestimonial[] Testimonials { get; init; } = [];
    public PageFaq[] Faq { get; init; } = [];
    public string Guarantee { get; init; } = "";
    public string Cta { get; init; } = "Continuar";
    public CheckoutField[] Fields { get; init; } = [];
}
public sealed record PublicOffer(string Slug, string Name, string ProductName, string Price, string Currency, string BillingType, PageContent Page);
public sealed record CheckoutInput(string Name, string Email, string? Phone, string? Document, Dictionary<string, string>? Fields);
public sealed record CheckoutResponse(Guid Id, string Status, string Price, string Currency, DateTimeOffset ExpiresAt, string Name, string Email, string? Phone, string? Document, Dictionary<string, string> Fields, string OfferSlug);
public sealed class PageContentValidator : AbstractValidator<PageContent>
{
    public PageContentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Subtitle).NotNull().MaximumLength(500);
        RuleFor(x => x.Description).NotNull().MaximumLength(10000);
        RuleFor(x => x.Guarantee).NotNull().MaximumLength(2000);
        RuleFor(x => x.Cta).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Color).NotNull().Matches(@"\A#[0-9a-fA-F]{6}\z");
        RuleFor(x => x.ImageUrl).Must(SafeUrl); RuleFor(x => x.VideoUrl).Must(SafeUrl); RuleFor(x => x.LogoUrl).Must(SafeUrl);
        RuleFor(x => x.Benefits).NotNull().Must(x => x is { Length: <= 20 } && x.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 500));
        RuleFor(x => x.Testimonials).NotNull().Must(x => x is { Length: <= 10 } && x.All(v => v is not null && v.Name is { Length: > 0 and <= 100 } && v.Text is { Length: > 0 and <= 2000 }));
        RuleFor(x => x.Faq).NotNull().Must(x => x is { Length: <= 20 } && x.All(v => v is not null && v.Question is { Length: > 0 and <= 300 } && v.Answer is { Length: > 0 and <= 2000 }));
        RuleFor(x => x.Fields).NotNull().Must(x => x is { Length: <= 10 } && x.All(v => v is not null && v.Key is not null && v.Key is not ("constructor" or "prototype") && Regex.IsMatch(v.Key, @"\A[a-z][a-z0-9_]{0,39}\z") && v.Label is { Length: > 0 and <= 100 } && v.Type is "text" or "textarea") && x.Select(v => v.Key).Distinct().Count() == x.Length);
    }
    private static bool SafeUrl(string? value) => string.IsNullOrEmpty(value) || (value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo));
}
public sealed class CheckoutInputValidator : AbstractValidator<CheckoutInput>
{
    public CheckoutInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(x => x is not null && x.Trim().Length >= 2).MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254).Must(CustomerIdentity.ValidEmail);
        RuleFor(x => x.Phone).MaximumLength(30).Must(CustomerIdentity.ValidPhone);
        RuleFor(x => x.Document).MaximumLength(40).Matches(@"\A[a-zA-Z0-9 ./-]*\z");
        RuleFor(x => x.Fields).Must(x => x is null || (x.Count <= 10 && x.All(v => v.Key.Length <= 40 && v.Value is not null && v.Value.Length <= 1000)));
    }
}
