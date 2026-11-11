using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Models;
using StationeryAccounting.Services;

namespace StationeryAccounting.Controllers;

/// <summary>
/// Страницы справочника товаров. Контроллер только принимает запрос,
/// вызывает сервис и выбирает, какую страницу показать.
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
        return View(_productService.GetAll());
    }

    // GET /Products/Details/5 — карточка товара
    public IActionResult Details(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFoundPage();
        }
        return View(product);
    }

    // GET /Products/Create — пустая форма
    public IActionResult Create()
    {
        return View(new Product());
    }

    // POST /Products/Create — сохранение новой записи
    [HttpPost]
    [ValidateAntiForgeryToken]   // защита от подделки запроса с чужого сайта
    public IActionResult Create(Product product)
    {
        // Сначала проверки атрибутов ([Required], [Range] и т. д.)
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        // Затем проверки сервиса (например, уникальность артикула)
        var result = _productService.Create(product);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(product);
        }

        TempData["Message"] = $"Товар «{product.Name}» добавлен";
        return RedirectToAction(nameof(Index));
    }

    // GET /Products/Edit/5 — форма редактирования
    public IActionResult Edit(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFoundPage();
        }
        return View(product);
    }

    // POST /Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return NotFoundPage();
        }
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        var result = _productService.Update(product);
        if (!result.Success)
        {
            if (_productService.GetById(id) == null)
            {
                return NotFoundPage();
            }
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(product);
        }

        TempData["Message"] = "Изменения сохранены";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Products/Archive/5 — в архив (вместо удаления)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Archive(int id)
    {
        var result = _productService.Archive(id);
        TempData[result.Success ? "Message" : "Error"] =
            result.Success ? "Товар отправлен в архив" : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Products/Restore/5 — вернуть из архива
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Restore(int id)
    {
        var result = _productService.Restore(id);
        TempData[result.Success ? "Message" : "Error"] =
            result.Success ? "Товар возвращён из архива" : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // Страница «Запись не найдена» с кодом 404 вместо падения приложения
    private IActionResult NotFoundPage()
    {
        Response.StatusCode = 404;
        return View("NotFound");
    }
}
