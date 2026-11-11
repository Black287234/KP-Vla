using StationeryAccounting.Models;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Tests;

/// <summary>Справочник сотрудников и сохранение данных в базе.</summary>
public class EmployeeAndPersistenceTests : IDisposable
{
    private readonly TestDb _testDb = new();

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void СоздатьСотрудника_ПустоеФИО_Отклоняется()
    {
        using var db = _testDb.CreateContext();

        var result = new EmployeeService(db).Create(new Employee { FullName = "", Department = "ИТ-отдел" });

        Assert.False(result.Success);
        Assert.Equal("Укажите ФИО сотрудника", result.Error);
    }

    [Fact]
    public void ИзменитьСотрудника_НеизвестныйId_Отклоняется()
    {
        using var db = _testDb.CreateContext();

        var result = new EmployeeService(db).Update(new Employee { Id = 999, FullName = "Кто-то", Department = "ИТ" });

        Assert.False(result.Success);
        Assert.Equal("Сотрудник не найден", result.Error);
    }

    [Fact]
    public void Сохранение_ЗаписьЧерезСервис_ЧитаетсяНовымКонтекстом()
    {
        // Записываем в одном контексте…
        using (var db = _testDb.CreateContext())
        {
            new ProductService(db).Create(new Product { Article = "KT-050", Name = "Дырокол", Unit = "шт.", MinQuantity = 1 });
        }
        int productId;
        using (var db = _testDb.CreateContext())
        {
            productId = new ProductService(db).GetAll().Single().Id;
            var employee = new Employee { FullName = "Смирнова Ольга Петровна", Department = "Секретариат" };
            new EmployeeService(db).Create(employee);
            var stock = new StockService(db);
            stock.Receive(new ReceiptForm { ProductId = productId, Quantity = 3, Date = DateTime.Today, Supplier = "ООО «ОфисСнаб»" });
            stock.Issue(new IssueForm { ProductId = productId, Quantity = 1, Date = DateTime.Today, EmployeeId = employee.Id });
        }

        // …а читаем в совершенно новом — данные взяты из базы, а не из памяти объекта
        using (var db = _testDb.CreateContext())
        {
            var product = new ProductService(db).GetById(productId);
            Assert.NotNull(product);
            Assert.Equal("Дырокол", product!.Name);
            Assert.Equal(2, new StockService(db).GetBalance(productId));
            Assert.Equal(2, new StockService(db).GetHistory(new HistoryFilter { ProductId = productId }).Count);
        }
    }
}
