using Zyven.Application;
namespace UnitTests;

public class CustomerIdentityTests
{
    [Theory]
    [InlineData("\u00a0buyer@example.test")]
    [InlineData("buyer@example.test\u2003")]
    [InlineData("bu yer@example.test")]
    [InlineData("é@example.test")]
    public void Unsupported_whitespace_and_non_ascii_addresses_are_rejected(string email)
    {
        Assert.False(new CheckoutInputValidator().Validate(new CheckoutInput("Buyer", email, null, null, null)).IsValid);
    }
    [Fact]
    public void Email_keeps_dots_aliases_and_uses_stable_case()
    {
        Assert.Equal("FIRST.LAST+TAG@EXAMPLE.TEST", CustomerIdentity.Email(" First.Last+tag@Example.test \r\n"));
        Assert.NotEqual(CustomerIdentity.Email("first.last@example.test"), CustomerIdentity.Email("firstlast@example.test"));
    }
    [Theory]
    [InlineData("+55 (11) 99999-0000", "+5511999990000")]
    [InlineData("+1 202 555-0123", "+12025550123")]
    [InlineData("11999990000", null)]
    [InlineData("+01234567", null)]
    [InlineData("++5511999990000", null)]
    [InlineData("+1234567890123456", null)]
    public void Phone_requires_explicit_country_and_never_invents_one(string input, string? expected) => Assert.Equal(expected, CustomerIdentity.Phone(input));
}
