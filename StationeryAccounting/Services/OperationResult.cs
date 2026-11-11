namespace StationeryAccounting.Services;

/// <summary>
/// Результат действия в сервисе. Вместо исключений на каждую ошибку пользователя
/// сервис возвращает понятный ответ: успешно или нет, и текст ошибки по-русски.
/// </summary>
public class OperationResult
{
    public bool Success { get; private set; }

    /// <summary>Текст ошибки, если действие отклонено.</summary>
    public string? Error { get; private set; }

    /// <summary>Предупреждение при успешном действии (например, остаток ниже минимума).</summary>
    public string? Warning { get; private set; }

    /// <summary>True, если ошибка в том, что запись не найдена (для ответа 404 в API).</summary>
    public bool IsNotFound { get; private set; }

    public static OperationResult Ok(string? warning = null)
    {
        return new OperationResult { Success = true, Warning = warning };
    }

    public static OperationResult Fail(string error)
    {
        return new OperationResult { Success = false, Error = error };
    }

    public static OperationResult NotFound(string error)
    {
        return new OperationResult { Success = false, Error = error, IsNotFound = true };
    }
}
