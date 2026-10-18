using Findirector.Domain;

namespace Findirector.Application;

/// <summary>Операции над планами. Формул здесь нет — только порядок расчёта.</summary>
public sealed class PlanService
{
    private readonly MonthCalculator calculator = new();
    private readonly IPlanRepository repository;

    public PlanService(IPlanRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
    }

    public PlanForecast Forecast(FinancialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var reports = new List<MonthReport>();
        decimal cash = plan.OpeningCash;
        decimal receivables = 0m; // до первого месяца долгов клиентов нет

        foreach (MonthPlan month in plan.Months)
        {
            MonthReport report = calculator.Calculate(month, cash, receivables);
            reports.Add(report);

            // П-Б5: конец месяца становится началом следующего
            cash = report.ClosingCash;
            receivables = report.ClosingReceivables;
        }

        return new PlanForecast(reports);
    }

    public PlanComparison Compare(FinancialPlan first, FinancialPlan second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return new PlanComparison(first, Forecast(first), second, Forecast(second));
    }

    /// <summary>
    /// Создаёт план из полей формы. Ограничения проверяют конструкторы Domain;
    /// их ошибка превращается в PlanInputException с названием поля и месяца (П-Ф5).
    /// </summary>
    public FinancialPlan Build(PlanDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var months = new List<MonthPlan>();
        for (int i = 0; i < draft.Months.Length; i++)
        {
            MonthDraft m = draft.Months[i];
            try
            {
                months.Add(new MonthPlan(m.Quantity, m.Price, m.UnitCost, m.FixedCosts, m.PaidNowPercent));
            }
            catch (ArgumentException error)
            {
                throw new PlanInputException($"Месяц {i + 1}, {FieldName(error.ParamName)}", Reason(error), error);
            }
        }

        try
        {
            return new FinancialPlan(draft.Id, draft.Name ?? "", draft.OpeningCash, months);
        }
        catch (ArgumentException error)
        {
            throw new PlanInputException(FieldName(error.ParamName), Reason(error), error);
        }
    }

    public void Save(FinancialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        repository.Save(plan);
    }

    public IReadOnlyList<PlanSummary> List() => repository.List();

    public FinancialPlan Open(Guid id) =>
        repository.Get(id) ?? throw new KeyNotFoundException("План не найден.");

    // Имена параметров конструкторов Domain → подписи полей на экране
    private static string FieldName(string? parameter) => parameter switch
    {
        "quantity" => "количество услуг (Q)",
        "price" => "цена (P)",
        "unitCost" => "переменные затраты на услугу (V)",
        "fixedCosts" => "постоянные расходы (F)",
        "paidNowPercent" => "оплата сразу (A)",
        "name" => "Название",
        "openingCash" => "Начальные деньги",
        "id" => "Идентификатор",
        _ => "План",
    };

    // Message у ArgumentException заканчивается на " (Parameter 'x')" — оставляем только причину
    private static string Reason(ArgumentException error) => error.Message.Split(" (Parameter")[0];
}
