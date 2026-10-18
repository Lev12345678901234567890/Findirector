using Findirector.Application;
using Findirector.Domain;

namespace Findirector.Tests;

/// <summary>ЛР5: поля формы → план и понятные ошибки ввода (П-Ф5).</summary>
public sealed class PlanDraftTests
{
    private readonly PlanService service = new(new InMemoryPlanRepository());

    [Fact] // новый план заполнен контрольными значениями и сразу считается как П2
    public void NewDraft_IsControlPlan()
    {
        PlanDraft draft = PlanDraft.CreateNew("Половина сразу");

        PlanForecast f = service.Forecast(service.Build(draft));

        Assert.NotEqual(Guid.Empty, draft.Id);
        Assert.Equal(new[] { 5_000m, 35_000m, 65_000m }, f.Months.Select(m => m.ClosingCash));
        Assert.Equal(0m, f.FundingNeed);
    }

    [Fact] // П6: A = 120 во втором месяце
    public void PercentOver100_ErrorNamesMonthAndField()
    {
        PlanDraft draft = PlanDraft.CreateNew();
        draft.Months[1].PaidNowPercent = 120;

        var error = Assert.Throws<PlanInputException>(() => service.Build(draft));

        Assert.Equal("Месяц 2, оплата сразу (A)", error.Field);
        Assert.Equal("Процент оплаты должен быть от 0 до 100.", error.Reason);
        Assert.Equal("Месяц 2, оплата сразу (A): Процент оплаты должен быть от 0 до 100.", error.Message);
    }

    [Theory] // каждое поле месяца даёт своё название в сообщении
    [InlineData("quantity", "Месяц 3, количество услуг (Q)")]
    [InlineData("price", "Месяц 3, цена (P)")]
    [InlineData("unitCost", "Месяц 3, переменные затраты на услугу (V)")]
    [InlineData("fixedCosts", "Месяц 3, постоянные расходы (F)")]
    public void NegativeMonthValue_ErrorNamesField(string field, string expected)
    {
        PlanDraft draft = PlanDraft.CreateNew();
        MonthDraft m = draft.Months[2];
        switch (field)
        {
            case "quantity": m.Quantity = -1; break;
            case "price": m.Price = -1m; break;
            case "unitCost": m.UnitCost = -1m; break;
            case "fixedCosts": m.FixedCosts = -1m; break;
        }

        var error = Assert.Throws<PlanInputException>(() => service.Build(draft));

        Assert.Equal(expected, error.Field);
    }

    [Fact]
    public void EmptyName_Error()
    {
        PlanDraft draft = PlanDraft.CreateNew();
        draft.Name = "   ";

        var error = Assert.Throws<PlanInputException>(() => service.Build(draft));

        Assert.Equal("Название", error.Field);
    }

    [Fact]
    public void NegativeOpeningCash_Error()
    {
        PlanDraft draft = PlanDraft.CreateNew();
        draft.OpeningCash = -1m;

        var error = Assert.Throws<PlanInputException>(() => service.Build(draft));

        Assert.Equal("Начальные деньги", error.Field);
        Assert.Equal("Сумма не может быть отрицательной.", error.Reason);
    }

    [Fact]
    public void PriceWithThreeDecimals_Error()
    {
        PlanDraft draft = PlanDraft.CreateNew();
        draft.Months[0].Price = 30_000.005m;

        var error = Assert.Throws<PlanInputException>(() => service.Build(draft));

        Assert.Equal("Месяц 1, цена (P)", error.Field);
        Assert.Equal("Сумма должна задаваться с точностью до копейки.", error.Reason);
    }

    [Fact] // открытый план попадает в форму с тем же Id и значениями
    public void From_KeepsIdAndValues()
    {
        var plan = new FinancialPlan(Guid.NewGuid(), "С копейками", 50_000.50m, new[]
        {
            new MonthPlan(5, 30_000.01m, 10_000.99m, 70_000.05m, 50),
            new MonthPlan(3, 25_000.10m, 9_999.90m, 60_000m, 0),
            new MonthPlan(0, 0.01m, 0m, 1.23m, 100),
        });

        PlanDraft draft = PlanDraft.From(plan);
        FinancialPlan rebuilt = service.Build(draft);

        Assert.Equal(plan.Id, draft.Id);
        Assert.Equal(50_000.50m, draft.OpeningCash);
        Assert.Equal(30_000.01m, draft.Months[0].Price);
        Assert.Equal(service.Forecast(plan).FinalCash, service.Forecast(rebuilt).FinalCash);
    }
}
