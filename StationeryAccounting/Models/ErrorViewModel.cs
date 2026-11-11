namespace StationeryAccounting.Models;

/// <summary>Данные для страницы ошибки.</summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
