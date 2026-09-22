using System.Globalization;
using System.Text;
using Findirector.Domain;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

Console.WriteLine("Финдиректор: прибыль есть — денег нет?");
Console.WriteLine("Один месяц. На старте 50 000 руб. Продаём 5 сайтов по 30 000 руб.");
Console.WriteLine("Затраты: 10 000 руб. на сайт и 70 000 руб. постоянных расходов.");

var calculator = new MonthCalculator();
foreach (int percent in new[] { 0, 50, 100 })
{
    var plan = new MonthPlan(quantity: 5, price: 30_000m, unitCost: 10_000m,
        fixedCosts: 70_000m, paidNowPercent: percent);
    MonthReport report = calculator.Calculate(plan, openingCash: 50_000m, openingReceivables: 0m);

    Console.WriteLine($"\nОплата в текущем месяце: {percent}%");
    Console.WriteLine($"Выручка:              {report.Revenue,12:N2} руб.");
    Console.WriteLine($"Расходы:              {report.Expenses,12:N2} руб.");
    Console.WriteLine($"Маржинальный доход:   {report.Margin,12:N2} руб.");
    Console.WriteLine($"Прибыль:              {report.Profit,12:N2} руб.");
    Console.WriteLine($"Поступления:          {report.CashReceipts,12:N2} руб.");
    Console.WriteLine($"Прогнозные деньги:    {report.ClosingCash,12:N2} руб.");
    Console.WriteLine($"Клиенты ещё должны:   {report.ClosingReceivables,12:N2} руб.");
    Console.WriteLine($"Не хватает денег:     {report.FundingGap,12:N2} руб.");
}

Console.WriteLine("\nСравните прибыль и деньги: что меняет срок оплаты?");
