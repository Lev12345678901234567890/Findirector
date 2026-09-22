namespace Findirector.Domain;

public sealed class MonthCalculator
{
    public MonthReport Calculate(MonthPlan plan, decimal openingCash, decimal openingReceivables)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Money.Check(openingCash, nameof(openingCash), allowNegative: true);
        Money.Check(openingReceivables, nameof(openingReceivables));

        decimal revenue = plan.Quantity * plan.Price;
        decimal variableCosts = plan.Quantity * plan.UnitCost;
        decimal margin = revenue - variableCosts;
        decimal paidNow = decimal.Round(revenue * plan.PaidNowPercent / 100m,
            2, MidpointRounding.AwayFromZero);
        decimal receipts = openingReceivables + paidNow;
        decimal closingCash = openingCash + receipts - variableCosts - plan.FixedCosts;
        decimal receivables = revenue - paidNow;

        return new MonthReport(revenue, variableCosts, plan.FixedCosts, margin,
            receipts, closingCash, receivables);
    }
}