using System.Globalization;
using Findirector.Application;
using Findirector.Infrastructure;
using Findirector.Web.Components;

// Числа на экране — по-русски: 50 000,50
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Точка сборки зависимостей (DIP): UI получает PlanService, а хранилище подставляется здесь
string databasePath = Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration["Database"] ?? "findirector.db");
builder.Services.AddSingleton<IPlanRepository>(new SqlitePlanRepository(databasePath));
builder.Services.AddSingleton<PlanService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
