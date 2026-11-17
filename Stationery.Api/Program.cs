using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Stationery.Api.Data;
using Stationery.Api.Endpoints;
using Stationery.Api.Infrastructure;
using Stationery.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<StockContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Stock") ?? "Data Source=stationery.db"));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IMovementRepository, MovementRepository>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddSingleton(TimeProvider.System);
// Перечисления передаются в JSON названиями (Paper, Receipt), а не числами
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    SeedData.Initialize(scope.ServiceProvider.GetRequiredService<StockContext>(), app.Environment.ContentRootPath, DateTime.Today);

app.UseAppErrors();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapCatalog();
api.MapStock();
// Неизвестный адрес API — ответ 404 в формате JSON, а не страница
api.MapFallback(() => Results.NotFound(new { message = "Адрес API не найден" }));

app.Run();

/// <summary>Объявление нужно тестам WebApplicationFactory.</summary>
public partial class Program;
