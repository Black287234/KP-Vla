using Microsoft.EntityFrameworkCore;
using StationeryAccounting.Data;
using StationeryAccounting.Services;

var builder = WebApplication.CreateBuilder(args);

// Подключаем MVC: контроллеры + представления (страницы Razor)
builder.Services.AddControllersWithViews();

// База данных SQLite. Строка подключения берётся из appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Сервисы с предметной логикой. Scoped — один объект на один HTTP-запрос
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<EmployeeService>();

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
