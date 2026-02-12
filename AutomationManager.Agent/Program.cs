using AutomationManager.Agent;
using AutomationManager.SDK;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutomationManager Agent";
});

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<AutomationWebSocketClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:5200";
    // Convert HTTP to WebSocket protocol
    var wsUrl = apiBaseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
    return new AutomationWebSocketClient($"{wsUrl}/ws/agent");
});
builder.Services.AddSingleton<AgentService>();

var host = builder.Build();
host.Run();
