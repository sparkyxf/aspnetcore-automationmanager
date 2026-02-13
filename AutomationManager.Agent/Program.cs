using AutomationManager.Agent;
using AutomationManager.Agent.Services;
using AutomationManager.SDK;
using AutomationManager.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Ensure console logging is enabled
builder.Logging.AddConsole();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutomationManager Agent";
});

// System tray is Windows-specific, only add on Windows
if (OperatingSystem.IsWindows())
{
#pragma warning disable CA1416 // Validate platform compatibility
    builder.Services.AddHostedService<SystemTrayService>();
#pragma warning restore CA1416 // Validate platform compatibility
}
builder.Services.AddHostedService<Worker>();

// Register WebSocket client
builder.Services.AddSingleton<AutomationWebSocketClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<AutomationWebSocketClient>>();
    var apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:5200";
    // Convert HTTP to WebSocket protocol
    var wsUrl = apiBaseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
    return new AutomationWebSocketClient($"{wsUrl}/ws/agent", msg => logger.LogDebug(msg));
});

// Register domain services
builder.Services.AddSingleton<ScriptParser>();
builder.Services.AddSingleton<ScriptValidator>();

// Register agent services
builder.Services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();

// Register reporting service with dependencies from AgentService
builder.Services.AddSingleton<IReportingService>(provider =>
{
    var webSocketClient = provider.GetRequiredService<AutomationWebSocketClient>();
    var executionService = provider.GetRequiredService<IScriptExecutionService>();
    var logger = provider.GetRequiredService<ILogger<ReportingService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    
    // Get agent ID and name from config for reporting service
    var agentIdString = configuration["AgentId"];
    var agentId = string.IsNullOrEmpty(agentIdString) ? Guid.NewGuid() : Guid.Parse(agentIdString);
    var agentName = configuration["AgentName"];
    if (string.IsNullOrWhiteSpace(agentName))
        agentName = Environment.MachineName;
    
    return new ReportingService(webSocketClient, executionService, logger, agentId, agentName);
});

builder.Services.AddSingleton<AgentService>();

var host = builder.Build();
host.Run();
