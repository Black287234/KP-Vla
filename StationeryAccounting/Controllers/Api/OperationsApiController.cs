using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Controllers.Api;

/// <summary>
/// API операций движения (JSON): история, поступление, выдача.
/// Нужен для ручных проверок HTTP-запросами (файл StationeryAccounting.http).
/// </summary>
[ApiController]
[Route("api/operations")]
public class OperationsApiController : ControllerBase
{
    private readonly StockService _stockService;

    public OperationsApiController(StockService stockService)
    {
        _stockService = stockService;
    }

    // GET /api/operations?productId=1&type=Issue&from=2026-10-01
    [HttpGet]
    public IActionResult GetHistory([FromQuery] HistoryFilter filter)
    {
        var list = _stockService.GetHistory(filter).Select(o => new
        {
            o.Id,
            o.ProductId,
            Product = o.Product!.Name,
            Type = o.Type.ToString(),
            o.Quantity,
            Date = o.Date.ToString("yyyy-MM-dd"),
            Employee = o.Employee?.FullName,
            o.Supplier,
            o.DocumentNumber,
            o.Comment
        });
        return Ok(list);
    }

    // POST /api/operations/receipt — оформить поступление
    [HttpPost("receipt")]
    public IActionResult Receipt(ReceiptForm form)
    {
        return ToResponse(_stockService.Receive(form), form.ProductId);
    }

    // POST /api/operations/issue — оформить выдачу
    [HttpPost("issue")]
    public IActionResult Issue(IssueForm form)
    {
        return ToResponse(_stockService.Issue(form), form.ProductId);
    }

    // Превращаем результат сервиса в HTTP-ответ: 200, 404 или 400
    private IActionResult ToResponse(OperationResult result, int productId)
    {
        if (result.Success)
        {
            return Ok(new
            {
                message = "Операция оформлена",
                warning = result.Warning,
                balance = _stockService.GetBalance(productId)
            });
        }
        if (result.IsNotFound)
        {
            return NotFound(new { error = result.Error });
        }
        return BadRequest(new { error = result.Error });
    }
}
