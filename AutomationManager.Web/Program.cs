using AutomationManager.Web.Components;
using AutomationManager.SDK;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<AutomationApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5200"); // API URL
});

builder.Services.AddScoped<AutomationWebSocketClient>(provider =>
{
    return new AutomationWebSocketClient("ws://localhost:5200/ws/agent");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
