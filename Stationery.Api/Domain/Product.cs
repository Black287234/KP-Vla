namespace Stationery.Api.Domain;

/// <summary>Товар справочника канцелярских товаров.</summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>Артикул вида КТ-0001; уникален.</summary>
    public string Article { get; set; } = "";

    public string Name { get; set; } = "";
    public ProductCategory Category { get; set; }

    /// <summary>Единица измерения: шт., пачка, упаковка, рулон, коробка, набор.</summary>
    public string Unit { get; set; } = "шт.";

    /// <summary>Минимальный (неснижаемый) остаток; при остатке ниже этого значения товар требует пополнения.</summary>
    public int MinQuantity { get; set; }

    /// <summary>Товар выведен из ассортимента: не поступает и не выдаётся, но история сохраняется.</summary>
    public bool IsArchived { get; set; }
}
