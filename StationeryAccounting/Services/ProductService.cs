using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Models;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Services;

/// <summary>
/// Справочник товаров: добавление, изменение, архивирование.
/// Контроллеры работают с базой только через этот сервис.
/// </summary>
public class ProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Все товары, отсортированные по артикулу.</summary>
    public List<Product> GetAll()
    {
        return _db.Products
            .AsNoTracking()          // только чтение — EF не нужно следить за изменениями
            .OrderBy(p => p.Article)
            .ToList();
    }

    /// <summary>
    /// Справочник с поиском и фильтрами. У каждой строки — рассчитанный остаток
    /// и признак «Требует пополнения».
    /// </summary>
    public List<ProductListItem> Search(ProductFilter filter)
    {
        var products = _db.Products.AsNoTracking().OrderBy(p => p.Article).ToList();

        // Все операции группируем по товару и считаем остаток по общей формуле
        var balances = _db.StockOperations
            .AsNoTracking()
            .ToList()
            .GroupBy(o => o.ProductId)
            .ToDictionary(g => g.Key, g => StockCalculator.CalculateBalance(g));

        var items = products.Select(p =>
        {
            int balance = balances.TryGetValue(p.Id, out var b) ? b : 0;  // нет операций — остаток 0
            return new ProductListItem
            {
                Id = p.Id,
                Article = p.Article,
                Name = p.Name,
                Unit = p.Unit,
                MinQuantity = p.MinQuantity,
                IsArchived = p.IsArchived,
                Balance = balance,
                NeedsRestock = StockCalculator.NeedsRestock(balance, p.MinQuantity)
            };
        });

        // Поиск по наименованию или артикулу без учёта регистра.
        // Делаем его в памяти: в SQLite сравнение без учёта регистра
        // работает только для латиницы, а наименования у нас русские.
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string text = filter.Search.Trim();
            items = items.Where(i =>
                i.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                i.Article.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (!filter.ShowArchived)
        {
            items = items.Where(i => !i.IsArchived);
        }

        if (filter.BelowMinOnly)
        {
            // Архивные товары не пополняют, поэтому в «Ниже минимума» их не показываем
            items = items.Where(i => i.NeedsRestock && !i.IsArchived);
        }

        return items.ToList();
    }

    /// <summary>Один товар по Id или null, если такого нет.</summary>
    public Product? GetById(int id)
    {
        return _db.Products.AsNoTracking().FirstOrDefault(p => p.Id == id);
    }

    /// <summary>Добавить новый товар.</summary>
    public OperationResult Create(Product product)
    {
        Normalize(product);

        var error = Validate(product);
        if (error != null)
        {
            return OperationResult.Fail(error);
        }

        // Артикул должен быть уникальным
        if (_db.Products.Any(p => p.Article == product.Article))
        {
            return OperationResult.Fail($"Товар с артикулом «{product.Article}» уже существует");
        }

        product.Id = 0;              // Id назначит база
        product.IsArchived = false;  // новый товар всегда активный
        _db.Products.Add(product);
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    /// <summary>Изменить основные данные товара.</summary>
    public OperationResult Update(Product product)
    {
        var existing = _db.Products.FirstOrDefault(p => p.Id == product.Id);
        if (existing == null)
        {
            return OperationResult.Fail("Товар не найден");
        }

        Normalize(product);

        var error = Validate(product);
        if (error != null)
        {
            return OperationResult.Fail(error);
        }

        // Уникальность артикула проверяем среди ДРУГИХ товаров
        if (_db.Products.Any(p => p.Article == product.Article && p.Id != product.Id))
        {
            return OperationResult.Fail($"Товар с артикулом «{product.Article}» уже существует");
        }

        // Переносим только разрешённые поля. Признак архива меняется отдельными действиями
        existing.Article = product.Article;
        existing.Name = product.Name;
        existing.Unit = product.Unit;
        existing.MinQuantity = product.MinQuantity;
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Отправить товар в архив. Удалять нельзя — пропадёт история движения.
    /// </summary>
    public OperationResult Archive(int id)
    {
        var product = _db.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return OperationResult.Fail("Товар не найден");
        }
        if (product.IsArchived)
        {
            return OperationResult.Fail("Товар уже находится в архиве");
        }

        product.IsArchived = true;
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    /// <summary>Вернуть товар из архива.</summary>
    public OperationResult Restore(int id)
    {
        var product = _db.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return OperationResult.Fail("Товар не найден");
        }
        if (!product.IsArchived)
        {
            return OperationResult.Fail("Товар не находится в архиве");
        }

        product.IsArchived = false;
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    // Убираем лишние пробелы; артикул приводим к верхнему регистру,
    // чтобы «kt-001» и «KT-001» считались одинаковыми
    private static void Normalize(Product product)
    {
        product.Article = (product.Article ?? string.Empty).Trim().ToUpperInvariant();
        product.Name = (product.Name ?? string.Empty).Trim();
        product.Unit = (product.Unit ?? string.Empty).Trim();
    }

    // Проверки на уровне сервиса дублируют атрибуты модели: сервис можно
    // вызвать и без формы (из API или теста), и правила всё равно сработают
    private static string? Validate(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Article))
            return "Укажите артикул";
        if (product.Article.Length > 20)
            return "Артикул — не длиннее 20 символов";
        if (string.IsNullOrWhiteSpace(product.Name))
            return "Укажите наименование";
        if (product.Name.Length > 100)
            return "Наименование — не длиннее 100 символов";
        if (!Units.All.Contains(product.Unit))
            return "Выберите единицу измерения из списка";
        if (product.MinQuantity < 0)
            return "Минимальный остаток не может быть отрицательным";
        return null;
    }
}
