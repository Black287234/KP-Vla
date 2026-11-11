using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Controllers.Api;

/// <summary>
/// API справочника товаров (JSON). Адрес: /api/products
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsApiController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly StockService _stockService;

    public ProductsApiController(ProductService productService, StockService stockService)
    {
        _productService = productService;
        _stockService = stockService;
    }

    // GET /api/products?search=ручка&belowMinOnly=true&showArchived=false
    [HttpGet]
    public ActionResult<List<ProductListItem>> GetAll([FromQuery] ProductFilter filter)
    {
        return _productService.Search(filter);
    }

    // GET /api/products/5 — товар с текущим остатком
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound(new { error = $"Товар с Id = {id} не найден" });
        }

        int balance = _stockService.GetBalance(id);
        return Ok(new
        {
            product.Id,
            product.Article,
            product.Name,
            product.Unit,
            product.MinQuantity,
            product.IsArchived,
            Balance = balance,
            NeedsRestock = StockCalculator.NeedsRestock(balance, product.MinQuantity)
        });
    }
}
