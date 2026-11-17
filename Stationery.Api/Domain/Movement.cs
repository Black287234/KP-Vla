namespace Stationery.Api.Domain;

/// <summary>Вид операции движения товара.</summary>
public enum MovementKind
{
    Receipt,  // поступление на склад
    Issue     // выдача подразделению
}

/// <summary>
/// Операция движения товара. Остаток не хранится: он вычисляется как сумма поступлений минус сумма выдач.
/// Операции не изменяются и не удаляются, поэтому история движения всегда полная.
/// </summary>
public class Movement
{
    public int Id { get; set; }
    public MovementKind Kind { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Количество в единицах измерения товара; всегда положительное.</summary>
    public int Quantity { get; set; }

    /// <summary>Дата операции (по документу).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Для поступления — поставщик и номер накладной.</summary>
    public string? Supplier { get; set; }
    public string? DocumentNumber { get; set; }

    /// <summary>Для выдачи — подразделение и получатель.</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Recipient { get; set; }

    public string? Comment { get; set; }

    /// <summary>Момент регистрации операции в системе.</summary>
    public DateTime RegisteredAt { get; set; }
}
