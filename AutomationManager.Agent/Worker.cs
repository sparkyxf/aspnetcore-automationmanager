using AutomationManager.SDK;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AutomationManager.Agent;

namespace AutomationManager.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentService _agentService;

    public Worker(ILogger<Worker> logger, AgentService agentService)
    {
        _logger = logger;
        _agentService = agentService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent starting");

        await _agentService.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        await _agentService.StopAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _agentService.Dispose();
        base.Dispose();
    }
}
