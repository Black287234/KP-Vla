using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Stationery.Tests;

/// <summary>
/// Интеграционные тесты: приложение запускается в памяти (WebApplicationFactory) с отдельным файлом базы данных,
/// запросы проходят весь конвейер — привязку данных, фильтры проверки, сервисы, EF Core и SQLite.
/// </summary>
[TestClass]
public class ApiTests
{
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _http = null!;
    private static string _dbFile = null!;
    private static readonly string Today = DateTime.Today.ToString("yyyy-MM-dd");

    [ClassInitialize]
    public static void Start(TestContext _)
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"stationery-test-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Stock", $"Data Source={_dbFile};Pooling=False"));
        _http = _factory.CreateClient();
    }

    [ClassCleanup]
    public static void Stop()
    {
        _http.Dispose();
        _factory.Dispose();
        File.Delete(_dbFile);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(HttpMethod method, string url, object? body = null, string? raw = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (raw != null) request.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body != null) request.Content = JsonContent.Create(body);
        var response = await _http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    [TestMethod, Description("Начальные данные загружены из seed.json")]
    public async Task Products_SeededFromJson()
    {
        var (status, body) = await Send(HttpMethod.Get, "/api/products?archived=true");
        Assert.AreEqual(HttpStatusCode.OK, status);
        Assert.AreEqual(19, body.GetArrayLength());
    }

    [TestMethod]
    public async Task UnknownProduct_Returns404WithMessage()
    {
        var (status, body) = await Send(HttpMethod.Get, "/api/products/999");
        Assert.AreEqual(HttpStatusCode.NotFound, status);
        Assert.AreEqual("Товар с кодом 999 не найден", body.GetProperty("message").GetString());
    }

    [TestMethod, Description("Фильтр проверки возвращает ошибки по полям")]
    public async Task CreateProduct_Invalid_ReturnsFieldErrors()
    {
        var (status, body) = await Send(HttpMethod.Post, "/api/products", new { article = "12", name = "", unit = "шт.", minQuantity = -1 });
        Assert.AreEqual(HttpStatusCode.BadRequest, status);
        var errors = body.GetProperty("errors");
        foreach (var field in new[] { "article", "name", "category", "minQuantity" })
            Assert.IsTrue(errors.TryGetProperty(field, out _), $"нет ошибки для поля {field}");
    }

    [TestMethod, Description("Поступление и выдача через API изменяют вычисляемый остаток")]
    public async Task ReceiptAndIssue_ChangeBalance()
    {
        var (created, product) = await Send(HttpMethod.Post, "/api/products",
            new { article = "ТС-0001", name = "Товар для теста API", category = "Writing", unit = "шт.", minQuantity = 5 });
        Assert.AreEqual(HttpStatusCode.Created, created);
        var id = product.GetProperty("id").GetInt32();

        var (s1, r) = await Send(HttpMethod.Post, "/api/movements/receipt", new { productId = id, quantity = 20, date = Today, supplier = "ООО «Тест»" });
        Assert.AreEqual(HttpStatusCode.Created, s1);
        Assert.AreEqual(20, r.GetProperty("balanceAfter").GetInt32());

        var (s2, i) = await Send(HttpMethod.Post, "/api/movements/issue", new { productId = id, quantity = 17, date = Today, departmentId = 1, recipient = "Иванов И. И." });
        Assert.AreEqual(HttpStatusCode.Created, s2);
        Assert.AreEqual(3, i.GetProperty("balanceAfter").GetInt32());

        var (s3, e) = await Send(HttpMethod.Post, "/api/movements/issue", new { productId = id, quantity = 4, date = Today, departmentId = 1, recipient = "Иванов И. И." });
        Assert.AreEqual(HttpStatusCode.Conflict, s3);
        StringAssert.Contains(e.GetProperty("message").GetString(), "на складе 3 шт.");

        var (_, low) = await Send(HttpMethod.Get, "/api/stock/low");
        Assert.IsTrue(low.EnumerateArray().Any(l => l.GetProperty("productId").GetInt32() == id && l.GetProperty("shortage").GetInt32() == 2));
    }

    [TestMethod, Description("Дефект Т20: неверный формат JSON — 400 и сообщение на русском языке")]
    public async Task MalformedJson_Returns400InRussian()
    {
        var (status, body) = await Send(HttpMethod.Post, "/api/movements/receipt", raw: "{\"productId\": 2, \"date\": \"32.13.2026\"}");
        Assert.AreEqual(HttpStatusCode.BadRequest, status);
        StringAssert.StartsWith(body.GetProperty("message").GetString(), "Данные запроса не удалось прочитать");
    }

    [TestMethod, Description("Параметр адреса в неверном формате — 400 и сообщение на русском языке")]
    public async Task WrongQueryParameter_Returns400InRussian()
    {
        var (status, body) = await Send(HttpMethod.Get, "/api/movements?from=вчера");
        Assert.AreEqual(HttpStatusCode.BadRequest, status);
        StringAssert.Contains(body.GetProperty("message").GetString(), "неверном формате");
    }

    [TestMethod]
    public async Task UnknownApiAddress_Returns404Json()
    {
        var (status, body) = await Send(HttpMethod.Get, "/api/nothing");
        Assert.AreEqual(HttpStatusCode.NotFound, status);
        Assert.AreEqual("Адрес API не найден", body.GetProperty("message").GetString());
    }

    [TestMethod, Description("Главная страница одностраничного приложения")]
    public async Task IndexPage_IsServed()
    {
        var html = await _http.GetStringAsync("/");
        StringAssert.Contains(html, "Склад канцтоваров");
    }
}
