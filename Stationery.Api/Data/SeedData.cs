using System.Text.Json;
using Stationery.Api.Domain;

namespace Stationery.Api.Data;

/// <summary>
/// Создание базы данных и загрузка начальных данных из файла Data/seed.json.
/// Структура базы создаётся по модели EF Core методом EnsureCreated.
/// </summary>
public static class SeedData
{
    private record SeedFile(List<SeedDepartment> Departments, List<SeedProduct> Products);
    private record SeedDepartment(string Name, string Head);
    private record SeedProduct(string Article, string Name, ProductCategory Category, string Unit, int Min, bool Archived = false);

    public static void Initialize(StockContext db, string contentRoot)
    {
        db.Database.EnsureCreated();
        if (db.Products.Any()) return;

        var path = Path.Combine(contentRoot, "Data", "seed.json");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var seed = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), options)!;

        db.Departments.AddRange(seed.Departments.Select(d => new Department { Name = d.Name, Head = d.Head }));
        db.Products.AddRange(seed.Products.Select(p => new Product
        {
            Article = p.Article, Name = p.Name, Category = p.Category, Unit = p.Unit, MinQuantity = p.Min, IsArchived = p.Archived
        }));
        db.SaveChanges();
    }
}
