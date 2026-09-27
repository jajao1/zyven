using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zyven.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Zyven.Application;
namespace IntegrationTests;

public class AuthLifecycleTests
{
    [Fact]
    public async Task Registration_me_rotation_replay_and_logout_are_enforced()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var email = $"creator-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "secure password 123", displayName = "Creator" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
        var cookie = register.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("zyven_refresh=")).Split(';')[0];
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var nextCookie = refresh.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("zyven_refresh=")).Split(';')[0];
        Assert.NotEqual(cookie, nextCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "secure password 123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("zyven_refresh=")).Split(';')[0]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var events = await db.Database.SqlQueryRaw<string>("SELECT \"Action\" AS \"Value\" FROM \"AuthEvents\" WHERE \"UserId\" = {0}", auth.User.Id).ToListAsync();
        Assert.Contains("session.revoked.replay", events); Assert.Contains("session.revoked.logout", events);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }
    [Fact]
    public async Task Invalid_registration_and_login_and_csrf_are_rejected()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/login", new { email = "a@b.com", password = "bad" })).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = "bad", password = "short", displayName = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = "invalid password" })).StatusCode);
    }
}
