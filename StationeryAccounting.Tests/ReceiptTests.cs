using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Tests;

/// <summary>Поступление товара на склад.</summary>
public class ReceiptTests : IDisposable
{
    private readonly TestDb _testDb = new();

    public void Dispose() => _testDb.Dispose();

    private OperationResult Receive(ReceiptForm form)
    {
        using var db = _testDb.CreateContext();
        return new StockService(db).Receive(form);
    }

    [Fact]
    public void Поступление_КорректныеДанные_ОстатокУвеличился()
    {
        var paper = _testDb.AddProduct("KT-004", "Бумага А4");

        var result = Receive(new ReceiptForm
        {
            ProductId = paper.Id, Quantity = 40, Date = DateTime.Today, Supplier = "ООО «Бумажный мир»"
        });

        using var db = _testDb.CreateContext();
        Assert.True(result.Success);
        Assert.Equal(40, new StockService(db).GetBalance(paper.Id));
    }

    [Fact]
    public void Поступление_БезПоставщикаИДокумента_Отклоняется()
    {
        var paper = _testDb.AddProduct("KT-004", "Бумага А4");

        var result = Receive(new ReceiptForm { ProductId = paper.Id, Quantity = 5, Date = DateTime.Today, Supplier = "   " });

        Assert.False(result.Success);
        Assert.Equal("Для поступления укажите поставщика или номер документа", result.Error);
    }

    [Fact]
    public void Поступление_ТолькоНомерДокумента_Допустимо()
    {
        var paper = _testDb.AddProduct("KT-004", "Бумага А4");

        var result = Receive(new ReceiptForm { ProductId = paper.Id, Quantity = 5, Date = DateTime.Today, DocumentNumber = "ТН-77" });

        Assert.True(result.Success);
    }

    [Fact]
    public void Поступление_НаАрхивныйТовар_Отклоняется()
    {
        var archived = _testDb.AddProduct("KT-011", "Корректирующая лента", archived: true);

        var result = Receive(new ReceiptForm { ProductId = archived.Id, Quantity = 5, Date = DateTime.Today, Supplier = "ООО «Тест»" });

        Assert.False(result.Success);
        Assert.Contains("архивным", result.Error);
    }

    [Fact]
    public void Поступление_КоличествоНоль_Отклоняется()
    {
        var paper = _testDb.AddProduct("KT-004", "Бумага А4");

        var result = Receive(new ReceiptForm { ProductId = paper.Id, Quantity = 0, Date = DateTime.Today, Supplier = "ООО «Тест»" });

        Assert.False(result.Success);
    }

    [Fact]
    public void Поступление_НеизвестныйТовар_ЗаписьНеНайдена()
    {
        var result = Receive(new ReceiptForm { ProductId = 999, Quantity = 1, Date = DateTime.Today, Supplier = "ООО «Тест»" });

        Assert.True(result.IsNotFound);
    }
}
