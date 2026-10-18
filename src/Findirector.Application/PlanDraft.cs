using Findirector.Domain;

namespace Findirector.Application;

/// <summary>Поля формы плана: то, что пользователь сейчас ввёл. Проверки — в PlanService.Build.</summary>
public sealed class PlanDraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal OpeningCash { get; set; }
    public MonthDraft[] Months { get; set; } = { new(), new(), new() };

    /// <summary>Новый план с контрольными значениями из постановки и новым Id.</summary>
    public static PlanDraft CreateNew(string name = "Новый план") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OpeningCash = 50_000m,
        Months = new[] { MonthDraft.Control(), MonthDraft.Control(), MonthDraft.Control() },
    };

    /// <summary>Заполняет форму из открытого плана; Id сохраняется, чтобы повторное сохранение обновило план.</summary>
    public static PlanDraft From(FinancialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new PlanDraft
        {
            Id = plan.Id,
            Name = plan.Name,
            OpeningCash = plan.OpeningCash,
            Months = plan.Months.Select(m => new MonthDraft
            {
                Quantity = m.Quantity,
                Price = m.Price,
                UnitCost = m.UnitCost,
                FixedCosts = m.FixedCosts,
                PaidNowPercent = m.PaidNowPercent,
            }).ToArray(),
        };
    }
}

/// <summary>Поля одного месяца в форме.</summary>
public sealed class MonthDraft
{
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal UnitCost { get; set; }
    public decimal FixedCosts { get; set; }
    public int PaidNowPercent { get; set; }

    public static MonthDraft Control() => new()
    {
        Quantity = 5, Price = 30_000m, UnitCost = 10_000m, FixedCosts = 70_000m, PaidNowPercent = 50,
    };
}
