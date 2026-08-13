using DruOpc.Components;
using DruOpc.Services;

// Anchor the content root to the app directory so static assets resolve
// no matter which directory the binary is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One OPC UA client application configuration for the whole app.
builder.Services.AddSingleton<UaApplicationProvider>();

// Per-circuit (per browser tab) OPC UA state.
builder.Services.AddScoped<UaConnection>();
builder.Services.AddScoped<UaBrowserService>();
builder.Services.AddScoped<UaWatchList>();
builder.Services.AddScoped<UaEventLog>();
builder.Services.AddScoped<UaAlarmList>();
builder.Services.AddScoped<UaHistoryService>();
builder.Services.AddScoped<BrowserState>();

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
