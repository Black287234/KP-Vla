namespace Stationery.Api.Infrastructure;

/// <summary>Ошибка, о которой нужно сообщить пользователю: код ответа HTTP и текст на русском языке.</summary>
public class AppException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static AppException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static AppException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static AppException Invalid(string message) => new(StatusCodes.Status400BadRequest, message);
}
