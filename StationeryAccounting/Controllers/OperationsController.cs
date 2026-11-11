using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StationeryAccounting.Models;
using StationeryAccounting.Services;
using StationeryAccounting.ViewModels;

namespace StationeryAccounting.Controllers;

/// <summary>
/// Поступления, выдачи, история движения и итоги за период.
/// Вся проверка правил — в StockService, здесь только приём формы и выбор страницы.
/// </summary>
public class OperationsController : Controller
{
    private readonly StockService _stockService;
    private readonly ProductService _productService;
    private readonly EmployeeService _employeeService;

    public OperationsController(StockService stockService, ProductService productService, EmployeeService employeeService)
    {
        _stockService = stockService;
        _productService = productService;
        _employeeService = employeeService;
    }

    // GET /Operations — история движения с фильтрами
    public IActionResult Index(HistoryFilter filter)
    {
        FillLists(onlyActiveProducts: false);
        var model = new HistoryViewModel
        {
            Filter = filter,
            Operations = _stockService.GetHistory(filter)
        };
        return View(model);
    }

    // GET /Operations/Receipt?productId=1 — форма поступления
    public IActionResult Receipt(int? productId)
    {
        FillLists(onlyActiveProducts: true);
        return View(new ReceiptForm { ProductId = productId ?? 0 });
    }

    // POST /Operations/Receipt
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Receipt(ReceiptForm form)
    {
        if (ModelState.IsValid)
        {
            var result = _stockService.Receive(form);
            if (result.Success)
            {
                TempData["Message"] = $"Поступление оформлено: {form.Quantity} ед.";
                return RedirectToAction("Details", "Products", new { id = form.ProductId });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        // Ошибка — показываем ту же форму с введёнными данными и сообщением
        FillLists(onlyActiveProducts: true);
        return View(form);
    }

    // GET /Operations/Issue?productId=1 — форма выдачи
    public IActionResult Issue(int? productId)
    {
        FillLists(onlyActiveProducts: true);
        return View(new IssueForm { ProductId = productId ?? 0 });
    }

    // POST /Operations/Issue
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Issue(IssueForm form)
    {
        if (ModelState.IsValid)
        {
            var result = _stockService.Issue(form);
            if (result.Success)
            {
                TempData["Message"] = $"Выдача оформлена: {form.Quantity} ед.";
                if (result.Warning != null)
                {
                    TempData["Warning"] = result.Warning;
                }
                return RedirectToAction("Details", "Products", new { id = form.ProductId });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        FillLists(onlyActiveProducts: true);
        return View(form);
    }

    // GET /Operations/Totals?from=2026-10-01&to=2026-10-31 — итоги за период
    public IActionResult Totals(DateTime? from, DateTime? to)
    {
        // По умолчанию — с начала текущего месяца по сегодня
        var model = new TotalsViewModel
        {
            From = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date,
            To = (to ?? DateTime.Today).Date
        };

        if (model.From > model.To)
        {
            model.Error = "Дата начала периода не может быть позже даты окончания";
        }
        else
        {
            model.Rows = _stockService.GetTotals(model.From, model.To);
        }
        return View(model);
    }

    // Выпадающие списки товаров и сотрудников для форм и фильтров
    private void FillLists(bool onlyActiveProducts)
    {
        var products = _productService.GetAll()
            .Where(p => !onlyActiveProducts || !p.IsArchived)
            .Select(p => new SelectListItem($"{p.Article} — {p.Name}", p.Id.ToString()))
            .ToList();
        ViewBag.Products = products;

        ViewBag.Employees = _employeeService.GetAll()
            .Select(e => new SelectListItem($"{e.FullName} ({e.Department})", e.Id.ToString()))
            .ToList();

        ViewBag.Types = new List<SelectListItem>
        {
            new("Поступление", OperationType.Receipt.ToString()),
            new("Выдача", OperationType.Issue.ToString())
        };
    }
}
