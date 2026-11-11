using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StationeryAccounting.Models;

/// <summary>
/// Операция движения товара: поступление или выдача.
/// Операции не редактируются и не удаляются — иначе сломается история и остаток.
/// Ошибку исправляют новой (корректирующей) операцией с комментарием.
/// </summary>
public class StockOperation
{
    public int Id { get; set; }

    [Display(Name = "Товар")]
    public int ProductId { get; set; }

    [JsonIgnore]
    public Product? Product { get; set; }

    [Display(Name = "Тип операции")]
    public OperationType Type { get; set; }

    [Display(Name = "Количество")]
    public int Quantity { get; set; }

    [Display(Name = "Дата операции")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    // Получатель — обязателен только для выдачи, поэтому int? (может быть пустым)
    [Display(Name = "Сотрудник")]
    public int? EmployeeId { get; set; }

    [JsonIgnore]
    public Employee? Employee { get; set; }

    [Display(Name = "Поставщик")]
    [StringLength(100)]
    public string? Supplier { get; set; }

    [Display(Name = "Номер документа")]
    [StringLength(50)]
    public string? DocumentNumber { get; set; }

    [Display(Name = "Комментарий")]
    [StringLength(250)]
    public string? Comment { get; set; }

    // Когда запись внесена в систему (служебное поле)
    [Display(Name = "Создано")]
    public DateTime CreatedAt { get; set; }
}
