using System.Globalization;

namespace Findirector.Web;

/// <summary>Только вид чисел на экране, расчётов здесь нет.</summary>
public static class Format
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    // 50 000 — без копеек, 50 000,50 — с копейками
    public static string Money(decimal value) =>
        value == decimal.Truncate(value) ? value.ToString("N0", Ru) : value.ToString("N2", Ru);
}
