using AutomationManager.Agent;
using AutomationManager.SDK;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutomationManager Agent";
});

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<AutomationWebSocketClient>();
builder.Services.AddSingleton<AgentService>();

var host = builder.Build();
host.Run();
