using OctopusDashboard.Components;
using OctopusDashboard.Models;
using OctopusDashboard.Services;
using OctopusDashboard.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<OctopusSettings>(builder.Configuration.GetSection("Octopus"));
builder.Services.AddScoped<AppState>();
builder.Services.AddHttpClient<IOctopusService, OctopusService>(client =>
{
    client.BaseAddress = new Uri("https://api.octopus.energy/v1/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapOctopusEndpoints();

app.Run();
