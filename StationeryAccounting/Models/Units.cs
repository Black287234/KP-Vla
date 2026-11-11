namespace StationeryAccounting.Models;

/// <summary>Допустимые единицы измерения товара (для выпадающего списка).</summary>
public static class Units
{
    public static readonly string[] All = { "шт.", "пачка", "упаковка", "коробка", "набор" };

    /// <summary>
    /// Сокращение для вывода рядом с числом: «3 уп.», а не «3 упаковка».
    /// </summary>
    public static string Short(string unit)
    {
        return unit switch
        {
            "пачка" => "пач.",
            "упаковка" => "уп.",
            "коробка" => "кор.",
            "набор" => "наб.",
            _ => unit
        };
    }
}
