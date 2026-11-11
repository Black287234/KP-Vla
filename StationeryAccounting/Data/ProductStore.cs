using StationeryAccounting.Models;

namespace StationeryAccounting.Data;

/// <summary>
/// Временное хранилище товаров в памяти (только для этапа 1).
/// Список статический — он живёт, пока работает приложение,
/// и сбрасывается при перезапуске. На этапе 2 заменим его базой SQLite.
/// </summary>
public static class ProductStore
{
    public static readonly List<Product> Products = new()
    {
        new Product { Id = 1,  Article = "KT-001", Name = "Ручка шариковая синяя",        Unit = "шт.",      MinQuantity = 20 },
        new Product { Id = 2,  Article = "KT-002", Name = "Ручка шариковая чёрная",       Unit = "шт.",      MinQuantity = 10 },
        new Product { Id = 3,  Article = "KT-003", Name = "Карандаш чернографитный HB",   Unit = "шт.",      MinQuantity = 15 },
        new Product { Id = 4,  Article = "KT-004", Name = "Бумага офисная А4, 500 л.",    Unit = "пачка",    MinQuantity = 5 },
        new Product { Id = 5,  Article = "KT-005", Name = "Скрепки 28 мм, 100 шт.",       Unit = "упаковка", MinQuantity = 3 },
        new Product { Id = 6,  Article = "KT-006", Name = "Степлер №24/6",                Unit = "шт.",      MinQuantity = 2 },
        new Product { Id = 7,  Article = "KT-007", Name = "Скобы для степлера №24/6",     Unit = "упаковка", MinQuantity = 5 },
        new Product { Id = 8,  Article = "KT-008", Name = "Папка-скоросшиватель пластик", Unit = "шт.",      MinQuantity = 10 },
        new Product { Id = 9,  Article = "KT-009", Name = "Маркер текстовыделитель",      Unit = "шт.",      MinQuantity = 6 },
        new Product { Id = 10, Article = "KT-010", Name = "Блокнот А5, 60 л.",            Unit = "шт.",      MinQuantity = 8 },
        new Product { Id = 11, Article = "KT-011", Name = "Корректирующая лента",         Unit = "шт.",      MinQuantity = 4, IsArchived = true }
    };
}
