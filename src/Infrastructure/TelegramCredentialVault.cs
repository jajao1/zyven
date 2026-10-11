namespace Zyven.Infrastructure;

public sealed class TelegramCredentialVault(string key)
{
    private readonly PushinPayCredentialVault inner = new(key);
    public EncryptedCredential Encrypt(string value) => inner.Encrypt(value);
    public string Decrypt(EncryptedCredential value) => inner.Decrypt(value);
    public static string Hash(string value) => PushinPayCredentialVault.HashCallbackSecret(value);
}
