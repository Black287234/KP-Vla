using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Models;

namespace StationeryAccounting.Data;

/// <summary>
/// Контекст базы данных EF Core. Каждый DbSet — это таблица в SQLite.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<StockOperation> StockOperations => Set<StockOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Артикул уникален — база сама не даст записать два одинаковых
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Article)
            .IsUnique();

        // Товар 1 — N операции. Restrict: нельзя удалить товар, у которого есть операции
        modelBuilder.Entity<StockOperation>()
            .HasOne(o => o.Product)
            .WithMany(p => p.Operations)
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Сотрудник 1 — N операции (выдачи)
        modelBuilder.Entity<StockOperation>()
            .HasOne(o => o.Employee)
            .WithMany(e => e.Operations)
            .HasForeignKey(o => o.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Индекс для быстрого поиска операций по товару и дате (история, расчёт остатка)
        modelBuilder.Entity<StockOperation>()
            .HasIndex(o => new { o.ProductId, o.Date });
    }
}
