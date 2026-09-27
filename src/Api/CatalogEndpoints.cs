using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class CatalogEndpoints
{
    public static void MapCatalog(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{org:guid}").RequireAuthorization();
        group.MapGet("/products", (Guid org, int? page, int? pageSize, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Products(org, UserId(user), page ?? 1, pageSize ?? 20, ct))));
        group.MapGet("/products/{id:guid}", (Guid org, Guid id, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Product(org, id, UserId(user), ct))));
        group.MapPost("/products", (Guid org, ProductRequest input, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => { var item = await service.SaveProduct(org, null, UserId(user), input, ct); return Results.Created($"/api/organizations/{org}/products/{item.Id}", item); }));
        group.MapPatch("/products/{id:guid}", (Guid org, Guid id, ProductRequest input, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.SaveProduct(org, id, UserId(user), input, ct))));
        group.MapGet("/offers", (Guid org, int? page, int? pageSize, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Offers(org, UserId(user), page ?? 1, pageSize ?? 20, ct))));
        group.MapGet("/offers/{id:guid}", (Guid org, Guid id, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Offer(org, id, UserId(user), ct))));
        group.MapPost("/offers", (Guid org, OfferRequest input, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => { var item = await service.SaveOffer(org, null, UserId(user), input, ct); return Results.Created($"/api/organizations/{org}/offers/{item.Id}", item); }));
        group.MapPatch("/offers/{id:guid}", (Guid org, Guid id, OfferRequest input, CatalogService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.SaveOffer(org, id, UserId(user), input, ct))));
    }
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
