using System.Security.Cryptography;
using System.Text;

namespace Zyven.Infrastructure;

public sealed record EncryptedCredential(string Ciphertext, string Nonce, string Tag, string Fingerprint);

public sealed class PushinPayCredentialVault
{
    private readonly byte[] key;

    public PushinPayCredentialVault(string base64Key)
    {
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Payments:CredentialEncryptionKey must be valid Base64.", exception);
        }

        if (key.Length != 32)
            throw new InvalidOperationException("Payments:CredentialEncryptionKey must decode to 32 bytes.");
    }

    public EncryptedCredential Encrypt(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("A non-empty secret is required.", nameof(secret));

        var plain = Encoding.UTF8.GetBytes(secret.Trim());
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plain, cipher, tag);
        var fingerprint = Convert.ToHexString(SHA256.HashData(plain))[..12];

        CryptographicOperations.ZeroMemory(plain);
        return new(
            Convert.ToBase64String(cipher),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            fingerprint);
    }

    public string Decrypt(EncryptedCredential value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var cipher = Convert.FromBase64String(value.Ciphertext);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(
            Convert.FromBase64String(value.Nonce),
            cipher,
            Convert.FromBase64String(value.Tag),
            plain);
        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static string HashCallbackSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("A non-empty callback secret is required.", nameof(secret));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
    }
}
