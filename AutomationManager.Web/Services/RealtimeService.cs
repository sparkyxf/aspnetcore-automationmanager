using AutomationManager.Contracts;
using AutomationManager.SDK;
using AutomationManager.Web.Services;
using System.Text.Json;

namespace AutomationManager.Web.Services;

public class RealtimeService : IAsyncDisposable
{
    private readonly AutomationWebSocketClient _webSocketClient;
    private readonly ILogger<RealtimeService> _logger;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private volatile bool _isConnected;
    private volatile bool _isRunning;
    private volatile bool _disposed;
    private Guid _connectionId = Guid.NewGuid();
    private int _subscriberCount = 0;

    public RealtimeService(
        AutomationWebSocketClient webSocketClient,
        ILogger<RealtimeService> logger)
    {
        _webSocketClient = webSocketClient;
        _logger = logger;
    }

    public event Action<AgentStatusUpdate>? OnAgentStatusUpdate;
    public event Action<ExecutionStatusUpdate>? OnExecutionStatusUpdate;

    public void AddSubscriber()
    {
        Interlocked.Increment(ref _subscriberCount);
    }

    public void RemoveSubscriber()
    {
        Interlocked.Decrement(ref _subscriberCount);
    }

    public bool HasSubscribers => _subscriberCount > 0;

    public async Task StartAsync()
    {
        if (_disposed) return;
        if (_isRunning) return;

        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_isRunning || _disposed)
                return;

            _isRunning = true;
            
            // Create a fresh CTS for the new connection lifecycle
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            _logger.LogInformation("Starting realtime service...");
            _ = Task.Run(() => ConnectionLoopAsync(_cancellationTokenSource.Token));
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        var retryDelay = 2000;
        const int maxDelay = 15000;

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Generate a new connection ID each time
                _connectionId = Guid.NewGuid();
                
                _logger.LogInformation("Realtime service connecting to API...");
                await _webSocketClient.ConnectAsync(_connectionId, "Web-Dashboard", cancellationToken);
                
                if (!_webSocketClient.IsConnected)
                {
                    _logger.LogWarning("WebSocket connection not established, retrying...");
                    _webSocketClient.DisposeConnection();
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = Math.Min(retryDelay + 1000, maxDelay);
                    continue;
                }

                _isConnected = true;
                retryDelay = 2000; // Reset delay on successful connection
                _logger.LogInformation("Realtime service connected");

                // Listen for messages until connection drops
                await ListenForMessages(cancellationToken);

                // Connection lost
                _isConnected = false;
                _webSocketClient.DisposeConnection();
                
                if (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    _logger.LogWarning("Realtime service connection lost, reconnecting in {Delay}ms...", retryDelay);
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = Math.Min(retryDelay + 1000, maxDelay);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _webSocketClient.DisposeConnection();
                
                if (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    _logger.LogWarning(ex, "Realtime service error, reconnecting in {Delay}ms...", retryDelay);
                    try { await Task.Delay(retryDelay, cancellationToken); } catch (OperationCanceledException) { break; }
                    retryDelay = Math.Min(retryDelay + 1000, maxDelay);
                }
            }
        }

        _isConnected = false;
        _isRunning = false;
        _logger.LogInformation("Realtime service connection loop ended");
    }

    public async Task StopAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (!_isRunning) return;
            
            _logger.LogInformation("Stopping realtime service...");
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            
            if (_isConnected && _webSocketClient.IsConnected)
            {
                try
                {
                    await _webSocketClient.DisconnectAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during graceful WebSocket disconnect");
                }
            }
            
            _webSocketClient.DisposeConnection();
            _isConnected = false;
            _logger.LogInformation("Realtime service stopped");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task ListenForMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            var message = await _webSocketClient.ReceiveRawMessageAsync(cancellationToken);
            
            if (message == null)
            {
                _logger.LogWarning("WebSocket connection closed by server");
                break;
            }
            
            HandleMessage(message);
        }
    }

    private void HandleMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var messageType = document.RootElement.GetProperty("Type").GetString();

            switch (messageType)
            {
                case "AgentStatus":
                    HandleAgentStatusMessage(document.RootElement);
                    break;
                case "AgentDisconnected":
                    HandleAgentDisconnectedMessage(document.RootElement);
                    break;
                case "ExecutionStatus":
                    HandleExecutionStatusMessage(document.RootElement);
                    break;
                default:
                    _logger.LogDebug("Unknown message type: {MessageType}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling realtime message");
        }
    }

    private void HandleAgentStatusMessage(JsonElement element)
    {
        try
        {
            var update = new AgentStatusUpdate
            {
                AgentId = Guid.Parse(element.GetProperty("AgentId").GetString()!),
                Status = element.GetProperty("Status").GetString()!,
                Timestamp = DateTime.UtcNow,
                IsConnected = true,
            };

            if (element.TryGetProperty("AgentName", out var agentName) && agentName.ValueKind == JsonValueKind.String)
                update.AgentName = agentName.GetString();

            if (element.TryGetProperty("CursorX", out var cursorX) && cursorX.ValueKind == JsonValueKind.Number)
                update.CursorX = cursorX.GetSingle();

            if (element.TryGetProperty("CursorY", out var cursorY) && cursorY.ValueKind == JsonValueKind.Number)
                update.CursorY = cursorY.GetSingle();

            if (element.TryGetProperty("CurrentCommand", out var currentCommand) && currentCommand.ValueKind == JsonValueKind.String)
                update.CurrentCommand = currentCommand.GetString();

            if (element.TryGetProperty("PreviousCommands", out var prevCommands) && prevCommands.ValueKind == JsonValueKind.Array)
            {
                update.PreviousCommands = prevCommands.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            if (element.TryGetProperty("NextCommands", out var nextCommands) && nextCommands.ValueKind == JsonValueKind.Array)
            {
                update.NextCommands = nextCommands.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            if (element.TryGetProperty("ScriptExecutionStatus", out var scriptStatus) && scriptStatus.ValueKind == JsonValueKind.String)
                update.ScriptExecutionStatus = scriptStatus.GetString();

            if (element.TryGetProperty("CurrentCommandIndex", out var currentIndex) && currentIndex.ValueKind == JsonValueKind.Number)
                update.CurrentCommandIndex = currentIndex.GetInt32();

            if (element.TryGetProperty("TotalCommands", out var totalCmds) && totalCmds.ValueKind == JsonValueKind.Number)
                update.TotalCommands = totalCmds.GetInt32();

            if (element.TryGetProperty("ErrorMessage", out var errorMsg) && errorMsg.ValueKind == JsonValueKind.String)
                update.ErrorMessage = errorMsg.GetString();

            if (element.TryGetProperty("CurrentLoop", out var currentLoop) && currentLoop.ValueKind == JsonValueKind.Number)
                update.CurrentLoop = currentLoop.GetInt32();

            if (element.TryGetProperty("TotalLoops", out var totalLoops) && totalLoops.ValueKind == JsonValueKind.Number)
                update.TotalLoops = totalLoops.GetInt32();

            if (element.TryGetProperty("ExecutionMode", out var executionMode) && executionMode.ValueKind == JsonValueKind.String)
                update.ExecutionMode = executionMode.GetString();

            OnAgentStatusUpdate?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing agent status message");
        }
    }

    private void HandleAgentDisconnectedMessage(JsonElement element)
    {
        try
        {
            var update = new AgentStatusUpdate
            {
                AgentId = Guid.Parse(element.GetProperty("AgentId").GetString()!),
                Status = "Disconnected",
                Timestamp = DateTime.UtcNow,
                IsConnected = false
            };

            OnAgentStatusUpdate?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing agent disconnected message");
        }
    }

    private void HandleExecutionStatusMessage(JsonElement element)
    {
        try
        {
            var update = new ExecutionStatusUpdate
            {
                ExecutionId = Guid.Parse(element.GetProperty("ExecutionId").GetString()!),
                AgentId = Guid.Parse(element.GetProperty("AgentId").GetString()!),
                Status = element.GetProperty("Status").GetString()!,
                Timestamp = DateTime.UtcNow
            };

            if (element.TryGetProperty("CurrentCommand", out var currentCommand))
                update.CurrentCommand = currentCommand.GetString();

            if (element.TryGetProperty("PreviousCommands", out var prevCommands))
            {
                update.PreviousCommands = prevCommands.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .ToList();
            }

            if (element.TryGetProperty("NextCommands", out var nextCommands))
            {
                update.NextCommands = nextCommands.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .ToList();
            }

            OnExecutionStatusUpdate?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing execution status message");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await StopAsync();
        _connectionSemaphore.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

public class AgentStatusUpdate
{
    public Guid AgentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsConnected { get; set; }
    public float? CursorX { get; set; }
    public float? CursorY { get; set; }
    public string? CurrentCommand { get; set; }
    public List<string> PreviousCommands { get; set; } = new();
    public List<string> NextCommands { get; set; } = new();
    public string? AgentName { get; set; }
    public string? ScriptExecutionStatus { get; set; }
    public int? CurrentCommandIndex { get; set; }
    public int? TotalCommands { get; set; }
    public string? ErrorMessage { get; set; }
    public int? CurrentLoop { get; set; }
    public int? TotalLoops { get; set; }
    public string? ExecutionMode { get; set; }
}

public class ExecutionStatusUpdate
{
    public Guid ExecutionId { get; set; }
    public Guid AgentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? CurrentCommand { get; set; }
    public List<string> PreviousCommands { get; set; } = new();
    public List<string> NextCommands { get; set; } = new();
}