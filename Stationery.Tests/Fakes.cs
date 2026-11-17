using Stationery.Api.Data;
using Stationery.Api.Domain;

namespace Stationery.Tests;

/// <summary>
/// Хранилища в оперативной памяти для модульных тестов сервисов.
/// Сервисы зависят от интерфейсов, поэтому база данных в этих тестах не нужна.
/// </summary>
public class MemoryStore : IProductRepository, IDepartmentRepository, IMovementRepository
{
    public List<Product> ProductList { get; } = [];
    public List<Department> DepartmentList { get; } = [];
    public List<Movement> MovementList { get; } = [];
    public int Saves { get; private set; }
    public int Commits { get; private set; }

    // Товары
    IReadOnlyList<Product> IProductRepository.List(bool includeArchived) =>
        ProductList.Where(p => includeArchived || !p.IsArchived).OrderBy(p => p.Article).ToList();
    Product? IProductRepository.Find(int id) => ProductList.FirstOrDefault(p => p.Id == id);
    public bool ArticleExists(string article, int? exceptId) => ProductList.Any(p => p.Article == article && p.Id != exceptId);
    public void Add(Product product) { product.Id = ProductList.Count + 1; ProductList.Add(product); }

    // Подразделения
    IReadOnlyList<Department> IDepartmentRepository.List() => DepartmentList.OrderBy(d => d.Name).ToList();
    Department? IDepartmentRepository.Find(int id) => DepartmentList.FirstOrDefault(d => d.Id == id);
    public bool NameExists(string name, int? exceptId) =>
        DepartmentList.Any(d => d.Id != exceptId && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    public void Add(Department department) { department.Id = DepartmentList.Count + 1; DepartmentList.Add(department); }

    // Операции
    public IReadOnlyDictionary<int, Balance> Balances() =>
        MovementList.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => new Balance(g.Key,
            g.Where(m => m.Kind == MovementKind.Receipt).Sum(m => m.Quantity),
            g.Where(m => m.Kind == MovementKind.Issue).Sum(m => m.Quantity)));
    public int BalanceOf(int productId) =>
        MovementList.Where(m => m.ProductId == productId).Sum(m => m.Kind == MovementKind.Receipt ? m.Quantity : -m.Quantity);
    public IReadOnlyList<Movement> List(MovementFilter f) => MovementList
        .Where(m => (f.From == null || m.Date >= f.From) && (f.To == null || m.Date <= f.To)
                    && (f.ProductId == null || m.ProductId == f.ProductId)
                    && (f.DepartmentId == null || m.DepartmentId == f.DepartmentId)
                    && (f.Kind == null || m.Kind == f.Kind))
        .OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();
    public void Add(Movement movement) { movement.Id = MovementList.Count + 1; MovementList.Add(movement); }
    public bool HasMovements(int productId) => MovementList.Any(m => m.ProductId == productId);

    public void Save() => Saves++;

    public IDisposable BeginTransaction(out Action commit)
    {
        commit = () => Commits++;
        return new Nothing();
    }

    private sealed class Nothing : IDisposable { public void Dispose() { } }
}

/// <summary>Часы с заданной датой: результаты тестов не зависят от дня запуска.</summary>
public sealed class FixedClock(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
