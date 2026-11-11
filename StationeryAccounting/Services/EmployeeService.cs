using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Models;

namespace StationeryAccounting.Services;

/// <summary>
/// Справочник сотрудников-получателей.
/// Сотрудников не удаляем: на них ссылаются операции выдачи (история).
/// </summary>
public class EmployeeService
{
    private readonly AppDbContext _db;

    public EmployeeService(AppDbContext db)
    {
        _db = db;
    }

    public List<Employee> GetAll()
    {
        return _db.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToList();
    }

    public Employee? GetById(int id)
    {
        return _db.Employees.AsNoTracking().FirstOrDefault(e => e.Id == id);
    }

    public OperationResult Create(Employee employee)
    {
        Normalize(employee);
        var error = Validate(employee);
        if (error != null)
        {
            return OperationResult.Fail(error);
        }

        employee.Id = 0;
        _db.Employees.Add(employee);
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    public OperationResult Update(Employee employee)
    {
        var existing = _db.Employees.FirstOrDefault(e => e.Id == employee.Id);
        if (existing == null)
        {
            return OperationResult.Fail("Сотрудник не найден");
        }

        Normalize(employee);
        var error = Validate(employee);
        if (error != null)
        {
            return OperationResult.Fail(error);
        }

        existing.FullName = employee.FullName;
        existing.Department = employee.Department;
        _db.SaveChanges();
        return OperationResult.Ok();
    }

    private static void Normalize(Employee employee)
    {
        employee.FullName = (employee.FullName ?? string.Empty).Trim();
        employee.Department = (employee.Department ?? string.Empty).Trim();
    }

    private static string? Validate(Employee employee)
    {
        if (string.IsNullOrWhiteSpace(employee.FullName))
            return "Укажите ФИО сотрудника";
        if (employee.FullName.Length > 100)
            return "ФИО — не длиннее 100 символов";
        if (string.IsNullOrWhiteSpace(employee.Department))
            return "Укажите отдел";
        if (employee.Department.Length > 100)
            return "Название отдела — не длиннее 100 символов";
        return null;
    }
}
