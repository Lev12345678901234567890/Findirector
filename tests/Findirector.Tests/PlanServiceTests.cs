using Findirector.Application;
using Findirector.Domain;

namespace Findirector.Tests;

public sealed class PlanServiceTests
{
    private readonly PlanService service = new(new InMemoryPlanRepository());

    // Контрольный план из постановки: 50 000 на старте, каждый месяц Q = 5, P = 30 000, V = 10 000, F = 70 000
    private static FinancialPlan ControlPlan(int percent, decimal openingCash = 50_000m, string name = "Контрольный")
    {
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, percent);
        return new FinancialPlan(Guid.NewGuid(), name, openingCash, new[] { month, month, month });
    }

    // План с разными параметрами месяцев, ожидания посчитаны вручную (см. docs/requirements.md, П9)
    private static FinancialPlan DifferentMonthsPlan() => new(Guid.NewGuid(), "Разные месяцы", 10_000m, new[]
    {
        new MonthPlan(2, 20_000m, 5_000m, 30_000m, 50),
        new MonthPlan(4, 25_000m, 5_000m, 30_000m, 25),
        new MonthPlan(1, 30_000m, 10_000m, 30_000m, 100),
    });

    private static decimal[] Cash(PlanForecast f) => f.Months.Select(m => m.ClosingCash).ToArray();
    private static decimal[] Receipts(PlanForecast f) => f.Months.Select(m => m.CashReceipts).ToArray();

    // ---------- ЛР3-Т1: примеры ЛР1 ----------

    [Fact] // П1: A = 0%, П-Б3–П-Б7
    public void ZeroPercent_ProfitButCashGap()
    {
        PlanForecast f = service.Forecast(ControlPlan(0));

        Assert.Equal(new[] { 0m, 150_000m, 150_000m }, Receipts(f));
        Assert.Equal(new[] { -70_000m, -40_000m, -10_000m }, Cash(f));
        Assert.All(f.Months, m => Assert.Equal(30_000m, m.Profit));
        Assert.Equal(90_000m, f.TotalProfit);
        Assert.Equal(-10_000m, f.FinalCash);
        Assert.Equal(150_000m, f.FinalReceivables);
        Assert.Equal(70_000m, f.FundingNeed);
    }

    [Fact] // П2: A = 50%
    public void HalfPercent_NoFundingNeed()
    {
        PlanForecast f = service.Forecast(ControlPlan(50));

        Assert.Equal(new[] { 75_000m, 150_000m, 150_000m }, Receipts(f));
        Assert.Equal(new[] { 5_000m, 35_000m, 65_000m }, Cash(f));
        Assert.Equal(90_000m, f.TotalProfit);
        Assert.Equal(75_000m, f.FinalReceivables);
        Assert.Equal(0m, f.FundingNeed);
    }

    [Fact] // П3: A = 100%
    public void FullPercent_NoReceivables()
    {
        PlanForecast f = service.Forecast(ControlPlan(100));

        Assert.Equal(new[] { 80_000m, 110_000m, 140_000m }, Cash(f));
        Assert.All(f.Months, m => Assert.Equal(0m, m.ClosingReceivables));
        Assert.Equal(90_000m, f.TotalProfit);
        Assert.Equal(0m, f.FundingNeed);
    }

    [Fact] // П4: нет продаж, П-Б2, П-Б6
    public void NoSales_FixedCostsMakeLoss()
    {
        var month = new MonthPlan(0, 30_000m, 10_000m, 70_000m, 50);
        var plan = new FinancialPlan(Guid.NewGuid(), "Без продаж", 50_000m, new[] { month, month, month });

        PlanForecast f = service.Forecast(plan);

        Assert.All(f.Months, m =>
        {
            Assert.Equal(0m, m.Revenue);
            Assert.Equal(0m, m.Margin);
            Assert.Equal(-70_000m, m.Profit);
        });
        Assert.Equal(new[] { -20_000m, -90_000m, -160_000m }, Cash(f));
        Assert.Equal(-210_000m, f.TotalProfit);
        Assert.Equal(0m, f.FinalReceivables);
        Assert.Equal(160_000m, f.FundingNeed);
    }

    [Fact] // П5: C1 = 0 — дефицита нет, П-Б6
    public void ExactlyZeroCash_IsNotAFundingNeed()
    {
        PlanForecast f = service.Forecast(ControlPlan(0, openingCash: 120_000m));

        Assert.Equal(new[] { 0m, 30_000m, 60_000m }, Cash(f));
        Assert.Equal(0m, f.FundingNeed);
    }

    [Fact] // П6: процент вне 0–100 — план не создаётся, расчёта нет, П-Ф5
    public void InvalidPercent_IsRejectedWithFieldName()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MonthPlan(5, 30_000m, 10_000m, 70_000m, 120));

        Assert.Equal("paidNowPercent", error.ParamName);
    }

    [Fact] // П9: разные параметры месяцев
    public void DifferentMonths_AreCalculatedOneByOne()
    {
        PlanForecast f = service.Forecast(DifferentMonthsPlan());

        Assert.Equal(new[] { 0m, 50_000m, -10_000m }, f.Months.Select(m => m.Profit).ToArray());
        Assert.Equal(new[] { 20_000m, 45_000m, 105_000m }, Receipts(f));
        Assert.Equal(new[] { -10_000m, -15_000m, 50_000m }, Cash(f));
        Assert.Equal(40_000m, f.TotalProfit);
        Assert.Equal(0m, f.FinalReceivables);
    }

    // ---------- ЛР3-Т2: перенос, минус, сравнение, повтор, неизменность ----------

    [Fact] // П-Б4, П-Б5: долг конца месяца приходит в следующем
    public void Receivables_AreCollectedNextMonth()
    {
        PlanForecast f = service.Forecast(DifferentMonthsPlan());

        // поступления месяца 2 = оплата сразу 25 000 + долг месяца 1 20 000
        Assert.Equal(20_000m, f.Months[0].ClosingReceivables);
        Assert.Equal(25_000m + f.Months[0].ClosingReceivables, f.Months[1].CashReceipts);
        Assert.Equal(30_000m + f.Months[1].ClosingReceivables, f.Months[2].CashReceipts);
    }

    [Fact] // П-Б6: прогноз продолжается после минуса
    public void NegativeCash_DoesNotStopForecast()
    {
        PlanForecast f = service.Forecast(ControlPlan(0));

        Assert.Equal(3, f.Months.Count);
        Assert.True(f.Months[0].ClosingCash < 0);
        Assert.Equal(f.Months[0].ClosingCash + 150_000m - 120_000m, f.Months[1].ClosingCash);
    }

    [Fact] // П-Б7: потребность — максимум дефицитов, а не сумма
    public void FundingNeed_IsMaxNotSum()
    {
        PlanForecast f = service.Forecast(DifferentMonthsPlan());

        Assert.Equal(25_000m, f.Months.Sum(m => m.FundingGap));
        Assert.Equal(15_000m, f.FundingNeed);
    }

    [Fact] // П7: сравнение двух планов, П-Ф3
    public void Compare_ShowsSameProfitDifferentCash()
    {
        FinancialPlan later = ControlPlan(0, name: "Всё через месяц");
        FinancialPlan half = ControlPlan(50, name: "Половина сразу");

        PlanComparison c = service.Compare(later, half);

        Assert.Same(later, c.First);
        Assert.Same(half, c.Second);
        Assert.Equal(90_000m, c.FirstForecast.TotalProfit);
        Assert.Equal(90_000m, c.SecondForecast.TotalProfit);
        Assert.Equal(-10_000m, c.FirstForecast.FinalCash);
        Assert.Equal(65_000m, c.SecondForecast.FinalCash);
        Assert.Equal(150_000m, c.FirstForecast.FinalReceivables);
        Assert.Equal(75_000m, c.SecondForecast.FinalReceivables);
        Assert.Equal(70_000m, c.FirstForecast.FundingNeed);
        Assert.Equal(0m, c.SecondForecast.FundingNeed);
    }

    [Fact] // П-Ф3: план в сравнении считается так же, как отдельно
    public void Compare_PlansDoNotAffectEachOther()
    {
        FinancialPlan plan = ControlPlan(50);

        PlanComparison c = service.Compare(ControlPlan(0), plan);

        Assert.Equal(Cash(service.Forecast(plan)), Cash(c.SecondForecast));
    }

    [Fact] // П-Ф6: повторный расчёт начинается заново
    public void RepeatedForecast_GivesSameResult()
    {
        FinancialPlan plan = ControlPlan(50);

        PlanForecast first = service.Forecast(plan);
        service.Forecast(ControlPlan(0));
        PlanForecast again = service.Forecast(plan);

        Assert.Equal(first.Months, again.Months);
        Assert.Equal(first.FundingNeed, again.FundingNeed);
    }

    [Fact] // расчёт и сравнение не меняют исходные данные
    public void ForecastAndCompare_DoNotChangePlan()
    {
        FinancialPlan plan = DifferentMonthsPlan();
        MonthPlan[] before = plan.Months.ToArray();

        service.Forecast(plan);
        service.Compare(plan, plan);

        Assert.Equal(10_000m, plan.OpeningCash);
        Assert.Equal(before, plan.Months);
        Assert.Equal(25, plan.Months[1].PaidNowPercent);
    }

    [Fact] // план хранит свою копию месяцев
    public void ChangingSourceArray_DoesNotChangePlan()
    {
        var months = new[]
        {
            new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50),
            new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50),
            new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50),
        };
        var plan = new FinancialPlan(Guid.NewGuid(), "Копия", 50_000m, months);

        months[0] = new MonthPlan(0, 0m, 0m, 0m, 0);

        Assert.Equal(5, plan.Months[0].Quantity);
    }

    // ---------- ЛР3-Т3: отказ для недопустимого плана ----------

    [Fact] // П-Б8: деньги до первого месяца не отрицательные
    public void NegativeOpeningCash_IsRejected()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ControlPlan(50, openingCash: -1m));
        Assert.Equal("openingCash", error.ParamName);
    }

    [Theory] // П-Б10: название не пустое
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyName_IsRejected(string? name)
    {
        var error = Assert.Throws<ArgumentException>(() => ControlPlan(50, name: name!));
        Assert.Equal("name", error.ParamName);
    }

    [Theory] // П-Б10: ровно три месяца
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void WrongMonthCount_IsRejected(int count)
    {
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50);
        var months = Enumerable.Repeat(month, count);

        var error = Assert.Throws<ArgumentException>(
            () => new FinancialPlan(Guid.NewGuid(), "План", 50_000m, months));
        Assert.Equal("months", error.ParamName);
    }

    [Fact] // П-Б10: месяц не может быть пропущен
    public void MissingMonth_IsRejected()
    {
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50);

        Assert.Throws<ArgumentException>(
            () => new FinancialPlan(Guid.NewGuid(), "План", 50_000m, new[] { month, null!, month }));
    }

    [Fact] // П-Б10: у плана есть идентификатор
    public void EmptyId_IsRejected()
    {
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50);

        var error = Assert.Throws<ArgumentException>(
            () => new FinancialPlan(Guid.Empty, "План", 50_000m, new[] { month, month, month }));
        Assert.Equal("id", error.ParamName);
    }

    [Fact]
    public void NullPlan_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => service.Forecast(null!));
    }
}
