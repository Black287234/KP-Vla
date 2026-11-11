using StationeryAccounting.Data;
using StationeryAccounting.Models;

namespace StationeryAccounting.Services;

/// <summary>
/// Сервис работы с товарами. Контроллеры обращаются к данным только через него,
/// поэтому при переходе на базу данных (этап 2) поменяется только этот класс.
/// </summary>
public class ProductService
{
    /// <summary>Все товары, отсортированные по артикулу.</summary>
    public List<Product> GetAll()
    {
        return ProductStore.Products
            .OrderBy(p => p.Article)
            .ToList();
    }

    /// <summary>Один товар по Id или null, если такого нет.</summary>
    public Product? GetById(int id)
    {
        // FirstOrDefault вернёт null, если ничего не найдено, — без исключения
        return ProductStore.Products.FirstOrDefault(p => p.Id == id);
    }
}
