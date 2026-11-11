using System.ComponentModel.DataAnnotations;

namespace StationeryAccounting.Models;

/// <summary>Тип операции движения товара.</summary>
public enum OperationType
{
    [Display(Name = "Поступление")]
    Receipt = 1,

    [Display(Name = "Выдача")]
    Issue = 2
}
