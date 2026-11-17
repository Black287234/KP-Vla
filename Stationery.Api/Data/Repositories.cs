using Microsoft.EntityFrameworkCore;
using Stationery.Api.Domain;

namespace Stationery.Api.Data;

/// <summary>Хранилище товаров. Сервисы работают с данными только через интерфейсы хранилищ.</summary>
public interface IProductRepository
{
    IReadOnlyList<Product> List(bool includeArchived);
    Product? Find(int id);
    bool ArticleExists(string article, int? exceptId);
    void Add(Product product);
    void Save();
}

public interface IDepartmentRepository
{
    IReadOnlyList<Department> List();
    Department? Find(int id);
    bool NameExists(string name, int? exceptId);
    void Add(Department department);
    void Save();
}

public class ProductRepository(StockContext db) : IProductRepository
{
    public IReadOnlyList<Product> List(bool includeArchived) =>
        db.Products.Where(p => includeArchived || !p.IsArchived).OrderBy(p => p.Article).ToList();

    public Product? Find(int id) => db.Products.FirstOrDefault(p => p.Id == id);

    public bool ArticleExists(string article, int? exceptId) =>
        db.Products.Any(p => p.Article == article && p.Id != exceptId);

    public void Add(Product product) => db.Products.Add(product);
    public void Save() => db.SaveChanges();
}

public class DepartmentRepository(StockContext db) : IDepartmentRepository
{
    public IReadOnlyList<Department> List() => db.Departments.OrderBy(d => d.Name).ToList();
    public Department? Find(int id) => db.Departments.FirstOrDefault(d => d.Id == id);

    // Сравнение без учёта регистра выполняется в памяти: SQLite не сравнивает кириллицу без учёта регистра
    public bool NameExists(string name, int? exceptId) =>
        db.Departments.AsNoTracking().AsEnumerable()
          .Any(d => d.Id != exceptId && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Add(Department department) => db.Departments.Add(department);
    public void Save() => db.SaveChanges();
}

/// <summary>Остаток товара на складе.</summary>
public record Balance(int ProductId, int Received, int Issued)
{
    public int Quantity => Received - Issued;
}

/// <summary>Условия отбора операций движения.</summary>
public record MovementFilter(DateOnly? From = null, DateOnly? To = null, int? ProductId = null,
    int? DepartmentId = null, MovementKind? Kind = null);

public interface IMovementRepository
{
    /// <summary>Остатки всех товаров (товар без операций в словаре отсутствует — его остаток равен нулю).</summary>
    IReadOnlyDictionary<int, Balance> Balances();
    int BalanceOf(int productId);
    bool HasMovements(int productId);
    IReadOnlyList<Movement> List(MovementFilter filter);
    void Add(Movement movement);
    void Save();

    /// <summary>Транзакция: проверка остатка и запись операции выполняются как одно целое.</summary>
    IDisposable BeginTransaction(out Action commit);
}

public class MovementRepository(StockContext db) : IMovementRepository
{
    public IReadOnlyDictionary<int, Balance> Balances() =>
        db.Movements
          .GroupBy(m => m.ProductId)
          .Select(g => new Balance(g.Key,
              g.Where(m => m.Kind == MovementKind.Receipt).Sum(m => m.Quantity),
              g.Where(m => m.Kind == MovementKind.Issue).Sum(m => m.Quantity)))
          .ToDictionary(b => b.ProductId);

    public int BalanceOf(int productId) =>
        db.Movements.Where(m => m.ProductId == productId)
          .Sum(m => m.Kind == MovementKind.Receipt ? m.Quantity : -m.Quantity);

    public bool HasMovements(int productId) => db.Movements.Any(m => m.ProductId == productId);

    public IReadOnlyList<Movement> List(MovementFilter f)
    {
        var q = db.Movements.AsNoTracking().Include(m => m.Product).Include(m => m.Department).AsQueryable();
        if (f.From is { } from) q = q.Where(m => m.Date >= from);
        if (f.To is { } to) q = q.Where(m => m.Date <= to);
        if (f.ProductId is { } p) q = q.Where(m => m.ProductId == p);
        if (f.DepartmentId is { } d) q = q.Where(m => m.DepartmentId == d);
        if (f.Kind is { } k) q = q.Where(m => m.Kind == k);
        return q.OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();
    }

    public void Add(Movement movement) => db.Movements.Add(movement);
    public void Save() => db.SaveChanges();

    public IDisposable BeginTransaction(out Action commit)
    {
        var tx = db.Database.BeginTransaction();
        commit = tx.Commit;
        return tx;
    }
}
