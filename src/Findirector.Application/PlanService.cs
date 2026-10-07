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

    public void Save(FinancialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        repository.Save(plan);
    }

    public IReadOnlyList<PlanSummary> List() => repository.List();

    public FinancialPlan Open(Guid id) =>
        repository.Get(id) ?? throw new KeyNotFoundException("План не найден.");
}
