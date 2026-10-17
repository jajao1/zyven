using System.Security.Cryptography;
using Zyven.Infrastructure;

namespace UnitTests;

public sealed class PushinPayCredentialVaultTests
{
    [Fact]
    public void Vault_encrypts_and_authenticates_tokens()
    {
        var vault = TestVault();

        var encrypted = vault.Encrypt("  merchant-secret-token  ");

        Assert.DoesNotContain("merchant-secret-token", encrypted.Ciphertext);
        Assert.Equal("merchant-secret-token", vault.Decrypt(encrypted));
        Assert.Equal(12, encrypted.Fingerprint.Length);
    }

    [Fact]
    public void Vault_uses_a_fresh_nonce_for_each_encryption()
    {
        var vault = TestVault();

        var first = vault.Encrypt("merchant-secret-token");
        var second = vault.Encrypt("merchant-secret-token");

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Vault_rejects_modified_ciphertext()
    {
        var vault = TestVault();
        var encrypted = vault.Encrypt("merchant-secret-token");
        var bytes = Convert.FromBase64String(encrypted.Ciphertext);
        bytes[0] ^= 1;
        var changed = encrypted with { Ciphertext = Convert.ToBase64String(bytes) };

        Assert.Throws<AuthenticationTagMismatchException>(() => vault.Decrypt(changed));
    }

    [Fact]
    public void Vault_requires_a_256_bit_key()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Assert.Throws<InvalidOperationException>(() => new PushinPayCredentialVault(key));
    }

    [Fact]
    public void Vault_rejects_empty_secrets()
    {
        var vault = TestVault();

        Assert.Throws<ArgumentException>(() => vault.Encrypt("   "));
    }

    private static PushinPayCredentialVault TestVault() =>
        new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
}
