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
