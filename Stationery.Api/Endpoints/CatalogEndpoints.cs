using Stationery.Api.Domain;
using Stationery.Api.Infrastructure;
using Stationery.Api.Services;

namespace Stationery.Api.Endpoints;

/// <summary>Конечные точки справочников: /api/products, /api/departments, /api/categories.</summary>
public static class CatalogEndpoints
{
    public static void MapCatalog(this RouteGroupBuilder api)
    {
        api.MapGet("/categories", () => ProductCategoryNames.All.Select(c => new { code = c.Key.ToString(), name = c.Value }));

        var products = api.MapGroup("/products");
        products.MapGet("/", (CatalogService s, string? search, ProductCategory? category, bool? archived) =>
            s.Products(search, category, archived ?? false).Select(ToJson));
        products.MapGet("/{id:int}", (int id, CatalogService s) => ToJson(s.Product(id)));
        products.MapPost("/", (ProductForm form, CatalogService s) =>
        {
            var p = s.CreateProduct(form);
            return Results.Created($"/api/products/{p.Id}", ToJson(p));
        }).Validate<ProductForm>();
        products.MapPut("/{id:int}", (int id, ProductForm form, CatalogService s) => ToJson(s.UpdateProduct(id, form)))
            .Validate<ProductForm>();
        products.MapPost("/{id:int}/archive", (int id, CatalogService s) => ToJson(s.SetArchived(id, true)));
        products.MapPost("/{id:int}/restore", (int id, CatalogService s) => ToJson(s.SetArchived(id, false)));

        var departments = api.MapGroup("/departments");
        departments.MapGet("/", (CatalogService s) => s.Departments());
        departments.MapPost("/", (DepartmentForm form, CatalogService s) =>
        {
            var d = s.CreateDepartment(form);
            return Results.Created($"/api/departments/{d.Id}", d);
        }).Validate<DepartmentForm>();
    }

    public static object ToJson(Product p) => new
    {
        p.Id, p.Article, p.Name, Category = p.Category.ToString(), CategoryName = ProductCategoryNames.All[p.Category],
        p.Unit, p.MinQuantity, p.IsArchived
    };
}
