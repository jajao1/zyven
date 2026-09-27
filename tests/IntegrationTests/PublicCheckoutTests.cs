using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace IntegrationTests;

public class PublicCheckoutTests
{
    private static async Task<(HttpClient Client, Guid Org, Guid Offer, string Slug)> Fixture(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new() { HandleCookies = false }); client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var auth = await client.PostAsJsonAsync("/api/auth/register", new { email = $"public-{Guid.NewGuid():N}@example.test", password = "secure long passphrase", displayName = "Seller" }); auth.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await auth.Content.ReadFromJsonAsync<Zyven.Application.AuthResponse>())!.AccessToken);
        var org = (await (await client.PostAsJsonAsync("/api/organizations", new { name = "Public studio" })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var product = await (await client.PostAsJsonAsync($"/api/organizations/{org}/products", new { name = "Course", slug = "course", status = "DRAFT" })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var productId = product.GetProperty("id").GetGuid();
        (await client.PatchAsJsonAsync($"/api/organizations/{org}/products/{productId}", new { name = "Course", slug = "course", status = "ACTIVE" })).EnsureSuccessStatusCode();
        var slug = $"public-{Guid.NewGuid():N}";
        object Input(string status) => new { productId, name = "Public course", slug, price = "19.90", currency = "BRL", status, billingType = "ONE_TIME" };
        var offer = await (await client.PostAsJsonAsync($"/api/organizations/{org}/offers", Input("DRAFT"))).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var id = offer.GetProperty("id").GetGuid();
        (await client.PatchAsJsonAsync($"/api/organizations/{org}/offers/{id}", Input("ACTIVE"))).EnsureSuccessStatusCode();
        return (client, org, id, slug);
    }
    private static object Buyer => new { name = "Buyer Name", email = "buyer@example.test", phone = "+55 11 99999-0000", document = "", fields = new { }, price = "0.01", organizationId = Guid.NewGuid() };
    [Fact]
    public async Task Public_page_excludes_internal_data_and_checkout_uses_server_price_and_secret_cookie()
    {
        await using var app = new WebApplicationFactory<Program>(); var fixture = await Fixture(app); using var owner = fixture.Client; using var client = app.CreateClient(new() { HandleCookies = false }); client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var page = await client.GetAsync($"/api/public/offers/{fixture.Slug}"); page.EnsureSuccessStatusCode(); var json = await page.Content.ReadAsStringAsync(); Assert.DoesNotContain("organizationId", json); Assert.DoesNotContain("productId", json); Assert.DoesNotContain("accessHash", json);
        var created = await client.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", Buyer); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var checkout = await created.Content.ReadFromJsonAsync<Zyven.Application.CheckoutResponse>(); Assert.Equal("19.90", checkout!.Price); Assert.Equal("CREATED", checkout.Status);
        var cookie = created.Headers.GetValues("Set-Cookie").Single(); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie); Assert.Contains($"path=/api/public/checkouts/{checkout.Id}", cookie);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/public/checkouts/{checkout.Id}")).StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        var read = await client.GetAsync($"/api/public/checkouts/{checkout.Id}"); read.EnsureSuccessStatusCode(); Assert.Contains("buyer@example.test", await read.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/public/checkouts/{Guid.NewGuid()}")).StatusCode);
        (await client.PatchAsJsonAsync($"/api/public/checkouts/{checkout.Id}", new { name = "Updated buyer", email = "updated@example.test" })).EnsureSuccessStatusCode();
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(app.Services); var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Zyven.Infrastructure.ZyvenDbContext>(scope.ServiceProvider);
        var stored = await db.Checkouts.FindAsync(checkout.Id); Assert.Equal(fixture.Org, stored!.OrganizationId); Assert.Equal("Updated buyer", stored.Name); Assert.DoesNotContain(stored.AccessHash, cookie);
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/api/public/checkouts/{checkout.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await client.PatchAsJsonAsync($"/api/public/checkouts/{checkout.Id}", Buyer)).StatusCode);
        var job = new Zyven.Infrastructure.CheckoutExpiration(db, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<Zyven.Infrastructure.CheckoutExpiration>.Instance); await job.Run(default); await db.Entry(stored).ReloadAsync(); Assert.Equal("EXPIRED", stored.Status);
    }
    [Fact]
    public async Task Page_editor_is_tenant_scoped_validates_media_and_required_checkout_fields()
    {
        await using var app = new WebApplicationFactory<Program>(); var fixture = await Fixture(app); using var owner = fixture.Client; var other = await Fixture(app); using var outsider = other.Client;
        var path = $"/api/organizations/{fixture.Org}/offers/{fixture.Offer}/page";
        var page = new Zyven.Application.PageContent { Title = "Safe title", Fields = [new("company", "Company", "text", true)], Benefits = ["Learn"], Faq = [new("Question", "Answer")] };
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync(path, page)).StatusCode);
        var email = (await (await outsider.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("email").GetString();
        (await owner.PostAsJsonAsync($"/api/organizations/{fixture.Org}/members", new { email, role = "FINANCE" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PutAsJsonAsync(path, page)).StatusCode);
        foreach (var url in new[] { "javascript:alert(1)", "data:text/html,bad", "https://user:password@example.com/a", "http://example.com/a" }) Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, page with { VideoUrl = url })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, page with { Fields = [new("bad key", "Label", "script", false)] })).StatusCode);
        (await owner.PutAsJsonAsync(path, page)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", Buyer)).StatusCode);
        var created = await owner.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Valid Buyer", email = "buyer@example.test", fields = new { company = "Company" } }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(app.Services); var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Zyven.Infrastructure.ZyvenDbContext>(scope.ServiceProvider);
        var offer = await db.Offers.FindAsync(fixture.Offer); offer!.Status = "INACTIVE"; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/public/offers/{fixture.Slug}")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", Buyer)).StatusCode);
        offer.Status = "ACTIVE"; var product = await db.Products.FindAsync(offer.ProductId); product!.Status = "DRAFT"; await db.SaveChangesAsync(); Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/public/offers/{fixture.Slug}")).StatusCode);
    }
    [Fact]
    public async Task Public_mutations_require_browser_header_and_trusted_origin()
    {
        await using var app = new WebApplicationFactory<Program>(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/public/offers/missing/checkouts", new { })).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/public/offers/missing/checkouts", new { })).StatusCode);
    }
}
