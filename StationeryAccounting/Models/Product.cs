using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StationeryAccounting.Models;

/// <summary>
/// Товар (канцелярская позиция) из справочника.
/// Текущий остаток здесь НЕ хранится — он всегда считается
/// по операциям: сумма поступлений минус сумма выдач.
/// </summary>
public class Product
{
    public int Id { get; set; }

    [Display(Name = "Артикул")]
    [Required(ErrorMessage = "Укажите артикул")]
    [StringLength(20, ErrorMessage = "Артикул — не длиннее 20 символов")]
    public string Article { get; set; } = string.Empty;

    [Display(Name = "Наименование")]
    [Required(ErrorMessage = "Укажите наименование")]
    [StringLength(100, ErrorMessage = "Наименование — не длиннее 100 символов")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Единица измерения")]
    [Required(ErrorMessage = "Укажите единицу измерения")]
    [StringLength(20)]
    public string Unit { get; set; } = "шт.";

    [Display(Name = "Минимальный остаток")]
    [Range(0, 100000, ErrorMessage = "Минимальный остаток должен быть от 0 до 100000")]
    public int MinQuantity { get; set; }

    // Товар не удаляем, а архивируем, чтобы не потерять историю движения
    [Display(Name = "В архиве")]
    public bool IsArchived { get; set; }

    // Навигационное свойство: все операции по этому товару (связь 1 — N).
    // JsonIgnore — чтобы API не уходило в бесконечную вложенность товар → операция → товар
    [JsonIgnore]
    public List<StockOperation> Operations { get; set; } = new();
}
