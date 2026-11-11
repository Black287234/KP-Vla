using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Models;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Services;

/// <summary>
/// Движение товаров: поступление, выдача, расчёт остатка, история и итоги.
/// Здесь находится главное правило модуля — нельзя выдать больше, чем есть на складе.
/// </summary>
public class StockService
{
    // Общий замок на всё приложение: две выдачи одного товара, пришедшие одновременно,
    // выполняются по очереди. Иначе обе могли бы увидеть один и тот же остаток
    // и вместе выдать больше, чем есть.
    private static readonly object IssueLock = new();

    private readonly AppDbContext _db;

    public StockService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Текущий остаток товара по формуле Σ поступлений − Σ выдач.</summary>
    public int GetBalance(int productId)
    {
        var operations = _db.StockOperations
            .AsNoTracking()
            .Where(o => o.ProductId == productId)
            .ToList();
        return StockCalculator.CalculateBalance(operations);
    }

    /// <summary>Оформить поступление товара.</summary>
    public OperationResult Receive(ReceiptForm form)
    {
        var commonError = CheckQuantityAndDate(form.Quantity, form.Date);
        if (commonError != null)
        {
            return OperationResult.Fail(commonError);
        }

        string? supplier = Clean(form.Supplier);
        string? document = Clean(form.DocumentNumber);
        if (supplier == null && document == null)
        {
            return OperationResult.Fail("Для поступления укажите поставщика или номер документа");
        }

        var product = _db.Products.FirstOrDefault(p => p.Id == form.ProductId);
        if (product == null)
        {
            return OperationResult.NotFound("Товар не найден");
        }
        if (product.IsArchived)
        {
            return OperationResult.Fail($"Нельзя проводить операции с архивным товаром «{product.Name}»");
        }

        _db.StockOperations.Add(new StockOperation
        {
            ProductId = product.Id,
            Type = OperationType.Receipt,
            Quantity = form.Quantity,
            Date = form.Date.Date,
            Supplier = supplier,
            DocumentNumber = document,
            Comment = Clean(form.Comment),
            CreatedAt = DateTime.Now
        });
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Оформить выдачу товара сотруднику. Отклоняется, если товара не хватает.
    /// Если после выдачи остаток ≤ минимума — выдача проходит, но возвращается предупреждение.
    /// </summary>
    public OperationResult Issue(IssueForm form)
    {
        var commonError = CheckQuantityAndDate(form.Quantity, form.Date);
        if (commonError != null)
        {
            return OperationResult.Fail(commonError);
        }

        if (form.EmployeeId == null)
        {
            return OperationResult.Fail("Для выдачи укажите сотрудника-получателя");
        }

        var product = _db.Products.FirstOrDefault(p => p.Id == form.ProductId);
        if (product == null)
        {
            return OperationResult.NotFound("Товар не найден");
        }
        if (product.IsArchived)
        {
            return OperationResult.Fail($"Нельзя проводить операции с архивным товаром «{product.Name}»");
        }

        var employee = _db.Employees.FirstOrDefault(e => e.Id == form.EmployeeId);
        if (employee == null)
        {
            return OperationResult.NotFound("Сотрудник не найден");
        }

        lock (IssueLock)
        {
            // ГЛАВНОЕ ПРАВИЛО: нельзя выдать больше, чем есть на складе
            int balance = GetBalance(product.Id);
            if (form.Quantity > balance)
            {
                return OperationResult.Fail(
                    $"Недостаточно товара «{product.Name}»: на складе {balance} {product.Unit}, " +
                    $"запрошено {form.Quantity} {product.Unit}");
            }

            _db.StockOperations.Add(new StockOperation
            {
                ProductId = product.Id,
                Type = OperationType.Issue,
                Quantity = form.Quantity,
                Date = form.Date.Date,
                EmployeeId = employee.Id,
                Comment = Clean(form.Comment),
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();

            // Контроль минимального количества
            int newBalance = balance - form.Quantity;
            string? warning = null;
            if (StockCalculator.NeedsRestock(newBalance, product.MinQuantity))
            {
                warning = $"Внимание: остаток «{product.Name}» — {newBalance} {product.Unit}, " +
                          $"минимальный — {product.MinQuantity}. Товар требует пополнения.";
            }
            return OperationResult.Ok(warning);
        }
    }

    /// <summary>История движения с фильтрами. Новые операции — сверху.</summary>
    public List<StockOperation> GetHistory(HistoryFilter filter)
    {
        // IQueryable — запрос ещё не выполнен, условия добавляются по одному
        IQueryable<StockOperation> query = _db.StockOperations
            .AsNoTracking()
            .Include(o => o.Product)
            .Include(o => o.Employee);

        if (filter.ProductId != null)
            query = query.Where(o => o.ProductId == filter.ProductId);
        if (filter.Type != null)
            query = query.Where(o => o.Type == filter.Type);
        if (filter.EmployeeId != null)
            query = query.Where(o => o.EmployeeId == filter.EmployeeId);
        if (filter.From != null)
            query = query.Where(o => o.Date >= filter.From.Value.Date);
        if (filter.To != null)
            query = query.Where(o => o.Date <= filter.To.Value.Date);

        return query
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Id)
            .ToList();
    }

    /// <summary>Итоги за период по каждому товару: сколько поступило и сколько выдано.</summary>
    public List<TotalsRow> GetTotals(DateTime from, DateTime to)
    {
        var operations = _db.StockOperations
            .AsNoTracking()
            .Include(o => o.Product)
            .Where(o => o.Date >= from.Date && o.Date <= to.Date)
            .ToList();

        return operations
            .GroupBy(o => o.ProductId)
            .Select(g => new TotalsRow
            {
                ProductId = g.Key,
                Article = g.First().Product!.Article,
                Name = g.First().Product!.Name,
                Unit = g.First().Product!.Unit,
                Received = g.Where(o => o.Type == OperationType.Receipt).Sum(o => o.Quantity),
                Issued = g.Where(o => o.Type == OperationType.Issue).Sum(o => o.Quantity)
            })
            .OrderBy(r => r.Article)
            .ToList();
    }

    // Общие проверки для поступления и выдачи
    private static string? CheckQuantityAndDate(int quantity, DateTime date)
    {
        if (quantity <= 0)
            return "Количество должно быть целым числом больше 0";
        if (quantity > 100000)
            return "Количество не может быть больше 100000";
        if (date.Date > DateTime.Today)
            return "Дата операции не может быть в будущем";
        return null;
    }

    // Пустая строка или пробелы → null, иначе обрезаем пробелы по краям
    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
