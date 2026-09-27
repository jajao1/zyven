using System.Text.RegularExpressions;
namespace Zyven.Application;

public static class CustomerIdentity
{
    public static string TrimEmail(string email) => email.Trim(' ', '\t', '\r', '\n');
    public static bool ValidEmail(string? email) => email is not null && TrimEmail(email).All(c => c is >= '!' and <= '~');
    // ASCII case folding is deterministic across .NET and the migration's database locale.
    public static string Email(string email) => new(TrimEmail(email).Select(c => c is >= 'a' and <= 'z' ? (char)(c - 32) : c).ToArray());
    // Explicit international prefix only. No country is inferred from local numbers.
    public static string? Phone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var value = Regex.Replace(phone.Trim(), "[ ()-]", "");
        return Regex.IsMatch(value, @"\A\+[1-9][0-9]{6,14}\z") ? value : null;
    }
    public static bool ValidPhone(string? phone) => string.IsNullOrWhiteSpace(phone) || Phone(phone) is not null;
}
