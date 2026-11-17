using Stationery.Api.Data;
using Stationery.Api.Domain;
using Stationery.Api.Infrastructure;
using Stationery.Api.Services;

namespace Stationery.Api.Endpoints;

/// <summary>Конечные точки движения товаров: операции, остатки, история, отчёты.</summary>
public static class StockEndpoints
{
    public static void MapStock(this RouteGroupBuilder api)
    {
        api.MapPost("/movements/receipt", (ReceiptForm form, StockService s) =>
        {
            var m = s.Receive(form);
            return Results.Created($"/api/movements/{m.Id}", ToJson(m, s.BalanceOf(m.ProductId)));
        }).Validate<ReceiptForm>();

        api.MapPost("/movements/issue", (IssueForm form, StockService s) =>
        {
            var m = s.Issue(form);
            return Results.Created($"/api/movements/{m.Id}", ToJson(m, s.BalanceOf(m.ProductId)));
        }).Validate<IssueForm>();

        api.MapGet("/movements", (StockService s, DateOnly? from, DateOnly? to, int? productId, int? departmentId, MovementKind? kind) =>
            s.History(new MovementFilter(from, to, productId, departmentId, kind)).Select(m => ToJson(m, null)));

        api.MapGet("/products/{id:int}/movements", (int id, StockService s) =>
            s.ProductHistory(id).Select(l => ToJson(l.Movement, l.BalanceAfter)));

        api.MapGet("/stock", (StockService s, bool? archived) => s.Stock(archived ?? false).Select(LineJson));
        api.MapGet("/stock/low", (StockService s) => s.LowStock().Select(LineJson));

        api.MapGet("/reports/summary", (StockService s) =>
        {
            var today = s.Today;
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var stock = s.Stock();
            var month = s.History(new MovementFilter(monthStart, today));
            return new
            {
                today,
                positions = stock.Count,
                inStock = stock.Count(l => l.Quantity > 0),
                belowMinimum = stock.Count(l => l.BelowMinimum),
                receiptsThisMonth = month.Count(m => m.Kind == MovementKind.Receipt),
                issuesThisMonth = month.Count(m => m.Kind == MovementKind.Issue),
                recent = s.History(new MovementFilter()).Take(8).Select(m => ToJson(m, null))
            };
        });

        api.MapGet("/reports/consumption", (StockService s, DateOnly from, DateOnly to) =>
            s.Consumption(from, to).Select(l => new
            {
                department = l.Department.Name, l.Department.Head, l.Operations, l.Positions, l.Quantity
            }));
    }

    public static object LineJson(StockLine l) => new
    {
        productId = l.Product.Id, l.Product.Article, l.Product.Name, l.Product.Unit,
        category = ProductCategoryNames.All[l.Product.Category],
        minQuantity = l.Product.MinQuantity, l.Received, l.Issued, l.Quantity, l.BelowMinimum, l.Shortage
    };

    public static object ToJson(Movement m, int? balanceAfter) => new
    {
        m.Id, Kind = m.Kind.ToString(), m.Date, m.ProductId,
        Article = m.Product?.Article, Product = m.Product?.Name, Unit = m.Product?.Unit, m.Quantity,
        m.Supplier, m.DocumentNumber, m.DepartmentId, Department = m.Department?.Name, m.Recipient, m.Comment,
        BalanceAfter = balanceAfter
    };
}
