using StationeryAccounting.Models;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Tests;

/// <summary>
/// Выдача товара — главное правило модуля: нельзя выдать больше, чем есть на складе.
/// Исходные данные во всех тестах: ручка, поступило 10 шт., минимум 3.
/// </summary>
public class IssueTests : IDisposable
{
    private readonly TestDb _testDb = new();
    private readonly Product _pen;
    private readonly Employee _employee;

    public IssueTests()
    {
        _pen = _testDb.AddProduct("KT-001", "Ручка шариковая синяя", minQuantity: 3);
        _employee = _testDb.AddEmployee();
        _testDb.AddOperation(_pen.Id, OperationType.Receipt, 10, DateTime.Today.AddDays(-5));
    }

    public void Dispose() => _testDb.Dispose();

    private OperationResult Issue(int quantity, int? employeeId = -1, DateTime? date = null, int? productId = null)
    {
        using var db = _testDb.CreateContext();
        var service = new StockService(db);
        return service.Issue(new IssueForm
        {
            ProductId = productId ?? _pen.Id,
            Quantity = quantity,
            EmployeeId = employeeId == -1 ? _employee.Id : employeeId,
            Date = date ?? DateTime.Today
        });
    }

    private int Balance()
    {
        using var db = _testDb.CreateContext();
        return new StockService(db).GetBalance(_pen.Id);
    }

    [Fact]
    public void ВыдатьТовар_ВПределахОстатка_УспешноОстатокУменьшился()
    {
        var result = Issue(4);

        Assert.True(result.Success);
        Assert.Equal(6, Balance());
    }

    [Fact]
    public void ВыдатьТовар_БольшеОстатка_ОтклоняетсяОстатокНеИзменился()
    {
        // Пример из задания: выдать 15 ручек при остатке 10
        var result = Issue(15);

        Assert.False(result.Success);
        Assert.Contains("Недостаточно товара", result.Error);
        Assert.Equal(10, Balance());
    }

    [Fact]
    public void ВыдатьТовар_РовноВесьОстаток_ДопустимоОстатокНоль()
    {
        var result = Issue(10);

        Assert.True(result.Success);
        Assert.Equal(0, Balance());
    }

    [Fact]
    public void ВыдатьТовар_ОстатокПлюсОдин_Отклоняется()
    {
        var result = Issue(11);

        Assert.False(result.Success);
        Assert.Equal(10, Balance());
    }

    [Fact]
    public void ВыдатьТовар_БезСотрудника_Отклоняется()
    {
        var result = Issue(2, employeeId: null);

        Assert.False(result.Success);
        Assert.Equal("Для выдачи укажите сотрудника-получателя", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ВыдатьТовар_КоличествоНеПоложительное_Отклоняется(int quantity)
    {
        var result = Issue(quantity);

        Assert.False(result.Success);
        Assert.Equal("Количество должно быть целым числом больше 0", result.Error);
    }

    [Fact]
    public void ВыдатьТовар_ДатаВБудущем_Отклоняется()
    {
        var result = Issue(1, date: DateTime.Today.AddDays(1));

        Assert.False(result.Success);
        Assert.Equal("Дата операции не может быть в будущем", result.Error);
    }

    [Fact]
    public void ВыдатьТовар_АрхивныйТовар_Отклоняется()
    {
        var archived = _testDb.AddProduct("KT-099", "Старый маркер", archived: true);

        var result = Issue(1, productId: archived.Id);

        Assert.False(result.Success);
        Assert.Contains("архивным", result.Error);
    }

    [Fact]
    public void ВыдатьТовар_НеизвестныйТовар_ЗаписьНеНайдена()
    {
        var result = Issue(1, productId: 999);

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public void ВыдатьТовар_НеизвестныйСотрудник_ЗаписьНеНайдена()
    {
        var result = Issue(1, employeeId: 999);

        Assert.False(result.Success);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public void ВыдатьТовар_ОстатокОпустилсяДоМинимума_УспехСПредупреждением()
    {
        // 10 − 7 = 3, минимум 3 → «Требует пополнения», но выдача разрешена
        var result = Issue(7);

        Assert.True(result.Success);
        Assert.NotNull(result.Warning);
        Assert.Contains("требует пополнения", result.Warning);
    }

    [Fact]
    public void ВыдатьТовар_ОстатокВышеМинимума_БезПредупреждения()
    {
        // 10 − 6 = 4 > 3
        var result = Issue(6);

        Assert.True(result.Success);
        Assert.Null(result.Warning);
    }
}
