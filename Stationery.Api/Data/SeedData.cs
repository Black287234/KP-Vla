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

    public static void Initialize(StockContext db, string contentRoot, DateTime today)
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
        SeedMovements(db, DateOnly.FromDateTime(today));
    }

    /// <summary>
    /// Учебные операции за последние 30 дней: поступление в начале периода и выдачи подразделениям.
    /// Количества подобраны так, чтобы часть товаров оказалась ниже минимального остатка.
    /// </summary>
    private static void SeedMovements(StockContext db, DateOnly today)
    {
        var products = db.Products.Where(p => !p.IsArchived).OrderBy(p => p.Id).ToList();
        var departments = db.Departments.OrderBy(d => d.Id).ToList();
        var start = today.AddDays(-28);
        var recipients = new[] { "Ковалёва И. П.", "Мартынова С. О.", "Широкова А. В.", "Крючков В. В.", "Абдуллаева З. Р.", "Тарасова Н. М." };
        var list = new List<Movement>();

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var received = p.MinQuantity * 3 + 10;
            list.Add(new Movement
            {
                Kind = MovementKind.Receipt, Product = p, Quantity = received, Date = start,
                Supplier = i % 2 == 0 ? "ООО «Офис-Комплект»" : "ИП Гарифуллин Р. Р.", DocumentNumber = $"ТН-{1040 + i}",
                RegisteredAt = start.ToDateTime(new TimeOnly(9, 30))
            });
            // Каждые 3 товара из 4 выдаются; у части выдано почти всё — остаток ниже минимального
            if (i % 4 == 3) continue;
            var share = i % 3 == 0 ? 0.85 : 0.45;
            var total = (int)(received * share);
            var parts = new[] { total / 2, total / 3, total - total / 2 - total / 3 };
            for (var k = 0; k < parts.Length; k++)
            {
                if (parts[k] <= 0) continue;
                var d = departments[(i + k) % departments.Count];
                var date = start.AddDays(4 + k * 8 + i % 4);
                list.Add(new Movement
                {
                    Kind = MovementKind.Issue, Product = p, Quantity = parts[k], Date = date, Department = d,
                    Recipient = recipients[(i + k) % departments.Count], RegisteredAt = date.ToDateTime(new TimeOnly(11, 0))
                });
            }
        }
        // Дополнительное поступление в середине периода
        list.Add(new Movement
        {
            Kind = MovementKind.Receipt, Product = products[0], Quantity = 40, Date = start.AddDays(14),
            Supplier = "ООО «Офис-Комплект»", DocumentNumber = "ТН-1077", RegisteredAt = start.AddDays(14).ToDateTime(new TimeOnly(10, 0))
        });
        db.Movements.AddRange(list);
        db.SaveChanges();
    }
}
