using Stationery.Api.Domain;
using Stationery.Api.Infrastructure;
using Stationery.Api.Services;

namespace Stationery.Tests;

/// <summary>Модульные тесты сервиса движения товаров (хранилища в оперативной памяти).</summary>
[TestClass]
public class StockServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private MemoryStore _store = null!;
    private StockService _stock = null!;
    private Product _paper = null!;

    [TestInitialize]
    public void Prepare()
    {
        _store = new MemoryStore();
        _paper = new Product { Article = "КТ-0001", Name = "Бумага А4", Category = ProductCategory.Paper, Unit = "пачка", MinQuantity = 20 };
        _store.Add(_paper);
        _store.Add(new Department { Name = "Учебная часть", Head = "Ковалёва И. П." });
        _stock = new StockService(_store, _store, _store, new FixedClock(Today.ToDateTime(new TimeOnly(10, 0))));
    }

    private Movement Receive(int quantity, int daysAgo = 0) =>
        _stock.Receive(new ReceiptForm(_paper.Id, quantity, Today.AddDays(-daysAgo), "ООО «Офис-Комплект»", "ТН-1", null));

    private Movement Issue(int quantity, int daysAgo = 0) =>
        _stock.Issue(new IssueForm(_paper.Id, quantity, Today.AddDays(-daysAgo), 1, "Ковалёва И. П.", null));

    private static AppException Fails(Action action) => Assert.ThrowsException<AppException>(action);

    [TestMethod, Description("Остаток равен сумме поступлений минус сумма выдач")]
    public void Balance_IsReceiptsMinusIssues()
    {
        Receive(50, 5);
        Receive(10, 2);
        Issue(15, 1);
        var line = _stock.Stock().Single();
        Assert.AreEqual(60, line.Received);
        Assert.AreEqual(15, line.Issued);
        Assert.AreEqual(45, line.Quantity);
    }

    [TestMethod, Description("Выдача больше остатка отклоняется с кодом 409, операция не сохраняется")]
    public void Issue_MoreThanBalance_Rejected()
    {
        Receive(18);
        var ex = Fails(() => Issue(50));
        Assert.AreEqual(409, ex.Status);
        Assert.AreEqual("Недостаточно товара «Бумага А4»: на складе 18 пачка, запрошено 50 пачка", ex.Message);
        Assert.AreEqual(1, _store.MovementList.Count);
    }

    [TestMethod, Description("Можно выдать ровно весь остаток")]
    public void Issue_WholeBalance_Allowed()
    {
        Receive(18);
        Issue(18);
        Assert.AreEqual(0, _store.BalanceOf(_paper.Id));
        Assert.AreEqual(1, _store.Commits, "выдача выполняется в транзакции");
    }

    [TestMethod, Description("Дефект Т14: выдача задним числом раньше поступления")]
    public void Issue_BackdatedBeforeReceipt_Rejected()
    {
        Receive(30);
        var ex = Fails(() => Issue(5, daysAgo: 10));
        StringAssert.Contains(ex.Message, "на дату 07.11.2026: можно выдать не более 0 пачка");
    }

    [TestMethod, Description("Выдача задним числом не должна делать отрицательным остаток после более поздних выдач")]
    public void Issue_BackdatedBreakingLaterBalance_Rejected()
    {
        Receive(20, 10);
        Issue(15, 2);              // после неё остаток 5
        Receive(40, 0);
        // На дату 5 дней назад было 20, но после выдачи 2 дня назад осталось бы 5 − 10 < 0
        var ex = Fails(() => Issue(10, daysAgo: 5));
        StringAssert.Contains(ex.Message, "не более 5 пачка");
        Issue(5, daysAgo: 5);      // а 5 — можно
        Assert.AreEqual(40, _store.BalanceOf(_paper.Id));
    }

    [DataTestMethod, Description("Количество, доступное к выдаче на дату")]
    [DataRow(12, 0, DisplayName = "До первого поступления — 0")]
    [DataRow(10, 5, DisplayName = "В день поступления — ограничено последующей выдачей")]
    [DataRow(3, 5, DisplayName = "После выдачи — остаток после неё")]
    [DataRow(1, 25, DisplayName = "В день второго поступления — весь остаток")]
    [DataRow(0, 25, DisplayName = "Сегодня — текущий остаток")]
    public void AvailableForIssue_ByDate(int daysAgo, int expected)
    {
        Receive(20, 10);
        Issue(15, 4);
        Receive(20, 1);
        Assert.AreEqual(expected, StockService.AvailableForIssue(_store.MovementList, Today.AddDays(-daysAgo)));
    }

    [DataTestMethod, Description("Дата операции: не в будущем и не ранее чем за 30 дней")]
    [DataRow(-1, "позже сегодняшней")]
    [DataRow(31, "не ранее чем за 30 дней")]
    public void Receive_WrongDate_Rejected(int daysAgo, string expected)
    {
        var ex = Fails(() => Receive(5, daysAgo));
        Assert.AreEqual(400, ex.Status);
        StringAssert.Contains(ex.Message, expected);
    }

    [TestMethod]
    public void Receive_Day30Back_Allowed() => Receive(5, 30);

    [TestMethod, Description("Операции по товару, выведенному из ассортимента, не оформляются")]
    public void Movement_ForArchivedProduct_Rejected()
    {
        _paper.IsArchived = true;
        Assert.AreEqual(409, Fails(() => Receive(5)).Status);
    }

    [TestMethod]
    public void Issue_UnknownDepartment_NotFound()
    {
        Receive(5);
        var ex = Fails(() => _stock.Issue(new IssueForm(_paper.Id, 1, Today, 99, "Иванов И. И.", null)));
        Assert.AreEqual(404, ex.Status);
    }

    [TestMethod, Description("Список к пополнению: остаток ниже минимального, нехватка = минимум − остаток")]
    public void LowStock_ShowsShortage()
    {
        Receive(25);
        Issue(10);
        var line = _stock.LowStock().Single();
        Assert.AreEqual(15, line.Quantity);
        Assert.AreEqual(5, line.Shortage);
        Issue(0 + 1);
        Assert.AreEqual(6, _stock.LowStock().Single().Shortage);
    }

    [TestMethod]
    public void LowStock_AtMinimum_NotListed()
    {
        Receive(20);
        Assert.AreEqual(0, _stock.LowStock().Count);
    }

    [TestMethod, Description("История товара: новые операции сверху, остаток после каждой операции")]
    public void ProductHistory_HasRunningBalance()
    {
        Receive(30, 3);
        Issue(12, 2);
        Issue(10, 1);
        var balances = _stock.ProductHistory(_paper.Id).Select(h => h.BalanceAfter).ToArray();
        CollectionAssert.AreEqual(new[] { 8, 18, 30 }, balances);
    }

    [TestMethod]
    public void History_WrongPeriod_Rejected() =>
        Assert.AreEqual(400, Fails(() => _stock.History(new(Today, Today.AddDays(-1)))).Status);

    [TestMethod, Description("Расход по подразделениям за период")]
    public void Consumption_SumsIssuesInPeriod()
    {
        Receive(100, 20);
        Issue(10, 15);
        Issue(7, 3);
        var line = _stock.Consumption(Today.AddDays(-5), Today).Single();
        Assert.AreEqual(1, line.Operations);
        Assert.AreEqual(7, line.Quantity);
    }
}

/// <summary>Модульные тесты справочников.</summary>
[TestClass]
public class CatalogServiceTests
{
    private MemoryStore _store = null!;
    private CatalogService _catalog = null!;

    [TestInitialize]
    public void Prepare()
    {
        _store = new MemoryStore();
        _catalog = new CatalogService(_store, _store, _store);
        _catalog.CreateProduct(new ProductForm("КТ-0001", "Бумага А4", ProductCategory.Paper, "пачка", 20));
    }

    [TestMethod, Description("Артикул приводится к верхнему регистру, пробелы в наименовании сжимаются")]
    public void CreateProduct_NormalizesInput()
    {
        var p = _catalog.CreateProduct(new ProductForm(" кт-0002 ", "Ручка   шариковая  синяя", ProductCategory.Writing, " шт. ", 50));
        Assert.AreEqual("КТ-0002", p.Article);
        Assert.AreEqual("Ручка шариковая синяя", p.Name);
        Assert.AreEqual("шт.", p.Unit);
    }

    [TestMethod]
    public void CreateProduct_DuplicateArticle_Conflict()
    {
        var ex = Assert.ThrowsException<AppException>(() =>
            _catalog.CreateProduct(new ProductForm("кт-0001", "Бумага", ProductCategory.Paper, "пачка", 1)));
        Assert.AreEqual(409, ex.Status);
        Assert.AreEqual("Товар с артикулом КТ-0001 уже есть в справочнике", ex.Message);
    }

    [TestMethod, Description("Дефект Т15: единица измерения не меняется, если по товару есть операции")]
    public void UpdateProduct_UnitChangeWithMovements_Conflict()
    {
        _store.Add(new Movement { ProductId = 1, Kind = MovementKind.Receipt, Quantity = 5, Date = new DateOnly(2026, 11, 1) });
        var ex = Assert.ThrowsException<AppException>(() =>
            _catalog.UpdateProduct(1, new ProductForm("КТ-0001", "Бумага А4", ProductCategory.Paper, "коробка", 20)));
        StringAssert.Contains(ex.Message, "Нельзя изменить единицу измерения");
        // остальные сведения изменять можно
        _catalog.UpdateProduct(1, new ProductForm("КТ-0001", "Бумага офисная А4", ProductCategory.Paper, "пачка", 25));
        Assert.AreEqual(25, _store.ProductList[0].MinQuantity);
    }

    [TestMethod]
    public void UpdateProduct_UnitChangeWithoutMovements_Allowed()
    {
        _catalog.UpdateProduct(1, new ProductForm("КТ-0001", "Бумага А4", ProductCategory.Paper, "коробка", 20));
        Assert.AreEqual("коробка", _store.ProductList[0].Unit);
    }

    [TestMethod, Description("Товар с остатком нельзя вывести из ассортимента")]
    public void Archive_WithBalance_Conflict()
    {
        _store.Add(new Movement { ProductId = 1, Kind = MovementKind.Receipt, Quantity = 5, Date = new DateOnly(2026, 11, 1) });
        var ex = Assert.ThrowsException<AppException>(() => _catalog.SetArchived(1, true));
        StringAssert.Contains(ex.Message, "остаток 5 пачка");
    }

    [TestMethod]
    public void Archive_AndRestore()
    {
        Assert.IsTrue(_catalog.SetArchived(1, true).IsArchived);
        Assert.AreEqual(409, Assert.ThrowsException<AppException>(() => _catalog.SetArchived(1, true)).Status);
        Assert.IsFalse(_catalog.SetArchived(1, false).IsArchived);
    }

    [TestMethod, Description("Повтор названия подразделения без учёта регистра и лишних пробелов")]
    public void CreateDepartment_Duplicate_Conflict()
    {
        _catalog.CreateDepartment(new DepartmentForm("Учебная часть", "Ковалёва Ирина Петровна"));
        var ex = Assert.ThrowsException<AppException>(() =>
            _catalog.CreateDepartment(new DepartmentForm("  учебная   ЧАСТЬ ", "Иванов Иван Иванович")));
        Assert.AreEqual(409, ex.Status);
    }

    [TestMethod]
    public void Products_SearchByNameOrArticle()
    {
        _catalog.CreateProduct(new ProductForm("КТ-0002", "Ручка шариковая", ProductCategory.Writing, "шт.", 1));
        Assert.AreEqual(1, _catalog.Products("РУЧКА", null, false).Count);
        Assert.AreEqual(1, _catalog.Products("0001", null, false).Count);
        Assert.AreEqual(1, _catalog.Products(null, ProductCategory.Writing, false).Count);
    }
}
