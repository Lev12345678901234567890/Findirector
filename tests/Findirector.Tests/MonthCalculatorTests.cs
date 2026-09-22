using Findirector.Domain;

namespace Findirector.Tests;

public sealed class MonthCalculatorTests
{
    private readonly MonthCalculator calculator = new();
    private static MonthPlan Example(int percent) => new(5, 30_000m, 10_000m, 70_000m, percent);

    [Fact] // К1: оплата сразу, П-Б1–П-Б5
    public void FullPayment_ProducesProfitAndCash()
    {
        MonthReport result = calculator.Calculate(Example(100), 50_000m, 0m);
        Assert.Equal(150_000m, result.Revenue);
        Assert.Equal(120_000m, result.Expenses);
        Assert.Equal(30_000m, result.Profit);
        Assert.Equal(80_000m, result.ClosingCash);
        Assert.Equal(0m, result.ClosingReceivables);
    }

    [Fact] // К2: прибыль при дефиците денег, П-Б2–П-Б6
    public void DeferredPayment_CanLeaveProfitButNoCash()
    {
        MonthReport result = calculator.Calculate(Example(0), 50_000m, 0m);
        Assert.Equal(30_000m, result.Profit);
        Assert.Equal(0m, result.CashReceipts);
        Assert.Equal(-70_000m, result.ClosingCash);
        Assert.Equal(70_000m, result.FundingGap);
        Assert.Equal(150_000m, result.ClosingReceivables);
    }

    [Fact] // К3: частичная оплата, П-Б3–П-Б5
    public void HalfPayment_AvoidsTheFundingGap()
    {
        MonthReport result = calculator.Calculate(Example(50), 50_000m, 0m);
        Assert.Equal(75_000m, result.CashReceipts);
        Assert.Equal(5_000m, result.ClosingCash);
        Assert.Equal(75_000m, result.ClosingReceivables);
        Assert.Equal(30_000m, result.Profit);
    }

    [Fact] // К4: оплата старой задолженности не становится новой выручкой, П-Б4–П-Б6
    public void OpeningReceivables_AreCollectedWithoutAddingRevenue()
    {
        MonthReport result = calculator.Calculate(Example(0), -70_000m, 150_000m);
        Assert.Equal(150_000m, result.Revenue);
        Assert.Equal(150_000m, result.CashReceipts);
        Assert.Equal(30_000m, result.Profit);
        Assert.Equal(-40_000m, result.ClosingCash);
        Assert.Equal(150_000m, result.ClosingReceivables);
    }

    [Fact] // К5: полкопейки округляется от нуля, П-Б9
    public void HalfKopeck_RoundsPaymentButPreservesTotalRevenue()
    {
        MonthReport result = calculator.Calculate(new MonthPlan(1, 0.05m, 0m, 0m, 50), 0m, 0m);
        Assert.Equal(0.03m, result.CashReceipts);
        Assert.Equal(0.02m, result.ClosingReceivables);
        Assert.Equal(0.05m, result.Revenue);
    }

    [Fact] // К6: без продаж постоянные расходы сохраняются, П-Б1–П-Б2
    public void NoSales_StillHasFixedCosts()
    {
        MonthReport result = calculator.Calculate(new MonthPlan(0, 30_000m, 10_000m, 70_000m, 100), 50_000m, 0m);
        Assert.Equal(0m, result.Revenue);
        Assert.Equal(-70_000m, result.Profit);
        Assert.Equal(-20_000m, result.ClosingCash);
    }

    [Fact] // К7: граница дефицита, П-Б6
    public void ExactlyZeroCash_IsNotAFundingGap()
    {
        MonthReport result = calculator.Calculate(Example(0), 120_000m, 0m);
        Assert.Equal(0m, result.ClosingCash);
        Assert.Equal(0m, result.FundingGap);
    }

    [Fact] // К8: ограничения параметров, П-Б8–П-Б9
    public void InvalidInputs_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(-1, 1m, 0m, 0m, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, -1m, 0m, 0m, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, 1m, -1m, 0m, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, 1m, 0m, -1m, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, 1m, 0m, 0m, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, 1m, 0m, 0m, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(1, 0.001m, 0m, 0m, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(Example(50), 0m, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(Example(50), 0.001m, 0m));
    }

    [Fact] // К9: цена ниже себестоимости допустима, П-Б2 и П-Б8
    public void PriceBelowUnitCost_ProducesAValidLoss()
    {
        MonthReport result = calculator.Calculate(new MonthPlan(2, 10m, 15m, 3m, 100), 100m, 0m);
        Assert.Equal(20m, result.Revenue);
        Assert.Equal(33m, result.Expenses);
        Assert.Equal(-13m, result.Profit);
        Assert.Equal(87m, result.ClosingCash);
        Assert.Equal(0m, result.ClosingReceivables);
        Assert.Equal(0m, result.FundingGap);
    }

    [Fact] // К10: повторный вызов не накапливает результаты, С-А2
    public void RepeatedCalculation_DoesNotAccumulateOrChangeInput()
    {
        MonthPlan plan = Example(50);
        MonthReport first = calculator.Calculate(plan, 50_000m, 0m);
        calculator.Calculate(Example(0), 50_000m, 0m);
        MonthReport again = calculator.Calculate(plan, 50_000m, 0m);

        Assert.Equal(first, again);
        Assert.Equal(5, plan.Quantity);
        Assert.Equal(30_000m, plan.Price);
        Assert.Equal(10_000m, plan.UnitCost);
        Assert.Equal(70_000m, plan.FixedCosts);
        Assert.Equal(50, plan.PaidNowPercent);
    }
    [Fact] // ЛР0 пример 1: положительный маржинальный доход, П-Б1
    public void PositiveMargin_RevenueAboveVariableCosts()
    {
        MonthReport result = calculator.Calculate(Example(100), 50_000m, 0m);
        Assert.Equal(100_000m, result.Margin);
    }

    [Fact] // ЛР0 пример 2: отрицательный маржинальный доход, цена ниже переменных затрат, П-Б1
    public void NegativeMargin_PriceBelowUnitCost()
    {
        MonthReport result = calculator.Calculate(new MonthPlan(2, 10m, 15m, 3m, 100), 100m, 0m);
        Assert.Equal(-10m, result.Margin);
    }
}
