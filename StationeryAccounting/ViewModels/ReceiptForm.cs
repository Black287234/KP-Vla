using System.ComponentModel.DataAnnotations;

namespace StationeryAccounting.ViewModels;

/// <summary>Форма оформления поступления товара.</summary>
public class ReceiptForm
{
    [Display(Name = "Товар")]
    [Range(1, int.MaxValue, ErrorMessage = "Выберите товар")]
    public int ProductId { get; set; }

    [Display(Name = "Количество")]
    [Range(1, 100000, ErrorMessage = "Количество должно быть целым числом от 1 до 100000")]
    public int Quantity { get; set; }

    [Display(Name = "Дата поступления")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Поставщик")]
    [StringLength(100, ErrorMessage = "Поставщик — не длиннее 100 символов")]
    public string? Supplier { get; set; }

    [Display(Name = "Номер документа")]
    [StringLength(50, ErrorMessage = "Номер документа — не длиннее 50 символов")]
    public string? DocumentNumber { get; set; }

    [Display(Name = "Комментарий")]
    [StringLength(250, ErrorMessage = "Комментарий — не длиннее 250 символов")]
    public string? Comment { get; set; }
}
