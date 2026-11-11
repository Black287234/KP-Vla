namespace StationeryAccounting.ViewModels;

/// <summary>Строка справочника товаров: данные товара + рассчитанный остаток.</summary>
public class ProductListItem
{
    public int Id { get; set; }
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int MinQuantity { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>Текущий остаток (считается, а не хранится).</summary>
    public int Balance { get; set; }

    /// <summary>Остаток ≤ минимального — «Требует пополнения».</summary>
    public bool NeedsRestock { get; set; }
}
