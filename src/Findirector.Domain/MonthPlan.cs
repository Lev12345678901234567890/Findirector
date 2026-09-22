namespace Findirector.Domain;

/// <summary>Параметры одного месяца. После создания менять их нельзя.</summary>
public sealed class MonthPlan
{
    public int Quantity { get; }
    public decimal Price { get; }
    public decimal UnitCost { get; }
    public decimal FixedCosts { get; }
    public int PaidNowPercent { get; }

    public MonthPlan(int quantity, decimal price, decimal unitCost,
        decimal fixedCosts, int paidNowPercent)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Количество не может быть отрицательным.");
        if (paidNowPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(paidNowPercent), "Процент оплаты должен быть от 0 до 100.");

        Money.Check(price, nameof(price));
        Money.Check(unitCost, nameof(unitCost));
        Money.Check(fixedCosts, nameof(fixedCosts));

        Quantity = quantity;
        Price = price;
        UnitCost = unitCost;
        FixedCosts = fixedCosts;
        PaidNowPercent = paidNowPercent;
    }
}
