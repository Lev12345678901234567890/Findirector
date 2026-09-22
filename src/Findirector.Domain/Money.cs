namespace Findirector.Domain;

internal static class Money
{
    public static void Check(decimal amount, string parameter, bool allowNegative = false)
    {
        if (!allowNegative && amount < 0)
            throw new ArgumentOutOfRangeException(parameter, "Сумма не может быть отрицательной.");
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(parameter, "Сумма должна задаваться с точностью до копейки.");
    }
}
