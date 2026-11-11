using StationeryAccounting.Models;

namespace StationeryAccounting.ViewModels;

/// <summary>Страница истории: фильтр, найденные операции и суммы по ним.</summary>
public class HistoryViewModel
{
    public HistoryFilter Filter { get; set; } = new();
    public List<StockOperation> Operations { get; set; } = new();

    /// <summary>Сколько поступило по найденным операциям.</summary>
    public int TotalReceived => Operations.Where(o => o.Type == OperationType.Receipt).Sum(o => o.Quantity);

    /// <summary>Сколько выдано по найденным операциям.</summary>
    public int TotalIssued => Operations.Where(o => o.Type == OperationType.Issue).Sum(o => o.Quantity);
}
