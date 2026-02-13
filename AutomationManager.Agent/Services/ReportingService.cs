using AutomationManager.Contracts;
using AutomationManager.SDK;
using AutomationManager.Agent.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace AutomationManager.Agent.Services;

public interface IReportingService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}

public class ReportingService : IReportingService
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly AutomationWebSocketClient _webSocketClient;
    private readonly IScriptExecutionService _executionService;
    private readonly ILogger<ReportingService> _logger;
    private readonly Guid _agentId;
    private readonly string _agentName;
    private bool _isRunning;
    private CancellationTokenSource? _reportingCancellation;
    private DateTime _lastReportTime = DateTime.UtcNow;
    private System.Threading.Timer? _backupReportTimer;
    
    // State tracking for change detection
    private string _lastReportedCurrentCommand = string.Empty;
    private List<string> _lastReportedPreviousCommands = new();
    private List<string> _lastReportedNextCommands = new();

    public ReportingService(
        AutomationWebSocketClient webSocketClient,
        IScriptExecutionService executionService,
        ILogger<ReportingService> logger,
        Guid agentId,
        string agentName)
    {
        _webSocketClient = webSocketClient;
        _executionService = executionService;
        _logger = logger;
        _agentId = agentId;
        _agentName = agentName;

        // Subscribe to execution status changes
        _executionService.ExecutionStatusChanged += OnExecutionStatusChanged;
        _executionService.OnCommandExecutionReport += OnCommandExecutionReport;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _reportingCancellation = new CancellationTokenSource();

        // Start backup timer to ensure at least one report per second
        _backupReportTimer = new System.Threading.Timer(BackupReportCallback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        
        _logger.LogInformation("Reporting service started for Agent {AgentId}", _agentId);
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _reportingCancellation?.Cancel();
        _backupReportTimer?.Dispose();
        _backupReportTimer = null;
        _logger.LogInformation("Reporting service stopped for Agent {AgentId}", _agentId);
    }

    private void OnExecutionStatusChanged(object? sender, ExecutionStatusChangedEventArgs e)
    {
        // Send status update when execution status changes
        _ = Task.Run(() => SendStatusUpdateIfChangedAsync());
    }

    private void OnCommandExecutionReport(object? sender, CommandExecutionReportEventArgs e)
    {
        // Check if command state changed before execution
        _ = Task.Run(() => SendStatusAsync(e.PreviousCommands, e.CurrentCommand, e.NextCommands));
    }

    private void BackupReportCallback(object? state)
    {
        if (!_isRunning) return;

        var timeSinceLastReport = DateTime.UtcNow - _lastReportTime;
        if (timeSinceLastReport >= TimeSpan.FromSeconds(1))
        {
            // Send backup report to meet minimum 1 second interval (force send even without changes)
            _ = Task.Run(async () => await SendStatusUpdateAsync(forceReport: true));
        }
    }



    private async Task SendStatusUpdateIfChangedAsync()
    {
        await SendStatusUpdateAsync(forceReport: true);
    }

    private async Task SendStatusAsync(List<string>? previousCommands = null, string? currentCommand = null, List<string>? nextCommands = null)
    {
        await SendStatusUpdateAsync(previousCommands, currentCommand, nextCommands, forceReport: false);
    }

    private bool HasCommandStateChanged(string currentCommand, List<string> previousCommands)
    {
        // Check if current command changed
        if (_lastReportedCurrentCommand != currentCommand)
            return true;

        // Check if previous commands changed
        if (_lastReportedPreviousCommands.Count != previousCommands.Count)
            return true;

        for (int i = 0; i < previousCommands.Count; i++)
        {
            if (i >= _lastReportedPreviousCommands.Count || _lastReportedPreviousCommands[i] != previousCommands[i])
                return true;
        }

        return false;
    }

    private async Task SendStatusUpdateAsync(List<string>? previousCommands = null, string? currentCommand = null, List<string>? nextCommands = null, bool forceReport = false)
    {
        if (!_webSocketClient.IsConnected || !_isRunning)
            return;

        try
        {
            // Get execution state from execution service
            var executionStatus = _executionService.Status.ToString();
            var currentCommandIndex = _executionService.CurrentCommandIndex;
            var totalCommands = _executionService.TotalCommands;
            var errorMessage = _executionService.ErrorMessage;

            // Update tracking state
            if (!forceReport)
            {
                _lastReportedPreviousCommands = previousCommands?.ToList() ?? new List<string>();
                _lastReportedCurrentCommand = currentCommand ?? string.Empty;
                _lastReportedNextCommands = nextCommands?.ToList() ?? new List<string>();
            }
            _lastReportTime = DateTime.UtcNow;
            
            var cursorPos = GetCursorPosition();

            // Determine agent status based on execution status
            var agentStatus = _executionService.Status switch
            {
                ScriptExecutionStatus.Running => "Active",
                ScriptExecutionStatus.Paused => "Paused", 
                ScriptExecutionStatus.Error => "Error",
                _ => "Idle"
            };

            var message = new AgentStatusMessage(
                _agentId,
                agentStatus,
                forceReport ? _lastReportedPreviousCommands : previousCommands,
                forceReport ? _lastReportedCurrentCommand : currentCommand,
                forceReport ? _lastReportedNextCommands : nextCommands,
                cursorPos.X,
                cursorPos.Y,
                executionStatus,
                currentCommandIndex,
                totalCommands,
                errorMessage,
                _executionService.CurrentLoop,
                _executionService.TotalLoops,
                _executionService.ExecutionMode.ToString()
            );

            await _webSocketClient.SendStatusAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status update");
            throw;
        }
    }

    private (int X, int Y) GetCursorPosition()
    {
        try
        {
            if (GetCursorPos(out POINT point))
            {
                return (point.X, point.Y);
            }
        }
        catch
        {
            // Ignore errors
        }
        return (0, 0);
    }

    public void Dispose()
    {
        _executionService.ExecutionStatusChanged -= OnExecutionStatusChanged;
        _executionService.OnCommandExecutionReport -= OnCommandExecutionReport;
        _backupReportTimer?.Dispose();
        _reportingCancellation?.Cancel();
        _reportingCancellation?.Dispose();
    }
}