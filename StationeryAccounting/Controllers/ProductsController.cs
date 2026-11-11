using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Services;

namespace StationeryAccounting.Controllers;

/// <summary>
/// Страницы справочника товаров. Контроллер только принимает запрос,
/// берёт данные из сервиса и отдаёт их в представление.
/// </summary>
public class ProductsController : Controller
{
    private readonly ProductService _productService;

    // Сервис приходит через конструктор (его зарегистрировали в Program.cs)
    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    // GET /Products — список товаров
    public IActionResult Index()
    {
        var products = _productService.GetAll();
        return View(products);
    }

    // GET /Products/Details/5 — карточка одного товара
    public IActionResult Details(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            // Неизвестный Id: показываем понятную страницу с кодом 404, а не падаем
            Response.StatusCode = 404;
            return View("NotFound");
        }
        return View(product);
    }
}
