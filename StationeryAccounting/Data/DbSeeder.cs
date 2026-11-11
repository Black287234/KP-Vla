using StationeryAccounting.Models;

namespace StationeryAccounting.Data;

/// <summary>
/// Начальные учебные данные. Заливаются один раз — при первом запуске,
/// когда таблица товаров ещё пустая.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        // Если товары уже есть — база заполнена, повторно ничего не добавляем
        if (db.Products.Any())
        {
            return;
        }

        var products = new List<Product>
        {
            new() { Article = "KT-001", Name = "Ручка шариковая синяя",        Unit = "шт.",      MinQuantity = 20 },
            new() { Article = "KT-002", Name = "Ручка шариковая чёрная",       Unit = "шт.",      MinQuantity = 10 },
            new() { Article = "KT-003", Name = "Карандаш чернографитный HB",   Unit = "шт.",      MinQuantity = 15 },
            new() { Article = "KT-004", Name = "Бумага офисная А4, 500 л.",    Unit = "пачка",    MinQuantity = 5 },
            new() { Article = "KT-005", Name = "Скрепки 28 мм, 100 шт.",       Unit = "упаковка", MinQuantity = 3 },
            new() { Article = "KT-006", Name = "Степлер №24/6",                Unit = "шт.",      MinQuantity = 2 },
            new() { Article = "KT-007", Name = "Скобы для степлера №24/6",     Unit = "упаковка", MinQuantity = 5 },
            new() { Article = "KT-008", Name = "Папка-скоросшиватель пластик", Unit = "шт.",      MinQuantity = 10 },
            new() { Article = "KT-009", Name = "Маркер текстовыделитель",      Unit = "шт.",      MinQuantity = 6 },
            new() { Article = "KT-010", Name = "Блокнот А5, 60 л.",            Unit = "шт.",      MinQuantity = 8 },
            new() { Article = "KT-011", Name = "Корректирующая лента",         Unit = "шт.",      MinQuantity = 4, IsArchived = true },
            new() { Article = "KT-012", Name = "Клей-карандаш 21 г",           Unit = "шт.",      MinQuantity = 5 }
        };
        db.Products.AddRange(products);

        var employees = new List<Employee>
        {
            new() { FullName = "Иванова Мария Сергеевна",     Department = "Бухгалтерия" },
            new() { FullName = "Петров Алексей Викторович",   Department = "Отдел продаж" },
            new() { FullName = "Сидорова Елена Николаевна",   Department = "Отдел кадров" },
            new() { FullName = "Кузнецов Дмитрий Андреевич",  Department = "ИТ-отдел" },
            new() { FullName = "Смирнова Ольга Петровна",     Department = "Секретариат" },
            new() { FullName = "Васильев Игорь Олегович",     Department = "Юридический отдел" },
            new() { FullName = "Морозова Анна Владимировна",  Department = "Отдел продаж" },
            new() { FullName = "Николаев Сергей Иванович",    Department = "Хозяйственный отдел" }
        };
        db.Employees.AddRange(employees);

        // Сохраняем, чтобы у товаров и сотрудников появились Id
        db.SaveChanges();

        // Короткие помощники, чтобы не писать одно и то же много раз
        Product P(string article) => products.First(p => p.Article == article);
        Employee E(int index) => employees[index];

        var ops = new List<StockOperation>
        {
            // Поступления от поставщиков (1 октября 2026)
            Receipt(P("KT-001"), 100, new DateTime(2026, 10, 1), "ООО «КанцОптТорг»", "ТН-1001"),
            Receipt(P("KT-002"), 50,  new DateTime(2026, 10, 1), "ООО «КанцОптТорг»", "ТН-1001"),
            Receipt(P("KT-003"), 60,  new DateTime(2026, 10, 1), "ООО «КанцОптТорг»", "ТН-1001"),
            Receipt(P("KT-004"), 40,  new DateTime(2026, 10, 2), "ООО «Бумажный мир»", "ТН-2045"),
            Receipt(P("KT-005"), 20,  new DateTime(2026, 10, 2), "ООО «КанцОптТорг»", "ТН-1002"),
            Receipt(P("KT-006"), 6,   new DateTime(2026, 10, 2), "ООО «КанцОптТорг»", "ТН-1002"),
            Receipt(P("KT-007"), 30,  new DateTime(2026, 10, 2), "ООО «КанцОптТорг»", "ТН-1002"),
            Receipt(P("KT-008"), 50,  new DateTime(2026, 10, 5), "ООО «ОфисСнаб»", "УПД-317"),
            Receipt(P("KT-009"), 24,  new DateTime(2026, 10, 5), "ООО «ОфисСнаб»", "УПД-317"),
            Receipt(P("KT-010"), 30,  new DateTime(2026, 10, 5), "ООО «ОфисСнаб»", "УПД-317"),
            Receipt(P("KT-011"), 10,  new DateTime(2026, 10, 5), "ООО «ОфисСнаб»", "УПД-317"),

            // Выдачи сотрудникам
            Issue(P("KT-001"), 30, new DateTime(2026, 10, 7),  E(0)),
            Issue(P("KT-004"), 15, new DateTime(2026, 10, 7),  E(0)),
            Issue(P("KT-002"), 12, new DateTime(2026, 10, 9),  E(1)),
            Issue(P("KT-008"), 25, new DateTime(2026, 10, 12), E(2)),
            Issue(P("KT-003"), 20, new DateTime(2026, 10, 14), E(3)),
            Issue(P("KT-001"), 25, new DateTime(2026, 10, 16), E(4)),
            Issue(P("KT-004"), 10, new DateTime(2026, 10, 20), E(5)),
            Issue(P("KT-005"), 4,  new DateTime(2026, 10, 21), E(4)),
            Issue(P("KT-006"), 2,  new DateTime(2026, 10, 22), E(7)),
            Issue(P("KT-007"), 8,  new DateTime(2026, 10, 22), E(7)),
            Issue(P("KT-008"), 20, new DateTime(2026, 10, 27), E(6)),
            Issue(P("KT-009"), 6,  new DateTime(2026, 10, 28), E(1)),
            Issue(P("KT-010"), 10, new DateTime(2026, 11, 2),  E(2)),
            Issue(P("KT-011"), 10, new DateTime(2026, 11, 3),  E(5)),
            Issue(P("KT-001"), 30, new DateTime(2026, 11, 5),  E(6)),
            Issue(P("KT-004"), 10, new DateTime(2026, 11, 9),  E(3), "Для распечатки отчётов")
        };
        db.StockOperations.AddRange(ops);
        db.SaveChanges();
    }

    private static StockOperation Receipt(Product product, int quantity, DateTime date, string supplier, string document)
    {
        return new StockOperation
        {
            ProductId = product.Id,
            Type = OperationType.Receipt,
            Quantity = quantity,
            Date = date,
            Supplier = supplier,
            DocumentNumber = document,
            CreatedAt = date
        };
    }

    private static StockOperation Issue(Product product, int quantity, DateTime date, Employee employee, string? comment = null)
    {
        return new StockOperation
        {
            ProductId = product.Id,
            Type = OperationType.Issue,
            Quantity = quantity,
            Date = date,
            EmployeeId = employee.Id,
            Comment = comment,
            CreatedAt = date
        };
    }
}
