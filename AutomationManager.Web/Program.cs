using AutomationManager.Web.Components;
using AutomationManager.Web.Services;
using AutomationManager.SDK;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Persist Data Protection keys so antiforgery tokens survive container restarts
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("AutomationManager.Web");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); // Serialize enums as strings
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5200";

builder.Services.AddHttpClient<AutomationApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddSingleton<AutomationWebSocketClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<AutomationWebSocketClient>>();
    var wsUrl = apiBaseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
    return new AutomationWebSocketClient($"{wsUrl}/ws/agent", msg => logger.LogDebug(msg));
});

// Add custom services
builder.Services.AddScoped<AgentStatusService>();
builder.Services.AddScoped<ScriptExecutionService>();
builder.Services.AddSingleton<RealtimeService>();
builder.Services.AddSingleton<AgentTrackerService>();

var app = builder.Build();

// Ensure services are properly disposed on shutdown with enhanced handshake
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application stopping - performing graceful shutdown...");
    
    try
    {
        // Stop real-time service with proper handshake
        var realtimeService = app.Services.GetService<RealtimeService>();
        if (realtimeService != null)
        {
            var stopTask = realtimeService.StopAsync();
            if (!stopTask.Wait(TimeSpan.FromSeconds(15))) // Give more time for graceful shutdown
            {
                logger.LogWarning("Realtime service stop operation timed out");
            }
            else
            {
                logger.LogInformation("Realtime service stopped gracefully");
            }
        }
        
        // Dispose agent tracker
        var agentTracker = app.Services.GetService<AgentTrackerService>();
        if (agentTracker != null)
        {
            agentTracker.Dispose();
            logger.LogInformation("Agent tracker disposed");
        }
        
        logger.LogInformation("Graceful shutdown completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during graceful shutdown");
    }
});

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
