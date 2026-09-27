using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public record AuthGrant(AuthResponse Response, string RefreshToken, DateTimeOffset ExpiresAt);
public sealed class AuthService(ZyvenDbContext db, IPasswordHasher<User> passwords, IConfiguration config, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string CreateRefresh() => Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
    public async Task<AuthGrant?> Register(RegisterRequest input, CancellationToken ct)
    {
        var user = new User { Email = input.Email.Trim(), NormalizedEmail = NormalizeEmail(input.Email), DisplayName = input.DisplayName.Trim(), CreatedAt = Now };
        user.PasswordHash = passwords.HashPassword(user, input.Password);
        db.Users.Add(user);
        try { return await StartSession(user, ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return null; }
    }
    // The dummy hash avoids skipping password work for unknown accounts.
    private static readonly User DummyUser = new();
    private static readonly string DummyHash = new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 210000 })).HashPassword(DummyUser, "dummy-password-not-a-credential");
    public async Task<AuthGrant?> Login(LoginRequest input, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == NormalizeEmail(input.Email), ct);
        var result = passwords.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyHash, input.Password);
        if (user is null || result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = passwords.HashPassword(user, input.Password);
        return await StartSession(user, ct);
    }
    private async Task<AuthGrant> StartSession(User user, CancellationToken ct)
    {
        var session = new AuthSession { User = user, UserId = user.Id, ExpiresAt = Now.AddDays(30) };
        var raw = CreateRefresh(); db.Sessions.Add(session); db.RefreshTokens.Add(new() { Hash = Hash(raw), Session = session });
        Audit(session, "session.created");
        await db.SaveChangesAsync(ct); return Grant(session, user, raw);
    }
    public async Task<AuthGrant?> Refresh(string raw, CancellationToken ct)
    {
        if (raw.Length != 96) return null;
        var hash = Hash(raw);
        // Every refresh and logout locks the same family row, making rotation/revocation atomic.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var sessionId = await db.RefreshTokens.Where(x => x.Hash == hash).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        if (sessionId is null) return null;
        var session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM \"Sessions\" WHERE \"Id\" = {sessionId.Value} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (session is null) return null; // Cleanup may have removed an expired family after token lookup.
        var token = await db.RefreshTokens.SingleAsync(x => x.Hash == hash, ct);
        if (token.ConsumedAt is not null)
        {
            session.RevokedAt = Now; Audit(session, "session.revoked.replay"); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return null;
        }
        if (session.RevokedAt is not null || session.ExpiresAt <= Now) return null;
        token.ConsumedAt = Now;
        var next = CreateRefresh(); db.RefreshTokens.Add(new() { Hash = Hash(next), SessionId = session.Id });
        var user = await db.Users.SingleAsync(x => x.Id == session.UserId, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Grant(session, user, next);
    }
    public async Task Logout(Guid sessionId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM \"Sessions\" WHERE \"Id\" = {sessionId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (session is not null) { session.RevokedAt = Now; Audit(session, "session.revoked.logout"); await db.SaveChangesAsync(ct); }
        await transaction.CommitAsync(ct);
    }
    private void Audit(AuthSession session, string action) => db.AuthEvents.Add(new() { UserId = session.UserId, SessionId = session.Id, Action = action, OccurredAt = Now });
    private AuthGrant Grant(AuthSession session, User user, string refresh)
    {
        var now = Now;
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], [
         new(JwtRegisteredClaimNames.Sub,user.Id.ToString()),new("sid",session.Id.ToString()),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
        ], now.UtcDateTime, now.AddMinutes(10).UtcDateTime, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        return new(new(new JwtSecurityTokenHandler().WriteToken(jwt), new(user.Id, user.Email, user.DisplayName)), refresh, session.ExpiresAt);
    }
}
