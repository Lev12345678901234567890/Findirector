namespace Findirector.Domain;

/// <summary>План на три месяца. Номер месяца — его позиция в списке + 1.</summary>
public sealed class FinancialPlan
{
    public const int MonthCount = 3;

    public Guid Id { get; }
    public string Name { get; }
    public decimal OpeningCash { get; }
    public IReadOnlyList<MonthPlan> Months { get; }

    public FinancialPlan(Guid id, string name, decimal openingCash, IEnumerable<MonthPlan> months)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Идентификатор плана не задан.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Название плана не может быть пустым.", nameof(name));
        Money.Check(openingCash, nameof(openingCash));
        ArgumentNullException.ThrowIfNull(months);

        MonthPlan[] copy = months.ToArray();
        if (copy.Length != MonthCount)
            throw new ArgumentException($"В плане должно быть ровно {MonthCount} месяца.", nameof(months));
        if (copy.Any(month => month is null))
            throw new ArgumentException("Параметры месяца не заданы.", nameof(months));

        Id = id;
        Name = name.Trim();
        OpeningCash = openingCash;
        Months = Array.AsReadOnly(copy);
    }
}
