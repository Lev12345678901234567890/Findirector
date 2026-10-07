namespace Findirector.Domain;

/// <summary>Прогноз плана: отчёты трёх месяцев и итоги П-Б7.</summary>
public sealed class PlanForecast
{
    public IReadOnlyList<MonthReport> Months { get; }

    public PlanForecast(IEnumerable<MonthReport> months)
    {
        ArgumentNullException.ThrowIfNull(months);
        MonthReport[] copy = months.ToArray();
        if (copy.Length != FinancialPlan.MonthCount)
            throw new ArgumentException($"В прогнозе должно быть ровно {FinancialPlan.MonthCount} месяца.", nameof(months));

        Months = Array.AsReadOnly(copy);
    }

    public decimal TotalProfit => Months.Sum(month => month.Profit);
    public decimal FinalCash => Months[^1].ClosingCash;
    public decimal FinalReceivables => Months[^1].ClosingReceivables;

    // Максимум месячных дефицитов, а не сумма: добавленные на старте деньги поднимают все месяцы.
    public decimal FundingNeed => Months.Max(month => month.FundingGap);
}
