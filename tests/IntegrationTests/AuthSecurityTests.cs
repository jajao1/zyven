using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zyven.Application;
using Zyven.Infrastructure;
namespace IntegrationTests;

public class AuthSecurityTests
{
    private static HttpClient Client(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new() { HandleCookies = false }); client.DefaultRequestHeaders.Add("X-Zyven-Client", "web"); return client;
    }
    private static async Task<(AuthResponse Auth, string Cookie)> Register(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"test-{Guid.NewGuid():N}@example.com", password = "secure testing password 123", displayName = "Tester" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase); Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase); Assert.Contains("path=/api/auth", header, StringComparison.OrdinalIgnoreCase);
        return ((await response.Content.ReadFromJsonAsync<AuthResponse>())!, header.Split(';')[0]);
    }
    private static Task<HttpResponseMessage> Refresh(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"); request.Headers.Add("Cookie", cookie); return client.SendAsync(request);
    }
    [Fact]
    public async Task Concurrent_refresh_has_one_winner_then_revokes_the_family()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = Client(app); var (auth, cookie) = await Register(client);
        var results = await Task.WhenAll(Refresh(client, cookie), Refresh(client, cookie));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        var winner = (await results.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", winner.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Logout_racing_refresh_leaves_no_valid_session()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = Client(app); var (auth, cookie) = await Register(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var logout = client.PostAsync("/api/auth/logout", null); var refresh = Refresh(client, cookie); await Task.WhenAll(logout, refresh); var logoutResponse = await logout; var refreshResponse = await refresh;
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        if (refreshResponse.IsSuccessStatusCode)
        {
            var nextCookie = refreshResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
            Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, nextCookie)).StatusCode);
        }
    }
    [Fact]
    public async Task Absolute_expiry_blocks_access_and_refresh_and_cleanup_keeps_live_replay_evidence()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = Client(app); var (auth, cookie) = await Register(client);
        var rotated = await Refresh(client, cookie); Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var session = await db.Sessions.SingleAsync(x => x.UserId == auth.User.Id);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(x => x.SessionId == session.Id));
        await new SessionCleanup(db, TimeProvider.System).Run(default);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(x => x.SessionId == session.Id));
        session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, rotated.Headers.GetValues("Set-Cookie").Single().Split(';')[0])).StatusCode);
        await new SessionCleanup(db, TimeProvider.System).Run(default);
        Assert.False(await db.RefreshTokens.AnyAsync(x => x.SessionId == session.Id));
    }
    [Fact]
    public async Task Foreign_origin_and_tampered_access_are_rejected()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = Client(app);
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-valid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Readiness_and_liveness_succeed_with_real_dependencies()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = Client(app);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
    [Fact]
    public async Task Redis_rate_gate_is_atomic_across_parallel_attempts()
    {
        await using var app = new WebApplicationFactory<Program>(); var gate = app.Services.GetRequiredService<AuthRateGate>();
        var key = Guid.NewGuid().ToString(); var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => gate.Allow("integration", key, 10)));
        Assert.Equal(10, results.Count(x => x));
    }
    [Fact]
    public async Task Production_uses_secure_cookie_and_hides_openapi()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://localhost" })));
        using var client = app.CreateClient(new() { HandleCookies = false, BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"secure-{Guid.NewGuid():N}@example.com", password = "a secure testing passphrase", displayName = "Secure creator" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secure", response.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(10), jwt.ValidTo - jwt.ValidFrom);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
    [Fact]
    public async Task Cleanup_deleting_a_family_while_refresh_waits_returns_unauthorized()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = Client(app);
        var (auth, cookie) = await Register(client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Sessions\" SET \"ExpiresAt\" = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE \"UserId\" = {auth.User.Id}");
        var blockerPid = await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var refreshing = Refresh(client, cookie);
        using var observerScope = app.Services.CreateScope();
        var observer = observerScope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var blocked = false;
        for (var attempt = 0; attempt < 100 && !blocked; attempt++)
        {
            blocked = await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE {0} = ANY(pg_blocking_pids(pid))) AS \"Value\"", blockerPid).SingleAsync();
            if (!blocked) await Task.Delay(20);
        }
        Assert.True(blocked, "Refresh should wait on the family row held by cleanup.");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Sessions\" WHERE \"UserId\" = {auth.User.Id}");
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await refreshing).StatusCode);
    }
    [Fact]
    public async Task Duplicate_normalized_email_and_wrong_password_do_not_create_users_or_sessions()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = Client(app);
        var (auth, _) = await Register(client);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { email = auth.User.Email.ToUpperInvariant(), password = "another secure passphrase", displayName = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = auth.User.Email, password = "the incorrect password" });
        var missing = await client.PostAsJsonAsync("/api/auth/login", new { email = $"missing-{Guid.NewGuid():N}@example.com", password = "the incorrect password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var wrongJson = System.Text.Json.JsonDocument.Parse(await wrong.Content.ReadAsStringAsync());
        using var missingJson = System.Text.Json.JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal(missingJson.RootElement.GetProperty("title").GetString(), wrongJson.RootElement.GetProperty("title").GetString());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == auth.User.Email.ToUpperInvariant()));
        Assert.Equal(1, await db.Sessions.CountAsync(x => x.UserId == auth.User.Id));
        Assert.NotEqual("secure testing password 123", (await db.Users.SingleAsync(x => x.Id == auth.User.Id)).PasswordHash);
    }
}
