using StationeryAccounting.Models;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Tests;

/// <summary>Справочник товаров: добавление, изменение, архив, поиск и фильтры.</summary>
public class ProductServiceTests : IDisposable
{
    private readonly TestDb _testDb = new();

    public void Dispose() => _testDb.Dispose();

    private ProductService Service(out Data.AppDbContext db)
    {
        db = _testDb.CreateContext();
        return new ProductService(db);
    }

    [Fact]
    public void СоздатьТовар_КорректныеДанные_Сохраняется()
    {
        var service = Service(out var db);

        var result = service.Create(new Product { Article = "kt-020", Name = "  Ластик  ", Unit = "шт.", MinQuantity = 3 });

        Assert.True(result.Success);
        var saved = Assert.Single(service.GetAll());
        Assert.Equal("KT-020", saved.Article);   // артикул приведён к верхнему регистру
        Assert.Equal("Ластик", saved.Name);      // пробелы по краям убраны
        db.Dispose();
    }

    [Fact]
    public void СоздатьТовар_ПовторяющийсяАртикул_Отклоняется()
    {
        _testDb.AddProduct("KT-001", "Ручка синяя");
        var service = Service(out var db);

        // Тот же артикул в другом регистре тоже считается повтором
        var result = service.Create(new Product { Article = "kt-001", Name = "Ручка красная", Unit = "шт." });

        Assert.False(result.Success);
        Assert.Contains("уже существует", result.Error);
        db.Dispose();
    }

    [Fact]
    public void СоздатьТовар_ПустоеНаименование_Отклоняется()
    {
        var service = Service(out var db);

        var result = service.Create(new Product { Article = "KT-030", Name = " ", Unit = "шт." });

        Assert.False(result.Success);
        Assert.Equal("Укажите наименование", result.Error);
        db.Dispose();
    }

    [Fact]
    public void СоздатьТовар_ОтрицательныйМинимум_Отклоняется()
    {
        var service = Service(out var db);

        var result = service.Create(new Product { Article = "KT-031", Name = "Скотч", Unit = "шт.", MinQuantity = -1 });

        Assert.False(result.Success);
        Assert.Equal("Минимальный остаток не может быть отрицательным", result.Error);
        db.Dispose();
    }

    [Fact]
    public void СоздатьТовар_МинимумНоль_Допустимо()
    {
        var service = Service(out var db);

        var result = service.Create(new Product { Article = "KT-032", Name = "Скотч", Unit = "шт.", MinQuantity = 0 });

        Assert.True(result.Success);
        db.Dispose();
    }

    [Fact]
    public void ИзменитьТовар_НеизвестныйId_Отклоняется()
    {
        var service = Service(out var db);

        var result = service.Update(new Product { Id = 999, Article = "KT-001", Name = "Ручка", Unit = "шт." });

        Assert.False(result.Success);
        Assert.Equal("Товар не найден", result.Error);
        Assert.Null(service.GetById(999));
        db.Dispose();
    }

    [Fact]
    public void АрхивироватьТовар_Повторно_Отклоняется()
    {
        var pen = _testDb.AddProduct("KT-001", "Ручка синяя");
        var service = Service(out var db);

        var first = service.Archive(pen.Id);
        var second = service.Archive(pen.Id);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.True(service.GetById(pen.Id)!.IsArchived);
        db.Dispose();
    }

    [Fact]
    public void НовыйТовар_БезОпераций_ОстатокНольИТребуетПополнения()
    {
        _testDb.AddProduct("KT-012", "Клей-карандаш", minQuantity: 5);
        var service = Service(out var db);

        var item = Assert.Single(service.Search(new ProductFilter()));

        Assert.Equal(0, item.Balance);
        Assert.True(item.NeedsRestock);
        db.Dispose();
    }

    [Fact]
    public void Поиск_ПоЧастиНаименованияБезУчетаРегистра_НаходитТовары()
    {
        _testDb.AddProduct("KT-001", "Ручка шариковая синяя");
        _testDb.AddProduct("KT-002", "Ручка шариковая чёрная");
        _testDb.AddProduct("KT-003", "Карандаш HB");
        var service = Service(out var db);

        var found = service.Search(new ProductFilter { Search = "РУЧКА" });

        Assert.Equal(2, found.Count);
        db.Dispose();
    }

    [Fact]
    public void Поиск_ПоАртикулу_НаходитОдинТовар()
    {
        _testDb.AddProduct("KT-001", "Ручка шариковая синяя");
        _testDb.AddProduct("KT-003", "Карандаш HB");
        var service = Service(out var db);

        var found = service.Search(new ProductFilter { Search = "kt-003" });

        Assert.Equal("Карандаш HB", Assert.Single(found).Name);
        db.Dispose();
    }

    [Fact]
    public void Поиск_НетСовпадений_ПустойСписок()
    {
        _testDb.AddProduct("KT-001", "Ручка шариковая синяя");
        var service = Service(out var db);

        Assert.Empty(service.Search(new ProductFilter { Search = "степлер" }));
        db.Dispose();
    }

    [Fact]
    public void ФильтрНижеМинимума_ПоказываетТолькоТоварыСОстаткомНеВышеМинимума()
    {
        var low = _testDb.AddProduct("KT-001", "Ручка", minQuantity: 5);      // остаток 5 = минимум
        var ok = _testDb.AddProduct("KT-002", "Карандаш", minQuantity: 5);    // остаток 20
        var archived = _testDb.AddProduct("KT-003", "Лента", minQuantity: 5, archived: true);
        _testDb.AddOperation(low.Id, OperationType.Receipt, 5, DateTime.Today);
        _testDb.AddOperation(ok.Id, OperationType.Receipt, 20, DateTime.Today);
        var service = Service(out var db);

        var found = service.Search(new ProductFilter { BelowMinOnly = true, ShowArchived = true });

        Assert.Equal(low.Id, Assert.Single(found).Id);   // архивный не попадает в «Ниже минимума»
        db.Dispose();
    }

    [Fact]
    public void Справочник_ПоУмолчанию_АрхивныеСкрыты()
    {
        _testDb.AddProduct("KT-001", "Ручка");
        _testDb.AddProduct("KT-011", "Лента", archived: true);
        var service = Service(out var db);

        Assert.Single(service.Search(new ProductFilter()));
        Assert.Equal(2, service.Search(new ProductFilter { ShowArchived = true }).Count);
        db.Dispose();
    }
}
