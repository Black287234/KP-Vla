namespace Stationery.Api.Domain;

/// <summary>Подразделение колледжа — получатель канцелярских товаров.</summary>
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Руководитель подразделения (ФИО).</summary>
    public string Head { get; set; } = "";
}
