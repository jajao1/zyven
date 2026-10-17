using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zyven.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Zyven.Application;
namespace IntegrationTests;

public class OrganizationTests
{
    private sealed class AcceptingPushinPayValidator : IPushinPayAccountValidator
    {
        public Task<PaymentOperationError?> ValidateAsync(string token, CancellationToken ct) => Task.FromResult<PaymentOperationError?>(null);
    }

    private static async Task<HttpClient> Register(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"org-{Guid.NewGuid():N}@example.com", password = "secure organizational password", displayName = "Member" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<string> Create(HttpClient client) => (await Json(await client.PostAsJsonAsync("/api/organizations", new { name = "Studio" }), HttpStatusCode.Created)).GetProperty("id").GetString()!;
    [Fact]
    public async Task Payment_account_validates_encrypts_and_never_returns_token()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPushinPayAccountValidator>();
            services.AddSingleton<IPushinPayAccountValidator, AcceptingPushinPayValidator>();
        }));
        using var owner = await Register(app);
        var id = await Create(owner);

        var response = await owner.PutAsJsonAsync($"/api/organizations/{id}/payment-account", new { token = "seller-secret" });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("seller-secret", json);
        var publicAccount = JsonDocument.Parse(json).RootElement;
        Assert.Equal("PUSHINPAY", publicAccount.GetProperty("provider").GetString());
        Assert.Equal(12, publicAccount.GetProperty("tokenFingerprint").GetString()!.Length);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var stored = await db.MerchantAccounts.SingleAsync(x => x.OrganizationId == Guid.Parse(id));
        Assert.NotEqual("seller-secret", stored.CredentialCiphertext);
        Assert.Equal("ACTIVE", stored.Status);
        Assert.Equal("PUSHINPAY", stored.Provider);
        Assert.Equal(64, stored.CallbackSecretHash!.Length);
    }
    [Fact]
    public async Task Independent_owners_cannot_read_or_mutate_each_others_organizations()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var alice = await Register(app); using var bob = await Register(app);
        var a = await Create(alice); var b = await Create(bob);
        var aMembers = await Json(await alice.GetAsync($"/api/organizations/{a}/members"));
        var bMembers = await Json(await bob.GetAsync($"/api/organizations/{b}/members"));
        var aMember = aMembers.GetProperty("items")[0].GetProperty("id").GetString();
        var bMember = bMembers.GetProperty("items")[0].GetProperty("id").GetString();
        foreach (var (client, own, foreign, foreignMember) in new[] { (alice, a, b, bMember), (bob, b, a, aMember) })
        {
            var list = await Json(await client.GetAsync("/api/organizations"));
            Assert.Equal(own, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/organizations/{foreign}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/organizations/{foreign}", new { name = "Foreign" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/organizations/{foreign}/members")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/organizations/{foreign}/members", new { email = "unknown@example.com", role = "OWNER" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/organizations/{foreign}/members/{foreignMember}", new { role = "SUPPORT" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/organizations/{foreign}/members/{foreignMember}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/organizations/{own}/members/{foreignMember}", new { role = "SUPPORT" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/organizations/{own}/members/{foreignMember}")).StatusCode);
        }
    }
    [Theory]
    [InlineData("OPERATOR")]
    [InlineData("FINANCE")]
    [InlineData("SUPPORT")]
    public async Task Administrators_manage_lower_roles_but_lower_roles_cannot_write(string role)
    {
        await using var app = new WebApplicationFactory<Program>();
        using var owner = await Register(app); using var admin = await Register(app); using var lower = await Register(app);
        var id = await Create(owner);
        var adminUser = await Json(await admin.GetAsync("/api/auth/me"));
        var lowerUser = await Json(await lower.GetAsync("/api/auth/me"));
        await Json(await owner.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = adminUser.GetProperty("email").GetString(), role = "ADMIN" }), HttpStatusCode.Created);
        var added = await Json(await admin.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = lowerUser.GetProperty("email").GetString(), role }), HttpStatusCode.Created);
        var memberId = added.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await lower.GetAsync($"/api/organizations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lower.GetAsync($"/api/organizations/{id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lower.PatchAsJsonAsync($"/api/organizations/{id}", new { name = "Spoofed", userId = adminUser.GetProperty("id").GetString(), role = "OWNER" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lower.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = "unknown@example.com", role = "SUPPORT" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lower.PatchAsJsonAsync($"/api/organizations/{id}/members/{memberId}", new { role = "OWNER" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lower.DeleteAsync($"/api/organizations/{id}/members/{memberId}")).StatusCode);
        await Json(await admin.PatchAsJsonAsync($"/api/organizations/{id}/members/{memberId}", new { role = "FINANCE" }));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/organizations/{id}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lower.GetAsync($"/api/organizations/{id}")).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var audits = await db.AuditLogs.Where(x => x.OrganizationId == Guid.Parse(id) && x.TargetId == Guid.Parse(memberId!)).ToListAsync();
        Assert.Equal(3, audits.Count);
        Assert.All(audits, item => Assert.Equal(adminUser.GetProperty("id").GetGuid(), item.ActorUserId));
        Assert.Contains(audits, x => x.Action == "organization.member.added");
        Assert.Contains(audits, x => x.Action == "organization.member.role_changed");
        Assert.Contains(audits, x => x.Action == "organization.member.removed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_owner_removals_or_demotions_preserve_one_owner(bool demote)
    {
        await using var app = new WebApplicationFactory<Program>();
        using var first = await Register(app); using var second = await Register(app);
        var id = await Create(first);
        var secondUser = await Json(await second.GetAsync("/api/auth/me"));
        var added = await Json(await first.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = secondUser.GetProperty("email").GetString(), role = "OWNER" }), HttpStatusCode.Created);
        var secondId = added.GetProperty("id").GetString();
        var members = await Json(await first.GetAsync($"/api/organizations/{id}/members"));
        var firstId = members.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetString() != secondId).GetProperty("id").GetString();
        using var blockerScope = app.Services.CreateScope(); var blocker = blockerScope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Organizations\" WHERE \"Id\" = {Guid.Parse(id)} FOR UPDATE");
        var blockerPid = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        Task<HttpResponseMessage> Change(HttpClient client, string? target) => demote
            ? client.PatchAsJsonAsync($"/api/organizations/{id}/members/{target}", new { role = "ADMIN" })
            : client.DeleteAsync($"/api/organizations/{id}/members/{target}");
        var a = Change(first, firstId); var b = Change(second, secondId);
        using var observerScope = app.Services.CreateScope(); var observer = observerScope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var waiting = 0;
        for (var attempt = 0; attempt < 100 && waiting < 2; attempt++)
        {
            waiting = await observer.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE cardinality(pg_blocking_pids(pid)) > 0 AND query LIKE '%Organizations%FOR UPDATE%' AND pid <> {0}", blockerPid).SingleAsync();
            if (waiting < 2) await Task.Delay(20);
        }
        Assert.Equal(2, waiting);
        await transaction.CommitAsync();
        var results = await Task.WhenAll(a, b);
        Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await observer.OrganizationMembers.CountAsync(x => x.OrganizationId == Guid.Parse(id) && x.Role == "OWNER"));
    }
    [Fact]
    public async Task Lists_are_paginated_and_successful_changes_are_audited()
    {
        await using var app = new WebApplicationFactory<Program>(); using var owner = await Register(app);
        var id = await Create(owner); await Create(owner);
        var page = await Json(await owner.GetAsync("/api/organizations?page=1&pageSize=1"));
        Assert.Equal(2, page.GetProperty("total").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/organizations?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/organizations?page=2147483647&pageSize=100")).StatusCode);
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(app.Services);
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Zyven.Infrastructure.ZyvenDbContext>(scope.ServiceProvider);
        var actions = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.Database.SqlQueryRaw<string>("SELECT \"Action\" AS \"Value\" FROM \"AuditLogs\" WHERE \"OrganizationId\" = {0}", Guid.Parse(id)));
        Assert.Contains("organization.created", actions);
    }
    [Fact]
    public async Task Creation_scopes_listing_and_membership_to_authenticated_user()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var owner = await Register(app); using var outsider = await Register(app); using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/organizations")).StatusCode);
        var id = await Create(owner);
        var list = await Json(await owner.GetAsync("/api/organizations"));
        Assert.Single(list.GetProperty("items").EnumerateArray()); Assert.Equal("OWNER", list.GetProperty("items")[0].GetProperty("role").GetString());
        Assert.Empty((await Json(await outsider.GetAsync("/api/organizations"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/organizations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PatchAsJsonAsync($"/api/organizations/{id}", new { name = "Stolen" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/organizations/{id}/members")).StatusCode);
        Assert.Equal("Renamed", (await Json(await owner.PatchAsJsonAsync($"/api/organizations/{id}", new { name = "Renamed" }))).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/organizations", new { name = " " })).StatusCode);
    }
    [Fact]
    public async Task Member_roles_scoping_and_last_owner_are_enforced()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var owner = await Register(app); using var admin = await Register(app); using var operatorClient = await Register(app);
        var id = await Create(owner); var otherId = await Create(owner);
        var adminEmail = (await Json(await admin.GetAsync("/api/auth/me"))).GetProperty("email").GetString();
        var operatorEmail = (await Json(await operatorClient.GetAsync("/api/auth/me"))).GetProperty("email").GetString();
        var member = await Json(await owner.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = adminEmail, role = "ADMIN" }), HttpStatusCode.Created);
        var adminId = member.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = adminEmail, role = "ADMIN" })).StatusCode);
        var low = await Json(await admin.PostAsJsonAsync($"/api/organizations/{id}/members", new { email = operatorEmail, role = "OPERATOR" }), HttpStatusCode.Created);
        var lowId = low.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PatchAsJsonAsync($"/api/organizations/{id}", new { name = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync($"/api/organizations/{id}/members/{lowId}", new { role = "OWNER" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.DeleteAsync($"/api/organizations/{id}/members/{adminId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/organizations/{otherId}/members/{lowId}")).StatusCode);
        var members = await Json(await owner.GetAsync($"/api/organizations/{id}/members"));
        var ownerId = members.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("role").GetString() == "OWNER").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/organizations/{id}/members/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync($"/api/organizations/{id}/members/{ownerId}", new { role = "ADMIN" })).StatusCode);
        await Json(await owner.PatchAsJsonAsync($"/api/organizations/{id}/members/{adminId}", new { role = "OWNER" }));
        var results = await Task.WhenAll(owner.DeleteAsync($"/api/organizations/{id}/members/{ownerId}"), admin.PatchAsJsonAsync($"/api/organizations/{id}/members/{adminId}", new { role = "ADMIN" }));
        Assert.Single(results, x => x.IsSuccessStatusCode);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await Json(await admin.GetAsync($"/api/organizations/{id}/members"))).GetProperty("items").EnumerateArray(), x => x.GetProperty("role").GetString() == "OWNER");
        Assert.Single((await Json(await admin.GetAsync("/api/organizations"))).GetProperty("items").EnumerateArray());
    }
}
