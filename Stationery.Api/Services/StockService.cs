using System.ComponentModel.DataAnnotations;
using Stationery.Api.Data;
using Stationery.Api.Domain;
using Stationery.Api.Infrastructure;

namespace Stationery.Api.Services;

public record ReceiptForm(
    [property: Required(ErrorMessage = "Выберите товар")] int? ProductId,
    [property: Required(ErrorMessage = "Укажите количество")]
    [property: Range(1, 100000, ErrorMessage = "Количество — от 1 до 100 000")] int? Quantity,
    [property: Required(ErrorMessage = "Укажите дату поступления")] DateOnly? Date,
    [property: Required(ErrorMessage = "Укажите поставщика")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Поставщик — от 2 до 100 символов")] string? Supplier,
    [property: StringLength(30, ErrorMessage = "Номер документа — не более 30 символов")] string? DocumentNumber,
    [property: StringLength(200, ErrorMessage = "Комментарий — не более 200 символов")] string? Comment);

public record IssueForm(
    [property: Required(ErrorMessage = "Выберите товар")] int? ProductId,
    [property: Required(ErrorMessage = "Укажите количество")]
    [property: Range(1, 100000, ErrorMessage = "Количество — от 1 до 100 000")] int? Quantity,
    [property: Required(ErrorMessage = "Укажите дату выдачи")] DateOnly? Date,
    [property: Required(ErrorMessage = "Выберите подразделение")] int? DepartmentId,
    [property: Required(ErrorMessage = "Укажите получателя")]
    [property: StringLength(100, MinimumLength = 5, ErrorMessage = "ФИО получателя — от 5 до 100 символов")] string? Recipient,
    [property: StringLength(200, ErrorMessage = "Комментарий — не более 200 символов")] string? Comment);

/// <summary>Строка ведомости остатков.</summary>
public record StockLine(Product Product, int Received, int Issued, int Quantity)
{
    public bool BelowMinimum => !Product.IsArchived && Quantity < Product.MinQuantity;
    public int Shortage => Math.Max(0, Product.MinQuantity - Quantity);
}

/// <summary>Операция в истории товара с остатком после неё.</summary>
public record HistoryLine(Movement Movement, int BalanceAfter);

/// <summary>Расход товаров подразделением за период.</summary>
public record ConsumptionLine(Department Department, int Operations, int Positions, int Quantity);

/// <summary>
/// Движение товаров: поступление, выдача, расчёт остатков, история, контроль минимального количества.
/// Остаток нигде не хранится: он вычисляется по операциям, поэтому не может разойтись с историей.
/// </summary>
public class StockService(IProductRepository products, IDepartmentRepository departments,
    IMovementRepository movements, TimeProvider clock)
{
    /// <summary>Насколько давней датой можно оформить операцию (ввод документов задним числом).</summary>
    public const int MaxDaysBack = 30;

    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public Movement Receive(ReceiptForm form)
    {
        var product = ActiveProduct(form.ProductId!.Value);
        CheckDate(form.Date!.Value);
        var m = new Movement
        {
            Kind = MovementKind.Receipt, ProductId = product.Id, Product = product, Quantity = form.Quantity!.Value,
            Date = form.Date!.Value, Supplier = form.Supplier!.Trim(), DocumentNumber = Clean(form.DocumentNumber),
            Comment = Clean(form.Comment), RegisteredAt = clock.GetLocalNow().DateTime
        };
        movements.Add(m);
        movements.Save();
        return m;
    }

    public Movement Issue(IssueForm form)
    {
        var product = ActiveProduct(form.ProductId!.Value);
        var department = departments.Find(form.DepartmentId!.Value)
                         ?? throw AppException.NotFound("Подразделение не найдено");
        CheckDate(form.Date!.Value);
        var quantity = form.Quantity!.Value;

        using var tx = movements.BeginTransaction(out var commit);
        var date = form.Date!.Value;
        var history = movements.List(new MovementFilter(ProductId: product.Id));
        var balance = history.Sum(Signed);
        var available = AvailableForIssue(history, date);
        if (quantity > available)
            throw AppException.Conflict(available == balance
                ? $"Недостаточно товара «{product.Name}»: на складе {balance} {product.Unit}, запрошено {quantity} {product.Unit}"
                : $"Недостаточно товара «{product.Name}» на дату {date:dd.MM.yyyy}: можно выдать не более {available} {product.Unit}, " +
                  $"иначе остаток в истории станет отрицательным (сейчас на складе {balance} {product.Unit})");

        var m = new Movement
        {
            Kind = MovementKind.Issue, ProductId = product.Id, Product = product, Quantity = quantity, Date = form.Date!.Value,
            DepartmentId = department.Id, Department = department, Recipient = form.Recipient!.Trim(),
            Comment = Clean(form.Comment), RegisteredAt = clock.GetLocalNow().DateTime
        };
        movements.Add(m);
        movements.Save();
        commit();
        return m;
    }

    /// <summary>Ведомость остатков по всем товарам.</summary>
    public IReadOnlyList<StockLine> Stock(bool includeArchived = false)
    {
        var balances = movements.Balances();
        return products.List(includeArchived)
            .Select(p => balances.TryGetValue(p.Id, out var b)
                ? new StockLine(p, b.Received, b.Issued, b.Quantity)
                : new StockLine(p, 0, 0, 0))
            .ToList();
    }

    /// <summary>Товары с остатком ниже минимального — к пополнению; сначала с наибольшей нехваткой.</summary>
    public IReadOnlyList<StockLine> LowStock() =>
        Stock().Where(l => l.BelowMinimum)
               .OrderByDescending(l => (double)l.Shortage / Math.Max(1, l.Product.MinQuantity))
               .ThenBy(l => l.Product.Article)
               .ToList();

    public IReadOnlyList<Movement> History(MovementFilter filter)
    {
        if (filter.From > filter.To) throw AppException.Invalid("Начало периода позже его окончания");
        return movements.List(filter).Reverse().ToList();   // новые операции — сверху
    }

    /// <summary>История одного товара с остатком после каждой операции (нарастающим итогом).</summary>
    public IReadOnlyList<HistoryLine> ProductHistory(int productId)
    {
        if (products.Find(productId) is null) throw AppException.NotFound($"Товар с кодом {productId} не найден");
        var running = 0;
        var lines = new List<HistoryLine>();
        foreach (var m in movements.List(new MovementFilter(ProductId: productId)))
        {
            running += m.Kind == MovementKind.Receipt ? m.Quantity : -m.Quantity;
            lines.Add(new HistoryLine(m, running));
        }
        lines.Reverse();
        return lines;
    }

    /// <summary>Расход по подразделениям за период.</summary>
    public IReadOnlyList<ConsumptionLine> Consumption(DateOnly from, DateOnly to)
    {
        if (from > to) throw AppException.Invalid("Начало периода позже его окончания");
        var issues = movements.List(new MovementFilter(from, to, Kind: MovementKind.Issue));
        return departments.List()
            .Select(d =>
            {
                var own = issues.Where(m => m.DepartmentId == d.Id).ToList();
                return new ConsumptionLine(d, own.Count, own.Select(m => m.ProductId).Distinct().Count(), own.Sum(m => m.Quantity));
            })
            .OrderByDescending(l => l.Quantity).ThenBy(l => l.Department.Name)
            .ToList();
    }

    public int BalanceOf(int productId) => movements.BalanceOf(productId);

    private static int Signed(Movement m) => m.Kind == MovementKind.Receipt ? m.Quantity : -m.Quantity;

    /// <summary>
    /// Сколько товара можно выдать датой <paramref name="date"/>, чтобы остаток не стал отрицательным
    /// ни на эту дату, ни после неё. Новая операция встаёт в историю после всех операций той же даты,
    /// поэтому учитываются остаток на конец этой даты и остатки после каждой более поздней операции.
    /// </summary>
    public static int AvailableForIssue(IEnumerable<Movement> history, DateOnly date)
    {
        var ordered = history.OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();
        var running = 0;
        var minimum = int.MaxValue;
        foreach (var m in ordered)
        {
            if (m.Date > date && minimum == int.MaxValue) minimum = running;   // остаток на конец даты выдачи
            running += Signed(m);
            if (m.Date > date) minimum = Math.Min(minimum, running);
        }
        if (minimum == int.MaxValue) minimum = running;   // более поздних операций нет
        return Math.Max(0, minimum);
    }

    private Product ActiveProduct(int id)
    {
        var p = products.Find(id) ?? throw AppException.NotFound($"Товар с кодом {id} не найден");
        if (p.IsArchived) throw AppException.Conflict($"Товар «{p.Name}» выведен из ассортимента: операции по нему не оформляются");
        return p;
    }

    private void CheckDate(DateOnly date)
    {
        if (date > Today) throw AppException.Invalid("Дата операции не может быть позже сегодняшней");
        if (date < Today.AddDays(-MaxDaysBack))
            throw AppException.Invalid($"Операцию можно оформить не ранее чем за {MaxDaysBack} дней до сегодняшней даты");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
