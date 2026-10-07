using Findirector.Domain;

namespace Findirector.Application;

/// <summary>Хранилище планов. Реализация — в Infrastructure (SQLite).</summary>
public interface IPlanRepository
{
    /// <summary>Создаёт план или полностью обновляет план с тем же Id.</summary>
    void Save(FinancialPlan plan);

    /// <summary>Все сохранённые планы, по названию.</summary>
    IReadOnlyList<PlanSummary> List();

    /// <summary>План по Id или null, если такого нет.</summary>
    FinancialPlan? Get(Guid id);
}
