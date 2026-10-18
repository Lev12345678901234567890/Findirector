using Bunit;
using Findirector.Application;
using Findirector.Domain;
using Findirector.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Findirector.Tests;

/// <summary>ЛР5: проверки экранов Blazor без браузера (bUnit).</summary>
public sealed class UiTests : BunitContext
{
    private readonly InMemoryPlanRepository repository = new();

    public UiTests()
    {
        Services.AddSingleton<IPlanRepository>(repository);
        Services.AddSingleton<PlanService>();
    }

    private static string[] Column(IRenderedComponent<PlanPage> page, int column) =>
        page.FindAll("#forecast tbody tr").Select(r => r.Children[column].TextContent).ToArray();

    [Fact] // ЛР5-Ф1, Ф2: расчёт контрольного плана П2, таблица и итоги
    public void Calculate_ShowsMonthsAndTotals()
    {
        var page = Render<PlanPage>();

        page.Find("#calculate").Click();

        Assert.Equal(new[] { "5 000", "35 000", "65 000" }, Column(page, 6).Select(s => s.Replace(' ', ' ')));
        string totals = page.Find("#totals").TextContent.Replace(' ', ' ');
        Assert.Contains("90 000", totals);
        Assert.Contains("75 000", totals);
        Assert.Empty(page.FindAll("tr.shortage"));
    }

    [Fact] // ЛР5-Ф2: при A = 0 все три месяца в минусе и подписаны
    public void NegativeCash_HasShortageLabel()
    {
        var page = Render<PlanPage>();
        for (int i = 1; i <= 3; i++)
            page.Find($"#a{i}").Change("0");

        page.Find("#calculate").Click();

        Assert.Equal(3, page.FindAll("tr.shortage").Count);
        Assert.All(Column(page, 8), note => Assert.Equal("Не хватает денег", note));
    }

    [Fact] // П-Ф6: изменение поля скрывает прежний прогноз
    public void ChangingInput_HidesForecast()
    {
        var page = Render<PlanPage>();
        page.Find("#calculate").Click();
        Assert.NotEmpty(page.FindAll("#forecast"));

        page.Find("#q1").Change("6");

        Assert.Empty(page.FindAll("#forecast"));
    }

    [Fact] // П-Ф5: ошибка с полем и месяцем, прогноз не строится, план не сохраняется
    public void WrongPercent_ShowsErrorAndDoesNotSave()
    {
        var page = Render<PlanPage>();
        page.Find("#a2").Change("120");

        page.Find("#calculate").Click();
        page.Find("#save").Click();

        Assert.Equal("Месяц 2, оплата сразу (A): Процент оплаты должен быть от 0 до 100.", page.Find("#error").TextContent);
        Assert.Empty(page.FindAll("#forecast"));
        Assert.Empty(repository.List());
    }

    [Fact] // ЛР5-Ф1: сохранить, создать новый, открыть из списка
    public void SaveAndOpenFromList()
    {
        var page = Render<PlanPage>();
        page.Find("#name").Change("Мой план");
        page.Find("#opening-cash").Change("50000.50");
        page.Find("#save").Click();
        Assert.Contains("сохранён", page.Find("#status").TextContent);

        page.Find("#new-plan").Click();
        Assert.Equal("Новый план", page.Find("#name").GetAttribute("value"));

        page.Find(".saved li button").Click();

        Assert.Equal("Мой план", page.Find("#name").GetAttribute("value"));
        Assert.Equal("50000.50", page.Find("#opening-cash").GetAttribute("value"));
    }

    private void SaveControl(string name, int percent)
    {
        PlanDraft draft = PlanDraft.CreateNew(name);
        foreach (MonthDraft m in draft.Months) m.PaidNowPercent = percent;
        var service = Services.GetRequiredService<PlanService>();
        service.Save(service.Build(draft));
    }

    [Fact] // ЛР5-Ф3, П7: итоги двух планов рядом
    public void Compare_ShowsTwoPlansSideBySide()
    {
        SaveControl("Всё через месяц", 0);
        SaveControl("Половина сразу", 50);
        var page = Render<ComparePage>();
        var ids = page.FindAll("#first option").Skip(1).Select(o => o.GetAttribute("value")!).ToArray();

        page.Find("#first").Change(ids[0]);
        page.Find("#second").Change(ids[1]);
        page.Find("#compare").Click();

        string table = page.Find("#comparison").TextContent.Replace(' ', ' ');
        Assert.Contains("Всё через месяц", table);
        Assert.Contains("-10 000 — Не хватает денег", table.Replace('−', '-'));
        Assert.Contains("65 000", table);
        Assert.Contains("70 000", table);
    }

    [Fact] // П-Ф6: смена плана скрывает сравнение; один и тот же план — ошибка
    public void Compare_ChangeHidesResult_SamePlanIsError()
    {
        SaveControl("Всё через месяц", 0);
        SaveControl("Половина сразу", 50);
        var page = Render<ComparePage>();
        var ids = page.FindAll("#first option").Skip(1).Select(o => o.GetAttribute("value")!).ToArray();
        page.Find("#first").Change(ids[0]);
        page.Find("#second").Change(ids[1]);
        page.Find("#compare").Click();
        Assert.NotEmpty(page.FindAll("#comparison"));

        page.Find("#second").Change(ids[0]);
        Assert.Empty(page.FindAll("#comparison"));

        page.Find("#compare").Click();
        Assert.Equal("Выберите два разных плана.", page.Find("#error").TextContent);
    }
}
