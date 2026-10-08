using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public interface IBuyerCodeDelivery { Task Send(string email, string code, CancellationToken ct); }

public sealed class DevelopmentBuyerCodeDelivery(ILogger<DevelopmentBuyerCodeDelivery> logger) : IBuyerCodeDelivery
{
    public Task Send(string email, string code, CancellationToken ct)
    {
        logger.LogInformation("Buyer access code generated for e-mail fingerprint {Fingerprint}: {BuyerCode}", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)))[..12], code);
        return Task.CompletedTask;
    }
}

public sealed class BuyerAuthService(ZyvenDbContext db, IBuyerCodeDelivery delivery, TimeProvider time, BuyerCodeHasher hasher)
{
    public async Task RequestCode(BuyerCodeRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (email is null || !await db.Customers.AnyAsync(x => x.NormalizedEmail == email, ct)) return;
        var now = time.GetUtcNow();
        var recent = await db.BuyerAccessCodes.Where(x => x.NormalizedEmail == email && x.ConsumedAt == null).ToListAsync(ct);
        foreach (var old in recent) old.ConsumedAt = now;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.BuyerAccessCodes.Add(new() { NormalizedEmail = email, CodeHash = hasher.Hash(email, code), CreatedAt = now, ExpiresAt = now.AddMinutes(10) });
        await db.SaveChangesAsync(ct);
        await delivery.Send(email, code, ct);
    }

    public async Task<(string Token, DateTimeOffset ExpiresAt)?> Verify(BuyerCodeVerification request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (email is null || request.Code.Length != 6 || request.Code.Any(c => !char.IsAsciiDigit(c))) return null;
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.BuyerAccessCodes.FromSqlInterpolated($"SELECT * FROM \"BuyerAccessCodes\" WHERE \"NormalizedEmail\" = {email} AND \"ConsumedAt\" IS NULL ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE").SingleOrDefaultAsync(ct);
        if (item is null || item.ExpiresAt <= now || item.FailedAttempts >= 5) return null;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(item.CodeHash), Convert.FromHexString(hasher.Hash(email, request.Code))))
        {
            item.FailedAttempts++; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return null;
        }
        item.ConsumedAt = now;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var expires = now.AddDays(30);
        db.BuyerSessions.Add(new() { NormalizedEmail = email, TokenHash = HashToken(token), CreatedAt = now, ExpiresAt = expires });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (token, expires);
    }

    public async Task<string?> Authenticate(string? token, CancellationToken ct)
    {
        if (token is null || token.Length != 96) return null;
        var hash = HashToken(token); var now = time.GetUtcNow();
        return await db.BuyerSessions.AsNoTracking().Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now).Select(x => x.NormalizedEmail).SingleOrDefaultAsync(ct);
    }

    public async Task Logout(string? token, CancellationToken ct)
    {
        if (token is null || token.Length != 96) return;
        var hash = HashToken(token);
        var session = await db.BuyerSessions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, ct);
        if (session is not null) { session.RevokedAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); }
    }

    private static string? Normalize(string? value) { var email = value?.Trim().ToUpperInvariant(); return email is { Length: > 2 and <= 254 } && email.Contains('@') ? email : null; }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class BuyerCodeHasher(string secret)
{
    private readonly byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    public string Hash(string email, string code) { using var hmac = new HMACSHA256(key); return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{email}:{code}"))); }
}
