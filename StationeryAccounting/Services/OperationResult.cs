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

    public static OperationResult Ok(string? warning = null)
    {
        return new OperationResult { Success = true, Warning = warning };
    }

    public static OperationResult Fail(string error)
    {
        return new OperationResult { Success = false, Error = error };
    }
}
