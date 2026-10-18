using Findirector.Application;
using Findirector.Domain;
using Findirector.Infrastructure;

namespace Findirector.Tests;

/// <summary>
/// ЛР5-Т1: сквозная проверка через те же операции, что вызывает UI, и настоящий файл SQLite.
/// «Перезапуск» — новый экземпляр хранилища и сервиса на том же файле.
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"findirector-e2e-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    private PlanService StartApp() => new(new SqlitePlanRepository(dbPath));

    [Fact]
    public void CreateCompareSaveRestartOpenChangeAndReject()
    {
        // 1. Создать два плана (как кнопка «Новый план» и ввод полей)
        PlanService app = StartApp();
        PlanDraft later = PlanDraft.CreateNew("Всё через месяц");
        foreach (MonthDraft m in later.Months) m.PaidNowPercent = 0;
        PlanDraft half = PlanDraft.CreateNew("Половина сразу");

        // 2. Расчёт (П1 и П2)
        PlanForecast laterForecast = app.Forecast(app.Build(later));
        PlanForecast halfForecast = app.Forecast(app.Build(half));
        Assert.Equal(new[] { -70_000m, -40_000m, -10_000m }, laterForecast.Months.Select(m => m.ClosingCash));
        Assert.All(laterForecast.Months, m => Assert.True(m.FundingGap > 0)); // на экране «Не хватает денег»
        Assert.Equal(65_000m, halfForecast.FinalCash);

        // 3. Сравнение (П7)
        PlanComparison comparison = app.Compare(app.Build(later), app.Build(half));
        Assert.Equal(90_000m, comparison.FirstForecast.TotalProfit);
        Assert.Equal(90_000m, comparison.SecondForecast.TotalProfit);
        Assert.Equal(70_000m, comparison.FirstForecast.FundingNeed);
        Assert.Equal(0m, comparison.SecondForecast.FundingNeed);

        // 4. Сохранение
        app.Save(app.Build(later));
        app.Save(app.Build(half));

        // 5. Перезапуск и открытие из списка — те же результаты
        PlanService restarted = StartApp();
        Assert.Equal(new[] { "Всё через месяц", "Половина сразу" }, restarted.List().Select(p => p.Name));
        PlanDraft opened = PlanDraft.From(restarted.Open(half.Id));
        PlanForecast reopened = restarted.Forecast(restarted.Build(opened));
        Assert.Equal(halfForecast.Months.Select(m => m.ClosingCash), reopened.Months.Select(m => m.ClosingCash));
        Assert.Equal(halfForecast.FinalReceivables, reopened.FinalReceivables);

        // 6. Изменение (A = 100), пересчёт с исходных данных (П3) и повторное сохранение без дубля
        foreach (MonthDraft m in opened.Months) m.PaidNowPercent = 100;
        PlanForecast changed = restarted.Forecast(restarted.Build(opened));
        Assert.Equal(new[] { 80_000m, 110_000m, 140_000m }, changed.Months.Select(m => m.ClosingCash));
        restarted.Save(restarted.Build(opened));
        Assert.Equal(2, restarted.List().Count);
        Assert.Equal(100, StartApp().Open(half.Id).Months[0].PaidNowPercent);

        // 7. Ошибочный ввод: расчёт и сохранение не выполняются (П6)
        opened.Months[1].PaidNowPercent = 120;
        var error = Assert.Throws<PlanInputException>(() => restarted.Build(opened));
        Assert.StartsWith("Месяц 2, оплата сразу (A)", error.Message);
        Assert.Equal(100, StartApp().Open(half.Id).Months[1].PaidNowPercent); // в базе прежние данные
    }
}
