using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Services;

var builder = WebApplication.CreateBuilder(args);

// Подключаем MVC: контроллеры + представления (страницы Razor)
builder.Services.AddControllersWithViews()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Ошибки проверки данных в API — по-русски и в том же формате { error }, что и ошибки сервисов
        options.InvalidModelStateResponseFactory = context =>
        {
            var messages = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Неверный формат данных" : e.ErrorMessage)
                .ToList();
            return new BadRequestObjectResult(new { error = string.Join("; ", messages) });
        };
    });

// База данных SQLite. Строка подключения берётся из appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Сервисы с предметной логикой. Scoped — один объект на один HTTP-запрос
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<StockService>();

var app = builder.Build();

// При запуске применяем миграции (создаём/обновляем базу) и заливаем учебные данные
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    // В рабочем режиме при ошибке показываем дружелюбную страницу, а не текст исключения
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();   // раздаём файлы из wwwroot (css)
app.UseRouting();

// Маршрут по умолчанию: /Контроллер/Действие/Id
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Нужен, чтобы тестовый проект мог обратиться к классу Program
public partial class Program { }
