namespace StationeryAccounting.ViewModels;

/// <summary>Данные для страницы справочника: фильтр + найденные товары.</summary>
public class ProductListViewModel
{
    public ProductFilter Filter { get; set; } = new();
    public List<ProductListItem> Items { get; set; } = new();
}
