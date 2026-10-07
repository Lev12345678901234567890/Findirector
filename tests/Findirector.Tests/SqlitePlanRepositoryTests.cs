using Findirector.Application;
using Findirector.Domain;
using Findirector.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Findirector.Tests;

/// <summary>Интеграционные тесты ЛР4: настоящий файл SQLite во временной папке.</summary>
public sealed class SqlitePlanRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"findirector-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    // План с копейками во всех денежных полях и разными месяцами
    private static FinancialPlan PlanWithKopecks(Guid id, string name = "С копейками") => new(id, name, 50_000.50m, new[]
    {
        new MonthPlan(5, 30_000.01m, 10_000.99m, 70_000.05m, 50),
        new MonthPlan(3, 25_000.10m, 9_999.90m, 60_000.00m, 0),
        new MonthPlan(0, 0.01m, 0.00m, 1.23m, 100),
    });

    private static FinancialPlan ControlPlan(Guid id, string name, int percent)
    {
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, percent);
        return new FinancialPlan(id, name, 50_000m, new[] { month, month, month });
    }

    private static void AssertSamePlan(FinancialPlan expected, FinancialPlan actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.OpeningCash, actual.OpeningCash);
        Assert.Equal(expected.Months.Count, actual.Months.Count);
        for (int i = 0; i < expected.Months.Count; i++)
        {
            Assert.Equal(expected.Months[i].Quantity, actual.Months[i].Quantity);
            Assert.Equal(expected.Months[i].Price, actual.Months[i].Price);
            Assert.Equal(expected.Months[i].UnitCost, actual.Months[i].UnitCost);
            Assert.Equal(expected.Months[i].FixedCosts, actual.Months[i].FixedCosts);
            Assert.Equal(expected.Months[i].PaidNowPercent, actual.Months[i].PaidNowPercent);
        }
    }

    [Fact] // ЛР4: схема создаётся при первом запуске
    public void FirstRun_CreatesEmptyDatabase()
    {
        var repository = new SqlitePlanRepository(dbPath);

        Assert.True(File.Exists(dbPath));
        Assert.Empty(repository.List());
    }

    [Fact] // ЛР4-Т1: после открытия новым экземпляром все значения и копейки совпадают
    public void SaveAndOpen_WithNewInstance_KeepsKopecks()
    {
        FinancialPlan plan = PlanWithKopecks(Guid.NewGuid());
        new SqlitePlanRepository(dbPath).Save(plan);

        FinancialPlan? opened = new SqlitePlanRepository(dbPath).Get(plan.Id);

        Assert.NotNull(opened);
        AssertSamePlan(plan, opened);
        Assert.Equal(50_000.50m, opened.OpeningCash);
        Assert.Equal(30_000.01m, opened.Months[0].Price);
        Assert.Equal(0.01m, opened.Months[2].Price);
    }

    [Fact] // ЛР4-Т2: прогноз после открытия совпадает с исходным
    public void Forecast_AfterOpen_IsTheSame()
    {
        FinancialPlan plan = PlanWithKopecks(Guid.NewGuid());
        var service = new PlanService(new SqlitePlanRepository(dbPath));
        PlanForecast before = service.Forecast(plan);
        service.Save(plan);

        var restarted = new PlanService(new SqlitePlanRepository(dbPath));
        PlanForecast after = restarted.Forecast(restarted.Open(plan.Id));

        Assert.Equal(before.Months, after.Months);
        Assert.Equal(before.TotalProfit, after.TotalProfit);
        Assert.Equal(before.FundingNeed, after.FundingNeed);
    }

    [Fact] // П8 из ЛР1: 50 000,50 на старте, A = 50% → числа П2 + 0,50
    public void Example8_SaveRestartOpenAndRecalculate()
    {
        var id = Guid.NewGuid();
        var month = new MonthPlan(5, 30_000m, 10_000m, 70_000m, 50);
        var plan = new FinancialPlan(id, "Половина сразу", 50_000.50m, new[] { month, month, month });
        new PlanService(new SqlitePlanRepository(dbPath)).Save(plan);

        var service = new PlanService(new SqlitePlanRepository(dbPath));
        PlanForecast f = service.Forecast(service.Open(id));

        Assert.Equal(new[] { 5_000.50m, 35_000.50m, 65_000.50m }, f.Months.Select(m => m.ClosingCash).ToArray());
        Assert.Equal(75_000m, f.FinalReceivables);
        Assert.Equal(0m, f.FundingNeed);
    }

    [Fact] // ЛР4-Т2, П-Ф4: тот же Id — обновление всех данных без дубликата
    public void SaveAgain_UpdatesPlanWithoutDuplicate()
    {
        var id = Guid.NewGuid();
        var repository = new SqlitePlanRepository(dbPath);
        repository.Save(ControlPlan(id, "Черновик", 50));

        var changedMonth = new MonthPlan(7, 31_000.25m, 11_000m, 65_000m, 100);
        var changed = new FinancialPlan(id, "Итоговый", 12_345.67m, new[] { changedMonth, changedMonth, changedMonth });
        repository.Save(changed);

        PlanSummary single = Assert.Single(repository.List());
        Assert.Equal("Итоговый", single.Name);
        AssertSamePlan(changed, new SqlitePlanRepository(dbPath).Get(id)!);
    }

    [Fact] // ЛР4-Т2: повторное сохранение не меняет другой план
    public void SaveAgain_DoesNotTouchOtherPlan()
    {
        var repository = new SqlitePlanRepository(dbPath);
        FinancialPlan other = PlanWithKopecks(Guid.NewGuid(), "Другой");
        var id = Guid.NewGuid();
        repository.Save(other);
        repository.Save(ControlPlan(id, "Мой", 0));

        repository.Save(ControlPlan(id, "Мой изменённый", 100));

        Assert.Equal(2, repository.List().Count);
        AssertSamePlan(other, repository.Get(other.Id)!);
    }

    [Fact] // ЛР4-Ф1: список всех планов по названию
    public void List_ReturnsAllPlansSortedByName()
    {
        var repository = new SqlitePlanRepository(dbPath);
        repository.Save(ControlPlan(Guid.NewGuid(), "Половина сразу", 50));
        repository.Save(ControlPlan(Guid.NewGuid(), "Всё через месяц", 0));

        Assert.Equal(new[] { "Всё через месяц", "Половина сразу" }, repository.List().Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        Assert.Null(new SqlitePlanRepository(dbPath).Get(Guid.NewGuid()));
    }

    [Fact] // С3: открыть несуществующий план — «План не найден»
    public void Open_UnknownId_Throws()
    {
        var service = new PlanService(new SqlitePlanRepository(dbPath));

        var error = Assert.Throws<KeyNotFoundException>(() => service.Open(Guid.NewGuid()));
        Assert.Equal("План не найден.", error.Message);
    }

    [Fact] // П-Ф5: недопустимый план не создаётся, поэтому сохранять нечего
    public void InvalidPlan_IsNeverSaved()
    {
        var repository = new SqlitePlanRepository(dbPath);

        Assert.ThrowsAny<ArgumentException>(() =>
            repository.Save(new FinancialPlan(Guid.NewGuid(), "", -1m, Array.Empty<MonthPlan>())));
        Assert.Throws<ArgumentNullException>(() => repository.Save(null!));
        Assert.Empty(repository.List());
    }

    [Fact] // ЛР4-Д1: в одном плане номер месяца уникален — база не даст записать дубль
    public void Database_RejectsDuplicateMonthNumber()
    {
        var id = Guid.NewGuid();
        new SqlitePlanRepository(dbPath).Save(ControlPlan(id, "План", 50));

        var error = Assert.Throws<SqliteException>(() => RawInsertMonth(id.ToString(), monthNumber: 2));
        Assert.Contains("UNIQUE", error.Message);
    }

    [Fact] // ЛР4-Д1: месяц без плана запрещён внешним ключом
    public void Database_RejectsMonthWithoutPlan()
    {
        _ = new SqlitePlanRepository(dbPath);

        var error = Assert.Throws<SqliteException>(() => RawInsertMonth(Guid.NewGuid().ToString(), monthNumber: 1));
        Assert.Contains("FOREIGN KEY", error.Message);
    }

    // Прямая вставка в обход репозитория — проверяем ограничения самой схемы
    private void RawInsertMonth(string planId, int monthNumber)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            INSERT INTO MonthPlans (PlanId, MonthNumber, Quantity, PriceKopecks, UnitCostKopecks, FixedCostsKopecks, PaidNowPercent)
            VALUES ($id, $number, 1, 100, 100, 100, 50);
            """;
        command.Parameters.AddWithValue("$id", planId);
        command.Parameters.AddWithValue("$number", monthNumber);
        command.ExecuteNonQuery();
    }
}
