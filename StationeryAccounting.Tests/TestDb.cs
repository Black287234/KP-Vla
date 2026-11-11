using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Models;

namespace StationeryAccounting.Tests;

/// <summary>
/// Тестовая база SQLite в оперативной памяти. Для каждого теста — своя чистая база.
/// Соединение держим открытым: пока оно открыто, база в памяти существует,
/// и разные DbContext видят одни и те же данные (так проверяем сохранение).
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();   // создаём таблицы по модели (без учебных данных)
    }

    /// <summary>Новый контекст к той же базе.</summary>
    public AppDbContext CreateContext() => new(_options);

    public Product AddProduct(string article, string name, int minQuantity = 5, bool archived = false, string unit = "шт.")
    {
        using var db = CreateContext();
        var product = new Product { Article = article, Name = name, MinQuantity = minQuantity, IsArchived = archived, Unit = unit };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    public Employee AddEmployee(string fullName = "Иванова Мария Сергеевна", string department = "Бухгалтерия")
    {
        using var db = CreateContext();
        var employee = new Employee { FullName = fullName, Department = department };
        db.Employees.Add(employee);
        db.SaveChanges();
        return employee;
    }

    /// <summary>Добавить операцию напрямую в базу (подготовка исходных данных теста).</summary>
    public void AddOperation(int productId, OperationType type, int quantity, DateTime date, int? employeeId = null)
    {
        using var db = CreateContext();
        db.StockOperations.Add(new StockOperation
        {
            ProductId = productId,
            Type = type,
            Quantity = quantity,
            Date = date,
            EmployeeId = employeeId,
            Supplier = type == OperationType.Receipt ? "ООО «Тест»" : null,
            CreatedAt = date
        });
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();
}
