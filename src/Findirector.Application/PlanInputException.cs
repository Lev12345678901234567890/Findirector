namespace Findirector.Application;

/// <summary>Ошибка ввода (П-Ф5): в каком поле и почему. UI показывает Message как есть.</summary>
public sealed class PlanInputException : Exception
{
    public string Field { get; }
    public string Reason { get; }

    public PlanInputException(string field, string reason, Exception inner)
        : base($"{field}: {reason}", inner)
    {
        Field = field;
        Reason = reason;
    }
}
