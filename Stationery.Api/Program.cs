using Stationery.Api.Domain;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Этап 1: справочник хранится в памяти процесса
var products = new List<Product>
{
    new() { Id = 1, Article = "КТ-0001", Name = "Бумага офисная А4, 500 листов", Category = ProductCategory.Paper, Unit = "пачка", MinQuantity = 20 },
    new() { Id = 2, Article = "КТ-0002", Name = "Ручка шариковая синяя", Category = ProductCategory.Writing, Unit = "шт.", MinQuantity = 50 },
    new() { Id = 3, Article = "КТ-0003", Name = "Карандаш чернографитный HB", Category = ProductCategory.Writing, Unit = "шт.", MinQuantity = 30 },
    new() { Id = 4, Article = "КТ-0004", Name = "Папка-регистратор 75 мм", Category = ProductCategory.Filing, Unit = "шт.", MinQuantity = 10 },
    new() { Id = 5, Article = "КТ-0005", Name = "Скобы для степлера № 24/6", Category = ProductCategory.DeskSupplies, Unit = "упаковка", MinQuantity = 15 },
    new() { Id = 6, Article = "КТ-0006", Name = "Картридж для лазерного принтера", Category = ProductCategory.PrinterSupplies, Unit = "шт.", MinQuantity = 3 }
};

// Одностраничный интерфейс: index.html и сценарии из wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/products", () => products.Select(p => new
{
    p.Id, p.Article, p.Name, Category = ProductCategoryNames.All[p.Category], p.Unit, p.MinQuantity
}));
api.MapGet("/products/{id:int}", (int id) =>
    products.FirstOrDefault(p => p.Id == id) is { } p
        ? Results.Ok(p)
        : Results.NotFound(new { message = $"Товар с кодом {id} не найден" }));

app.Run();
