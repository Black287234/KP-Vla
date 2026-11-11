using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Models;
using StationeryAccounting.Services;

namespace StationeryAccounting.Controllers.Api;

/// <summary>
/// API для получения товаров в формате JSON (проверка GET-запросом).
/// Адрес: /api/products
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsApiController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsApiController(ProductService productService)
    {
        _productService = productService;
    }

    // GET /api/products — все товары
    [HttpGet]
    public ActionResult<List<Product>> GetAll()
    {
        return _productService.GetAll();
    }

    // GET /api/products/5 — один товар
    [HttpGet("{id:int}")]
    public ActionResult<Product> GetById(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            // Возвращаем 404 с понятным сообщением на русском
            return NotFound(new { error = $"Товар с Id = {id} не найден" });
        }
        return product;
    }
}
