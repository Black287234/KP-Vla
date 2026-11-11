using StationeryAccounting.Services;

var builder = WebApplication.CreateBuilder(args);

// Подключаем MVC: контроллеры + представления (страницы Razor)
builder.Services.AddControllersWithViews();

// Регистрируем сервис товаров. Контроллеры получают его через конструктор
// (внедрение зависимостей), а не создают сами через new.
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

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
