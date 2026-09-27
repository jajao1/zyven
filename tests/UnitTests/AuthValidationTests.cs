using FluentValidation;
using Zyven.Application;
namespace UnitTests;

public class AuthValidationTests
{
    [Theory]
    [InlineData("a@b.com", "longenoughpassword", "N")]
    [InlineData("bad", "longenoughpassword", "Name")]
    [InlineData("a@b.com", "short", "Name")]
    [InlineData("a@b.com", "longenoughpassword", " ")]
    public void Rejects_invalid_registration(string email, string password, string name) => Assert.False(new RegisterValidator().Validate(new RegisterRequest(email, password, name)).IsValid);
    [Fact] public void Accepts_valid_registration() => Assert.True(new RegisterValidator().Validate(new RegisterRequest("creator@example.com", "a strong password 123", "Creator")).IsValid);
}


