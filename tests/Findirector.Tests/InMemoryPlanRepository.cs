using Findirector.Application;
using Findirector.Domain;

namespace Findirector.Tests;

/// <summary>Простое хранилище в памяти для тестов расчёта, где база не нужна.</summary>
internal sealed class InMemoryPlanRepository : IPlanRepository
{
    private readonly Dictionary<Guid, FinancialPlan> plans = new();

    public void Save(FinancialPlan plan) => plans[plan.Id] = plan;

    public IReadOnlyList<PlanSummary> List() =>
        plans.Values.OrderBy(p => p.Name).Select(p => new PlanSummary(p.Id, p.Name)).ToList();

    public FinancialPlan? Get(Guid id) => plans.GetValueOrDefault(id);
}
