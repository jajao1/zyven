using FluentValidation;
namespace Zyven.Application;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record UserResponse(Guid Id, string Email, string DisplayName);
public record AuthResponse(string AccessToken, UserResponse User);
public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254); RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128); RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(80); }
}
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254); RuleFor(x => x.Password).NotEmpty().MaximumLength(128); }
}
