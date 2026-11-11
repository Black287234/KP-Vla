using System.ComponentModel.DataAnnotations;

namespace StationeryAccounting.ViewModels;

/// <summary>Параметры поиска в справочнике товаров (приходят из строки адреса).</summary>
public class ProductFilter
{
    [Display(Name = "Поиск")]
    public string? Search { get; set; }

    [Display(Name = "Только ниже минимума")]
    public bool BelowMinOnly { get; set; }

    [Display(Name = "Показывать архивные")]
    public bool ShowArchived { get; set; }
}
