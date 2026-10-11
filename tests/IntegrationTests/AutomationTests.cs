using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Zyven.Application;
namespace IntegrationTests;

public class AutomationTests
{
    private static async Task<HttpClient> Register(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new() { HandleCookies = false }); client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"automation-{Guid.NewGuid():N}@example.com", password = "automation secure password", displayName = "Seller" }); response.EnsureSuccessStatusCode(); client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken); return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK) { var body = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {body}"); return JsonSerializer.Deserialize<JsonElement>(body); }
    private static async Task<(string Org, string Offer)> Seed(HttpClient client)
    {
        var org = (await Json(await client.PostAsJsonAsync("/api/organizations", new { name = "Automation studio" }), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var product = (await Json(await client.PostAsJsonAsync($"/api/organizations/{org}/products", new { name = "Course", slug = $"course-{Guid.NewGuid():N}", description = "Course", imageUrl = "https://example.com/course.png", status = "DRAFT" }), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var offer = (await Json(await client.PostAsJsonAsync($"/api/organizations/{org}/offers", new { productId = product, name = "Offer", slug = $"offer-{Guid.NewGuid():N}", headline = "Offer", description = "Offer", price = 10m, currency = "BRL", billingType = "ONE_TIME", status = "DRAFT" }), HttpStatusCode.Created)).GetProperty("id").GetString()!; return (org, offer);
    }
    [Fact]
    public async Task Draft_is_tenant_scoped_validated_and_published_as_immutable_version()
    {
        await using var app = new WebApplicationFactory<Program>(); using var owner = await Register(app); using var stranger = await Register(app); var seeded = await Seed(owner);
        var path = $"/api/organizations/{seeded.Org}/offers/{seeded.Offer}/automation";
        var draft = new { steps = new object[] { new { type = "SEND_TELEGRAM_MESSAGE", text = "Olá, {{buyer_name}}" }, new { type = "WAIT", waitMinutes = 5 }, new { type = "TELEGRAM_ACCESS" }, new { type = "END" } } };
        var saved = await Json(await owner.PutAsJsonAsync(path, draft)); Assert.Equal(4, saved.GetProperty("steps").GetArrayLength()); Assert.Equal(0, saved.GetProperty("publishedVersion").GetInt32());
        var published = await Json(await owner.PostAsync($"{path}/publish", null)); Assert.Equal(1, published.GetProperty("publishedVersion").GetInt32());
        await Json(await owner.PutAsJsonAsync(path, new { steps = new object[] { new { type = "SEND_TELEGRAM_MESSAGE", text = "Versão seguinte" }, new { type = "END" } } }));
        published = await Json(await owner.PostAsync($"{path}/publish", null)); Assert.Equal(2, published.GetProperty("publishedVersion").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, new { steps = new object[] { new { type = "SEND_TELEGRAM_MESSAGE", text = "Sem encerramento" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, new { steps = new object[] { new { type = "SEND_TELEGRAM_MESSAGE", text = "{{secret}}" }, new { type = "END" } } })).StatusCode);
    }
}
