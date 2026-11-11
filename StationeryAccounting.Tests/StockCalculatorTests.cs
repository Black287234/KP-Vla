using StationeryAccounting.Models;
using StationeryAccounting.Services;

namespace StationeryAccounting.Tests;

/// <summary>Расчёт остатка и контроль минимума — без базы данных.</summary>
public class StockCalculatorTests
{
    private static StockOperation Op(OperationType type, int quantity) =>
        new() { Type = type, Quantity = quantity };

    [Fact]
    public void РасчетОстатка_ПоступленияИВыдачи_РавенРазнице()
    {
        // 100 + 20 поступило, 30 + 25 выдано → 65
        var ops = new[]
        {
            Op(OperationType.Receipt, 100), Op(OperationType.Receipt, 20),
            Op(OperationType.Issue, 30), Op(OperationType.Issue, 25)
        };

        Assert.Equal(65, StockCalculator.CalculateBalance(ops));
    }

    [Fact]
    public void РасчетОстатка_НетОпераций_РавенНулю()
    {
        Assert.Equal(0, StockCalculator.CalculateBalance(Array.Empty<StockOperation>()));
    }

    [Fact]
    public void РасчетОстатка_ВыданоВсё_РавенНулю()
    {
        var ops = new[] { Op(OperationType.Receipt, 10), Op(OperationType.Issue, 10) };

        Assert.Equal(0, StockCalculator.CalculateBalance(ops));
    }

    [Theory]
    [InlineData(4, 5, true)]    // меньше минимума
    [InlineData(5, 5, true)]    // ровно минимум — уже «Требует пополнения»
    [InlineData(6, 5, false)]   // больше минимума
    [InlineData(0, 0, true)]    // минимум 0, остатка нет
    public void КонтрольМинимума_РазныеОстатки_ВерныйПризнак(int balance, int min, bool expected)
    {
        Assert.Equal(expected, StockCalculator.NeedsRestock(balance, min));
    }
}
