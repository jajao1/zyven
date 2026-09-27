using Zyven.Application;
namespace UnitTests;

public class CheckoutValidationTests
{
    [Fact]
    public void Page_validation_rejects_null_collections_items_and_newline_tokens()
    {
        var validator = new PageContentValidator(); var valid = new PageContent { Title = "Course" }; Assert.True(validator.Validate(valid).IsValid);
        var invalid = new[] { valid with { Color = "#002fa7\n" }, valid with { Fields = [new("key\n", "Label", "text", false)] }, valid with { Fields = [new("constructor", "Label", "text", false)] }, valid with { Fields = [new("a", "A", "text", true), new("a", "B", "text", false)] }, valid with { Benefits = null! }, valid with { Benefits = [null!] }, valid with { Testimonials = [null!] }, valid with { Faq = null! }, valid with { Fields = [null!] }, valid with { Title = null! } };
        foreach (var page in invalid) Assert.False(validator.Validate(page).IsValid);
    }
    [Fact]
    public void Checkout_validation_rejects_invalid_contact_and_oversized_fields()
    {
        var validator = new CheckoutInputValidator(); var valid = new CheckoutInput("Buyer Name", "buyer@example.test", null, null, null); Assert.True(validator.Validate(valid).IsValid);
        foreach (var input in new[] { valid with { Name = " " }, valid with { Email = "bad" }, valid with { Phone = "123\n" }, valid with { Document = "123\n" }, valid with { Fields = new() { ["key"] = new string('x', 1001) } }, valid with { Fields = new() { ["key"] = null! } } }) Assert.False(validator.Validate(input).IsValid);
    }
}
