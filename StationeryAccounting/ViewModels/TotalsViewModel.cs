using System.ComponentModel.DataAnnotations;

namespace StationeryAccounting.ViewModels;

/// <summary>Итоги за период: сколько поступило и сколько выдано.</summary>
public class TotalsViewModel
{
    [Display(Name = "С даты")]
    [DataType(DataType.Date)]
    public DateTime From { get; set; }

    [Display(Name = "По дату")]
    [DataType(DataType.Date)]
    public DateTime To { get; set; }

    public List<TotalsRow> Rows { get; set; } = new();

    public int TotalReceived => Rows.Sum(r => r.Received);
    public int TotalIssued => Rows.Sum(r => r.Issued);

    /// <summary>Ошибка в выбранном периоде (например, начало позже конца).</summary>
    public string? Error { get; set; }
}
