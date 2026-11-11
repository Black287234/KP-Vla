using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StationeryAccounting.Models;

namespace StationeryAccounting.Controllers;

/// <summary>Главная страница и страница ошибки.</summary>
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
