using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Zyven.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Zyven.Application;
namespace IntegrationTests;

public class CatalogTests
{
    private static async Task<HttpClient> Register(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"catalog-{Guid.NewGuid():N}@example.com", password = "catalog secure password", displayName = "Seller" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<string> Org(HttpClient client) => (await Json(await client.PostAsJsonAsync("/api/organizations", new { name = "Catalog studio" }), HttpStatusCode.Created)).GetProperty("id").GetString()!;
    private static object Product(string slug = "course", string status = "DRAFT") => new { name = "Course", slug, description = "Lessons", imageUrl = "https://example.com/cover.png", status };
    private static object Offer(string productId, string slug, decimal price = 19.90m, string status = "DRAFT") => new { productId, name = "Lifetime", slug, headline = "Learn", description = "All lessons", price, currency = "BRL", status, billingType = "ONE_TIME" };
    [Fact]
    public async Task Catalog_persists_prices_and_rejects_cross_tenant_access_and_product_links()
    {
        await using var app = new WebApplicationFactory<Program>(); using var alice = await Register(app); using var bob = await Register(app);
        var a = await Org(alice); var b = await Org(bob);
        var pa = (await Json(await alice.PostAsJsonAsync($"/api/organizations/{a}/products", Product()), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var pb = (await Json(await bob.PostAsJsonAsync($"/api/organizations/{b}/products", Product()), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var slug = $"offer-{Guid.NewGuid():N}";
        var offer = await Json(await alice.PostAsJsonAsync($"/api/organizations/{a}/offers", Offer(pa, slug)), HttpStatusCode.Created);
        var id = offer.GetProperty("id").GetString(); Assert.Equal(19.90m, decimal.Parse(offer.GetProperty("price").GetString()!, System.Globalization.CultureInfo.InvariantCulture)); Assert.Equal("DRAFT", offer.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync($"/api/organizations/{a}/offers", Offer(pb, "foreign"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PatchAsJsonAsync($"/api/organizations/{a}/offers/{id}", Offer(pb, slug))).StatusCode);
        foreach (var resource in new[] { "products", "offers" })
        {
            var item = resource == "products" ? pa : id;
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/organizations/{a}/{resource}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/organizations/{a}/{resource}/{item}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/organizations/{b}/{resource}/{item}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchAsJsonAsync($"/api/organizations/{a}/{resource}/{item}", resource == "products" ? Product() : Offer(pa, slug))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsJsonAsync($"/api/organizations/{b}/offers", Offer(pb, slug))).StatusCode);
        await Json(await alice.PatchAsJsonAsync($"/api/organizations/{a}/offers/{id}", Offer(pa, slug, 29.99m, "ARCHIVED")));
        var saved = await Json(await alice.GetAsync($"/api/organizations/{a}/offers/{id}")); Assert.Equal(29.99m, decimal.Parse(saved.GetProperty("price").GetString()!, System.Globalization.CultureInfo.InvariantCulture)); Assert.Equal("ARCHIVED", saved.GetProperty("status").GetString());
        Assert.Single((await Json(await alice.GetAsync($"/api/organizations/{a}/offers"))).GetProperty("items").EnumerateArray());
        Assert.Empty((await Json(await bob.GetAsync($"/api/organizations/{b}/offers"))).GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("OPERATOR", true)]
    [InlineData("FINANCE", false)]
    [InlineData("SUPPORT", false)]
    public async Task Persisted_role_controls_catalog_writes(string role, bool allowed)
    {
        await using var app = new WebApplicationFactory<Program>(); using var owner = await Register(app); using var member = await Register(app);
        var org = await Org(owner); var email = (await Json(await member.GetAsync("/api/auth/me"))).GetProperty("email").GetString();
        await Json(await owner.PostAsJsonAsync($"/api/organizations/{org}/members", new { email, role }), HttpStatusCode.Created);
        var product = (await Json(await owner.PostAsJsonAsync($"/api/organizations/{org}/products", Product()), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/organizations/{org}/products")).StatusCode);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await member.PatchAsJsonAsync($"/api/organizations/{org}/products/{product}", Product("updated", "ACTIVE"))).StatusCode);
        Assert.Equal(allowed ? HttpStatusCode.Created : HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/organizations/{org}/offers", Offer(product, $"role-{Guid.NewGuid():N}"))).StatusCode);
    }
    [Fact]
    public async Task Validation_rejects_rounding_duplicates_invalid_fields_and_bad_pagination()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = await Register(app); var org = await Org(client); var root = $"/api/organizations/{org}";
        var product = (await Json(await client.PostAsJsonAsync($"{root}/products", Product()), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{root}/products", Product())).StatusCode);
        foreach (var price in new[] { 0m, -1m, 1.001m, 10000000000000000m }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{root}/offers", Offer(product, $"invalid-{Guid.NewGuid():N}", price))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{root}/products", Product("UPPER space"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{root}/products", Product("valid", "UNKNOWN"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{root}/products", new { name = "x", slug = "x", description = "", imageUrl = "javascript:alert(1)", status = "DRAFT" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{root}/offers", new { productId = product, name = "x", slug = "valid", price = 5m, currency = "brl", billingType = "INVALID", status = "DRAFT" })).StatusCode);
        await Json(await client.PostAsJsonAsync($"{root}/products", Product("second")), HttpStatusCode.Created);
        var page = await Json(await client.GetAsync($"{root}/products?page=2&pageSize=1")); Assert.Equal(2, page.GetProperty("total").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{root}/offers?page=2147483647&pageSize=100")).StatusCode);
    }
    [Fact]
    public async Task Database_rejects_cross_tenant_product_foreign_key_and_price_remains_exact()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = await Register(app);
        var a = await Org(client); var b = await Org(client);
        var product = (await Json(await client.PostAsJsonAsync($"/api/organizations/{a}/products", Product()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var slug = $"precise-{Guid.NewGuid():N}";
        var result = await Json(await client.PostAsJsonAsync($"/api/organizations/{a}/offers", new { productId = product, name = "Exact", slug, price = "9999999999999999.99", currency = "BRL", billingType = "ONE_TIME" }), HttpStatusCode.Created);
        Assert.Equal("9999999999999999.99", result.GetProperty("price").GetString());
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var offerId = result.GetProperty("id").GetGuid();
        Assert.Equal(9999999999999999.99m, (await db.Offers.SingleAsync(x => x.Id == offerId)).Price);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OrganizationId\" = {Guid.Parse(b)} WHERE \"Id\" = {offerId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        var audit = await db.AuditLogs.SingleAsync(x => x.TargetId == offerId && x.Action == "offer.created"); Assert.Equal(Guid.Parse(a), audit.OrganizationId);
        await Json(await client.PatchAsJsonAsync($"/api/organizations/{a}/offers/{offerId}", Offer(product.ToString(), slug, 10.25m)));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.TargetId == offerId && x.Action == "offer.price_changed"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/organizations/{a}/products", Product("trailing\n"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/organizations/{a}/offers", new { productId = product, name = "Bad", slug = "currency", price = "1.01", currency = "BRL\n", billingType = "ONE_TIME" })).StatusCode);
    }
    [Fact]
    public async Task Catalog_write_rechecks_role_after_waiting_for_organization_lock()
    {
        await using var app = new WebApplicationFactory<Program>(); using var owner = await Register(app); using var member = await Register(app);
        var org = await Org(owner); var user = await Json(await member.GetAsync("/api/auth/me"));
        await Json(await owner.PostAsJsonAsync($"/api/organizations/{org}/members", new { email = user.GetProperty("email").GetString(), role = "OPERATOR" }), HttpStatusCode.Created);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Organizations\" WHERE \"Id\" = {Guid.Parse(org)} FOR UPDATE");
        var pending = member.PostAsJsonAsync($"/api/organizations/{org}/products", Product());
        using var observerScope = app.Services.CreateScope(); var observer = observerScope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var waiting = 0;
        for (var attempt = 0; attempt < 100 && waiting == 0; attempt++)
        {
            waiting = await observer.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE cardinality(pg_blocking_pids(pid)) > 0 AND query LIKE '%Organizations%FOR UPDATE%'").SingleAsync();
            if (waiting == 0) await Task.Delay(20);
        }
        Assert.True(waiting > 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OrganizationMembers\" SET \"Role\" = 'SUPPORT' WHERE \"OrganizationId\" = {Guid.Parse(org)} AND \"UserId\" = {user.GetProperty("id").GetGuid()}");
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await pending).StatusCode);
        Assert.False(await observer.Products.AnyAsync(x => x.OrganizationId == Guid.Parse(org)));
    }
}
