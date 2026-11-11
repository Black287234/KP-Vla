using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Models;
using StationeryAccounting.Services;

namespace StationeryAccounting.Controllers;

/// <summary>
/// Справочник сотрудников. Удаления нет: на сотрудников ссылаются выдачи в истории.
/// </summary>
public class EmployeesController : Controller
{
    private readonly EmployeeService _employeeService;

    public EmployeesController(EmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    // GET /Employees
    public IActionResult Index()
    {
        return View(_employeeService.GetAll());
    }

    // GET /Employees/Create
    public IActionResult Create()
    {
        return View(new Employee());
    }

    // POST /Employees/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Employee employee)
    {
        if (!ModelState.IsValid)
        {
            return View(employee);
        }

        var result = _employeeService.Create(employee);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(employee);
        }

        TempData["Message"] = $"Сотрудник «{employee.FullName}» добавлен";
        return RedirectToAction(nameof(Index));
    }

    // GET /Employees/Edit/5
    public IActionResult Edit(int id)
    {
        var employee = _employeeService.GetById(id);
        if (employee == null)
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }
        return View(employee);
    }

    // POST /Employees/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Employee employee)
    {
        if (id != employee.Id || _employeeService.GetById(id) == null)
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }
        if (!ModelState.IsValid)
        {
            return View(employee);
        }

        var result = _employeeService.Update(employee);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(employee);
        }

        TempData["Message"] = "Изменения сохранены";
        return RedirectToAction(nameof(Index));
    }
}
