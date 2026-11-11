using System.ComponentModel.DataAnnotations;

namespace StationeryAccounting.ViewModels;

/// <summary>Форма оформления выдачи товара сотруднику.</summary>
public class IssueForm
{
    [Display(Name = "Товар")]
    [Range(1, int.MaxValue, ErrorMessage = "Выберите товар")]
    public int ProductId { get; set; }

    [Display(Name = "Количество")]
    [Range(1, 100000, ErrorMessage = "Количество должно быть целым числом от 1 до 100000")]
    public int Quantity { get; set; }

    [Display(Name = "Дата выдачи")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    // int? — чтобы «не выбрано» отличалось от реального Id; обязательность проверяет сервис
    [Display(Name = "Сотрудник-получатель")]
    public int? EmployeeId { get; set; }

    [Display(Name = "Комментарий")]
    [StringLength(250, ErrorMessage = "Комментарий — не длиннее 250 символов")]
    public string? Comment { get; set; }
}
