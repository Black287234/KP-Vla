using StationeryAccounting.Models;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Tests;

/// <summary>История движения с фильтрами и итоги за период.</summary>
public class HistoryAndTotalsTests : IDisposable
{
    private readonly TestDb _testDb = new();
    private readonly Product _pen;
    private readonly Product _paper;
    private readonly Employee _ivanova;
    private readonly Employee _petrov;

    // Исходные данные: октябрь 2026, по две операции на каждый товар
    public HistoryAndTotalsTests()
    {
        _pen = _testDb.AddProduct("KT-001", "Ручка");
        _paper = _testDb.AddProduct("KT-004", "Бумага А4");
        _ivanova = _testDb.AddEmployee("Иванова Мария Сергеевна");
        _petrov = _testDb.AddEmployee("Петров Алексей Викторович", "Отдел продаж");

        _testDb.AddOperation(_pen.Id, OperationType.Receipt, 100, new DateTime(2026, 10, 1));
        _testDb.AddOperation(_paper.Id, OperationType.Receipt, 40, new DateTime(2026, 10, 2));
        _testDb.AddOperation(_pen.Id, OperationType.Issue, 30, new DateTime(2026, 10, 10), _ivanova.Id);
        _testDb.AddOperation(_paper.Id, OperationType.Issue, 15, new DateTime(2026, 10, 31), _petrov.Id);
    }

    public void Dispose() => _testDb.Dispose();

    private List<StockOperation> History(HistoryFilter filter)
    {
        using var db = _testDb.CreateContext();
        return new StockService(db).GetHistory(filter);
    }

    [Fact]
    public void История_БезФильтра_ВсеОперацииНовыеСверху()
    {
        var all = History(new HistoryFilter());

        Assert.Equal(4, all.Count);
        Assert.Equal(new DateTime(2026, 10, 31), all[0].Date);
    }

    [Fact]
    public void История_ФильтрПоТоваруИТипу_ТолькоВыдачиРучки()
    {
        var found = History(new HistoryFilter { ProductId = _pen.Id, Type = OperationType.Issue });

        var op = Assert.Single(found);
        Assert.Equal(30, op.Quantity);
    }

    [Fact]
    public void История_ФильтрПоСотруднику_ТолькоЕгоВыдачи()
    {
        var found = History(new HistoryFilter { EmployeeId = _petrov.Id });

        Assert.Equal(_paper.Id, Assert.Single(found).ProductId);
    }

    [Fact]
    public void История_ФильтрПоПериоду_ГраницыВключаются()
    {
        // Период ровно с 2 по 10 октября — обе граничные даты должны попасть
        var found = History(new HistoryFilter { From = new DateTime(2026, 10, 2), To = new DateTime(2026, 10, 10) });

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void ИтогиЗаПериод_Октябрь_СуммыПоТоварамВерны()
    {
        using var db = _testDb.CreateContext();
        var rows = new StockService(db).GetTotals(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        var pen = rows.Single(r => r.ProductId == _pen.Id);
        var paper = rows.Single(r => r.ProductId == _paper.Id);
        Assert.Equal((100, 30), (pen.Received, pen.Issued));
        Assert.Equal((40, 15), (paper.Received, paper.Issued));
    }

    [Fact]
    public void ИтогиЗаПериод_ПериодБезОпераций_ПустойСписок()
    {
        using var db = _testDb.CreateContext();

        var rows = new StockService(db).GetTotals(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));

        Assert.Empty(rows);
    }
}
