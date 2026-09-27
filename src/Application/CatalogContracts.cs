using FluentValidation;
namespace Zyven.Application;

public record ProductRequest(string Name, string Slug, string? Description, string? ImageUrl, string Status = "DRAFT");
public record OfferRequest(Guid ProductId, string Name, string Slug, string? Headline, string? Description, [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)] decimal Price, string Currency, string BillingType, string Status = "DRAFT");
public sealed class ProductValidator : AbstractValidator<ProductRequest>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z");
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.ImageUrl).MaximumLength(2048).Must(x => string.IsNullOrEmpty(x) || Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)).WithMessage("Use uma URL HTTPS válida para a imagem.");
        RuleFor(x => x.Status).Must(CatalogRules.Status).WithMessage("Status inválido.");
    }
}
public sealed class OfferValidator : AbstractValidator<OfferRequest>
{
    public OfferValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z");
        RuleFor(x => x.Headline).MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(9999999999999999.99m).Must(x => decimal.Round(x, 2) == x).WithMessage("Preço deve ser positivo e ter até duas casas decimais.");
        RuleFor(x => x.Currency).NotEmpty().Matches(@"\A[A-Z]{3}\z");
        RuleFor(x => x.Status).Must(CatalogRules.Status).WithMessage("Status inválido.");
        RuleFor(x => x.BillingType).Must(x => x is "ONE_TIME" or "SUBSCRIPTION").WithMessage("Tipo de cobrança inválido.");
    }
}
public static class CatalogRules
{
    public static bool Status(string? status) => status is "DRAFT" or "ACTIVE" or "INACTIVE" or "ARCHIVED";
}
