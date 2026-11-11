using System.ComponentModel.DataAnnotations;
using StationeryAccounting.Models;

namespace StationeryAccounting.ViewModels;

/// <summary>Фильтр истории движения. Пустое поле — фильтр не применяется.</summary>
public class HistoryFilter
{
    [Display(Name = "Товар")]
    public int? ProductId { get; set; }

    [Display(Name = "Тип операции")]
    public OperationType? Type { get; set; }

    [Display(Name = "Сотрудник")]
    public int? EmployeeId { get; set; }

    [Display(Name = "С даты")]
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [Display(Name = "По дату")]
    [DataType(DataType.Date)]
    public DateTime? To { get; set; }
}
