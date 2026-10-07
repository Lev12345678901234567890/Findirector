using Findirector.Domain;

namespace Findirector.Application;

/// <summary>Результат сравнения двух планов, каждый посчитан отдельно.</summary>
public sealed record PlanComparison(
    FinancialPlan First,
    PlanForecast FirstForecast,
    FinancialPlan Second,
    PlanForecast SecondForecast);
