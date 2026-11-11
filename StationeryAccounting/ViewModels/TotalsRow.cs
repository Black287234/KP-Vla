namespace StationeryAccounting.ViewModels;

/// <summary>Итоги по одному товару за период.</summary>
public class TotalsRow
{
    public int ProductId { get; set; }
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Received { get; set; }
    public int Issued { get; set; }
}
