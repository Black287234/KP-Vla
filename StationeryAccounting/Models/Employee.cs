using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StationeryAccounting.Models;

/// <summary>Сотрудник, которому выдаются канцтовары.</summary>
public class Employee
{
    public int Id { get; set; }

    [Display(Name = "ФИО")]
    [Required(ErrorMessage = "Укажите ФИО сотрудника")]
    [StringLength(100, ErrorMessage = "ФИО — не длиннее 100 символов")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Отдел")]
    [Required(ErrorMessage = "Укажите отдел")]
    [StringLength(100, ErrorMessage = "Название отдела — не длиннее 100 символов")]
    public string Department { get; set; } = string.Empty;

    // Все выдачи этому сотруднику (связь 1 — N)
    [JsonIgnore]
    public List<StockOperation> Operations { get; set; } = new();
}
