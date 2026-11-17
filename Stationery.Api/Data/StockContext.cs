using Microsoft.EntityFrameworkCore;
using Stationery.Api.Domain;

namespace Stationery.Api.Data;

/// <summary>Контекст базы данных склада канцелярских товаров.</summary>
public class StockContext(DbContextOptions<StockContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Movement> Movements => Set<Movement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.Property(p => p.Article).HasMaxLength(7).IsRequired();
            e.HasIndex(p => p.Article).IsUnique();
            e.Property(p => p.Name).HasMaxLength(120).IsRequired();
            e.Property(p => p.Unit).HasMaxLength(15).IsRequired();
            e.Property(p => p.Category).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<Department>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(d => d.Name).IsUnique();
            e.Property(d => d.Head).HasMaxLength(100).IsRequired();
        });
        b.Entity<Movement>(e =>
        {
            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.Supplier).HasMaxLength(100);
            e.Property(m => m.DocumentNumber).HasMaxLength(30);
            e.Property(m => m.Recipient).HasMaxLength(100);
            e.Property(m => m.Comment).HasMaxLength(200);
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Department).WithMany().HasForeignKey(m => m.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.ProductId, m.Date });
            // Количество всегда положительное: вид операции задаёт знак при расчёте остатка
            e.ToTable(t => t.HasCheckConstraint("CK_Movements_Quantity", "Quantity > 0"));
        });
    }
}
