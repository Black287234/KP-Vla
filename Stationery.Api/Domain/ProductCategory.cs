namespace Stationery.Api.Domain;

/// <summary>Группа товаров.</summary>
public enum ProductCategory
{
    Paper,          // Бумага и бумажные изделия
    Writing,        // Письменные принадлежности
    Filing,         // Папки, файлы, системы хранения
    DeskSupplies,   // Настольные принадлежности
    PrinterSupplies // Расходные материалы для оргтехники
}

public static class ProductCategoryNames
{
    public static readonly IReadOnlyDictionary<ProductCategory, string> All = new Dictionary<ProductCategory, string>
    {
        [ProductCategory.Paper] = "Бумага и бумажные изделия",
        [ProductCategory.Writing] = "Письменные принадлежности",
        [ProductCategory.Filing] = "Папки и системы хранения",
        [ProductCategory.DeskSupplies] = "Настольные принадлежности",
        [ProductCategory.PrinterSupplies] = "Расходные материалы для оргтехники"
    };
}
