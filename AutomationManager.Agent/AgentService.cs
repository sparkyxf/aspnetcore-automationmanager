using AutomationManager.SDK;
using AutomationManager.Contracts;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;

namespace AutomationManager.Agent;

public class AgentService : IDisposable
{
    private readonly AutomationWebSocketClient _webSocketClient;
    private readonly ILogger<AgentService> _logger;
    private readonly IConfiguration _configuration;
    private Guid _agentId = Guid.NewGuid(); // In real app, load from config

    public AgentService(ILogger<AgentService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var url = _configuration["ApiBaseUrl"] + "/ws/agent";
        _webSocketClient = new AutomationWebSocketClient(url);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _webSocketClient.ConnectAsync(_agentId, cancellationToken);

        // Start sending status updates
        _ = Task.Run(() => SendStatusUpdatesAsync(cancellationToken), cancellationToken);
    }

    public async Task StopAsync()
    {
        await _webSocketClient.DisconnectAsync();
    }

    public void Dispose()
    {
        _webSocketClient.Dispose();
    }

    private async Task SendStatusUpdatesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var cursorPos = GetCursorPosition();
            var message = new AgentStatusMessage(
                _agentId,
                "Connected",
                null, // Previous
                null, // Current
                null, // Next
                cursorPos.X,
                cursorPos.Y
            );

            await _webSocketClient.SendStatusAsync(message, cancellationToken);
            await Task.Delay(1000, cancellationToken);
        }
    }

    private (int X, int Y) GetCursorPosition()
    {
        // Use Windows API to get cursor position
        // For demo, return dummy
        return (500, 300);
    }
}