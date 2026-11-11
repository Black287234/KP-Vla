using StationeryAccounting.Models;

namespace StationeryAccounting.ViewModels;

/// <summary>Карточка товара: сам товар, его остаток и история движения именно по нему.</summary>
public class ProductCardViewModel
{
    public Product Product { get; set; } = null!;
    public int Balance { get; set; }
    public bool NeedsRestock { get; set; }
    public List<StockOperation> History { get; set; } = new();
}
