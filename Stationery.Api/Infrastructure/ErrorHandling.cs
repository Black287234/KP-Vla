using System.ComponentModel.DataAnnotations;

namespace Stationery.Api.Infrastructure;

/// <summary>
/// Единый формат ошибок API: {"message": "...", "errors": {"поле": ["..."]}}.
/// </summary>
public static class ErrorHandling
{
    /// <summary>Перехват AppException на уровне конвейера: обработчики не содержат try/catch.</summary>
    public static void UseAppErrors(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (AppException ex)
            {
                context.Response.StatusCode = ex.Status;
                await context.Response.WriteAsJsonAsync(new { message = ex.Message });
            }
            catch (BadHttpRequestException ex)
            {
                // Тело запроса не разобрано или параметр адреса указан в неверном формате
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { message = Describe(ex) });
            }
        });
    }

    private static string Describe(BadHttpRequestException ex)
    {
        var text = ex.ToString();
        if (text.Contains("JsonException") || text.Contains("as JSON"))
            return "Данные запроса не удалось прочитать: проверьте формат JSON и значений (дата — гггг-мм-дд, количество — целое число)";
        if (text.Contains("Failed to bind parameter"))
            return "Параметр запроса указан в неверном формате (дата — гггг-мм-дд, код — целое число)";
        if (text.Contains("Required parameter"))
            return "Не передан обязательный параметр запроса";
        return "Неверный запрос";
    }

    /// <summary>Фильтр конечной точки: проверка атрибутов DataAnnotations у тела запроса.</summary>
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var body = context.Arguments.OfType<T>().FirstOrDefault();
            if (body is null)
                return Results.BadRequest(new { message = "Не переданы данные запроса" });
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true))
            {
                var errors = results
                    .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""]).Select(m => (Field: m, r.ErrorMessage)))
                    .GroupBy(x => JsonName(x.Field))
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
                return Results.BadRequest(new { message = "Проверьте введённые данные", errors });
            }
            return await next(context);
        });

    private static string JsonName(string field) =>
        string.IsNullOrEmpty(field) ? "" : char.ToLowerInvariant(field[0]) + field[1..];
}
