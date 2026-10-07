using System.Globalization;
using System.Text;
using Findirector.Application;
using Findirector.Domain;
using Findirector.Infrastructure;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

Console.WriteLine("Финдиректор: прибыль есть — денег нет?");
Console.WriteLine("План на три месяца. На старте 50 000 руб. Каждый месяц 5 сайтов по 30 000 руб.");
Console.WriteLine("Затраты: 10 000 руб. на сайт и 70 000 руб. постоянных расходов.");

var service = new PlanService(new SqlitePlanRepository("findirector.db"));
FinancialPlan later = ControlPlan(new Guid("11111111-0000-0000-0000-000000000001"), "Всё через месяц", 0);
FinancialPlan half = ControlPlan(new Guid("11111111-0000-0000-0000-000000000002"), "Половина сразу", 50);

PrintForecast(later, service.Forecast(later));
PrintForecast(half, service.Forecast(half));

PlanComparison comparison = service.Compare(later, half);
Console.WriteLine("\nСравнение планов");
Console.WriteLine($"{"",-30}{comparison.First.Name,18}{comparison.Second.Name,18}");
PrintRow("Прибыль за 3 месяца", comparison.FirstForecast.TotalProfit, comparison.SecondForecast.TotalProfit);
PrintRow("Деньги после месяца 3", comparison.FirstForecast.FinalCash, comparison.SecondForecast.FinalCash);
PrintRow("Клиенты ещё должны", comparison.FirstForecast.FinalReceivables, comparison.SecondForecast.FinalReceivables);
PrintRow("Нужно денег на старте", comparison.FirstForecast.FundingNeed, comparison.SecondForecast.FundingNeed);

Console.WriteLine("\nОшибочный ввод: оплата 120% во втором месяце");
int[] percents = { 50, 120, 50 };
for (int i = 0; i < percents.Length; i++)
{
    try
    {
        _ = new MonthPlan(5, 30_000m, 10_000m, 70_000m, percents[i]);
    }
    catch (ArgumentException error)
    {
        Console.WriteLine($"Месяц {i + 1}, поле {error.ParamName}: {error.Message.Split(" (")[0]}");
    }
}
Console.WriteLine("План с ошибкой не создаётся, поэтому прогноз не строится.");

// ЛР4: сохранение и открытие. Id постоянные, поэтому повторный запуск обновляет планы, а не дублирует
service.Save(later);
service.Save(half);
Console.WriteLine("\nСохранено в findirector.db. Список планов:");
foreach (PlanSummary summary in service.List())
    Console.WriteLine($"  {summary.Name}  ({summary.Id})");

FinancialPlan opened = service.Open(half.Id);
PlanForecast reopened = service.Forecast(opened);
Console.WriteLine($"\nОткрыт план «{opened.Name}»: начальные деньги {opened.OpeningCash:N2}, оплата сразу {opened.Months[0].PaidNowPercent}%");
Console.WriteLine($"Деньги после месяца 3 после открытия: {reopened.FinalCash:N0} — как до сохранения: {service.Forecast(half).FinalCash:N0}");

static FinancialPlan ControlPlan(Guid id, string name, int percent)
{
    var month = new MonthPlan(quantity: 5, price: 30_000m, unitCost: 10_000m,
        fixedCosts: 70_000m, paidNowPercent: percent);
    return new FinancialPlan(id, name, openingCash: 50_000m, new[] { month, month, month });
}

static void PrintForecast(FinancialPlan plan, PlanForecast forecast)
{
    Console.WriteLine($"\nПлан «{plan.Name}», оплата сразу {plan.Months[0].PaidNowPercent}%");
    Console.WriteLine($"{"Месяц",-6}{"Выручка",12}{"Маржа",12}{"Расходы",12}{"Прибыль",12}{"Поступления",13}{"Деньги",12}{"Долг",12}");

    for (int i = 0; i < forecast.Months.Count; i++)
    {
        MonthReport m = forecast.Months[i];
        string note = m.ClosingCash < 0 ? "  не хватает денег" : "";
        Console.WriteLine($"{i + 1,-6}{m.Revenue,12:N0}{m.Margin,12:N0}{m.Expenses,12:N0}{m.Profit,12:N0}" +
            $"{m.CashReceipts,13:N0}{m.ClosingCash,12:N0}{m.ClosingReceivables,12:N0}{note}");
    }

    Console.WriteLine($"Итого прибыль: {forecast.TotalProfit:N0}; деньги C3: {forecast.FinalCash:N0}; " +
        $"долг D3: {forecast.FinalReceivables:N0}; нужно на старте: {forecast.FundingNeed:N0}");
}

static void PrintRow(string title, decimal first, decimal second) =>
    Console.WriteLine($"{title,-30}{first,18:N0}{second,18:N0}");
