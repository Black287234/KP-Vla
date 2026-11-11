using StationeryAccounting.Models;

namespace StationeryAccounting.Services;

/// <summary>
/// Расчёты по складу без обращения к базе — «чистые» функции,
/// которые удобно проверять автотестами.
/// </summary>
public static class StockCalculator
{
    /// <summary>
    /// Текущий остаток: Σ поступлений − Σ выдач.
    /// Для товара без операций остаток равен 0.
    /// </summary>
    public static int CalculateBalance(IEnumerable<StockOperation> operations)
    {
        int received = operations
            .Where(o => o.Type == OperationType.Receipt)
            .Sum(o => o.Quantity);

        int issued = operations
            .Where(o => o.Type == OperationType.Issue)
            .Sum(o => o.Quantity);

        return received - issued;
    }

    /// <summary>
    /// Товар требует пополнения, если остаток меньше или РАВЕН минимальному.
    /// Пример: минимум 5, остаток 5 — уже пора заказывать.
    /// </summary>
    public static bool NeedsRestock(int balance, int minQuantity)
    {
        return balance <= minQuantity;
    }
}
