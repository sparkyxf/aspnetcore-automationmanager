using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AutomationManager.Contracts;
using AutomationManager.SDK;
using AutomationManager.Agent.Services;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace AutomationManager.Agent;

public class AgentService : BackgroundService
{
    private readonly ILogger<AgentService> _logger;
    private readonly AutomationWebSocketClient _webSocketClient;
    private readonly IScriptExecutionService _executionService;
    private readonly IReportingService _reportingService;
    private readonly Guid _agentId;
    private readonly string _agentName;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    public AgentService(
        ILogger<AgentService> logger,
        AutomationWebSocketClient webSocketClient,
        IScriptExecutionService executionService,
        IReportingService reportingService)
    {
        _logger = logger;
        _webSocketClient = webSocketClient;
        _executionService = executionService;
        _reportingService = reportingService;
        _agentId = Guid.NewGuid();
        _agentName = Environment.MachineName;
        
        _logger.LogInformation("Created Agent {AgentId} ({AgentName})", _agentId, _agentName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent {AgentId} starting...", _agentId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Connect with infinite retry
                await ConnectWithRetryAsync(stoppingToken);

                if (!_webSocketClient.IsConnected)
                    continue;

                await _reportingService.StartAsync(stoppingToken);
                _logger.LogInformation("Agent {AgentId} connected and running", _agentId);
                
                // Message receive loop - runs until connection is lost
                while (!stoppingToken.IsCancellationRequested && _webSocketClient.IsConnected)
                {
                    try
                    {
                        var message = await _webSocketClient.ReceiveRawMessageAsync(stoppingToken);
                        if (message != null)
                        {
                            await HandleIncomingMessageAsync(message);
                        }
                        else
                        {
                            // null means connection was closed
                            _logger.LogWarning("Agent {AgentId} received null message, connection lost", _agentId);
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Agent {AgentId} error receiving message, connection lost", _agentId);
                        break;
                    }
                }

                // Connection lost - dispose and prepare for reconnection
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Agent {AgentId} connection lost, preparing to reconnect...", _agentId);
                    await _reportingService.StopAsync();
                    _webSocketClient.DisposeConnection();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent {AgentId} stop requested", _agentId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent {AgentId} encountered an error", _agentId);
                _webSocketClient.DisposeConnection();
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        var retryDelay = 2000;
        const int maxDelay = 15000;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Agent {AgentId} attempting to connect to API...", _agentId);
                await _webSocketClient.ConnectAsync(_agentId, _agentName, stoppingToken);
                
                if (_webSocketClient.IsConnected)
                {
                    _logger.LogInformation("Agent {AgentId} connected successfully", _agentId);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Agent {AgentId} connection attempt failed: {Message}. Retrying in {Delay}ms...", 
                    _agentId, ex.Message, retryDelay);
                _webSocketClient.DisposeConnection();
            }

            await Task.Delay(retryDelay, stoppingToken);
            retryDelay = Math.Min(retryDelay + 1000, maxDelay);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Agent {AgentId}...", _agentId);
        
        try
        {
            await _reportingService.StopAsync();
            _executionService.StopExecution();
            await _webSocketClient.DisconnectAsync();
            
            _logger.LogInformation("Agent {AgentId} stopped successfully", _agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Agent {AgentId}", _agentId);
        }
        
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleIncomingMessageAsync(string messageJson)
    {
        try
        {
            var controlMessage = JsonSerializer.Deserialize<ExecutionControlMessage>(messageJson);
            if (controlMessage == null)
            {
                _logger.LogWarning("Received null execution control message");
                return;
            }

            _logger.LogInformation("Received execution control command: {SetMode}, Mode={Mode}, LoopCount={LoopCount}", 
                controlMessage.SetMode, controlMessage.Mode, controlMessage.LoopCount);

            switch (controlMessage.SetMode.ToLower())
            {
                case "run":
                    if (string.IsNullOrEmpty(controlMessage.ScriptText))
                    {
                        _logger.LogError("Cannot start execution: Script text is empty");
                        return;
                    }
                    await _executionService.RunScriptAsync(
                        controlMessage.ScriptText, 
                        controlMessage.Mode ?? ExecutionMode.RunOnce,
                        controlMessage.LoopCount);
                    break;
                    
                case "pause":
                    _executionService.PauseExecution();
                    break;
                    
                case "resume":
                    _executionService.ResumeExecution();
                    break;
                    
                case "stop":
                    _executionService.StopExecution();
                    break;
                    
                default:
                    _logger.LogWarning("Unknown execution control command: {SetMode}", controlMessage.SetMode);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming message: {Message}", messageJson);
        }
    }

    public override void Dispose()
    {
        _webSocketClient.Dispose();
        base.Dispose();
    }
}