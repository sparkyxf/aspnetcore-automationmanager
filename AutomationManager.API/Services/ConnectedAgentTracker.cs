using AutomationManager.Contracts;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AutomationManager.API.Services;

public class ConnectedAgentTracker
{
    private readonly ConcurrentDictionary<Guid, ConnectedAgentInfo> _connectedAgents = new();
    private readonly ConcurrentDictionary<int, WebSocket> _webClients = new();
    private int _webClientIdCounter = 0;
    private readonly ILogger<ConnectedAgentTracker> _logger;

    public ConnectedAgentTracker(ILogger<ConnectedAgentTracker> logger)
    {
        _logger = logger;
    }

    public void RegisterWebClient(WebSocket webSocket)
    {
        var id = Interlocked.Increment(ref _webClientIdCounter);
        _webClients[id] = webSocket;
        _logger.LogInformation("Web client {ClientId} connected, total web clients: {Count}", id, _webClients.Count);
    }

    public void UnregisterWebClient(WebSocket webSocket)
    {
        var keysToRemove = _webClients.Where(kvp => kvp.Value == webSocket).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
        {
            _webClients.TryRemove(key, out _);
        }
        _logger.LogInformation("Web client disconnected, remaining web clients: {Count}", _webClients.Count);
    }

    public void RegisterAgent(Guid agentId, string agentName, WebSocket webSocket)
    {
        // If an agent with the same ID already exists (e.g. client restarted), dispose old connection data
        if (_connectedAgents.TryRemove(agentId, out var oldAgent))
        {
            _logger.LogInformation("Agent {AgentId} ({AgentName}) re-registering, disposing old connection", agentId, oldAgent.AgentName);
            // Old WebSocket will be cleaned up by the old HandleWebSocketAsync finally block
        }

        var agentInfo = new ConnectedAgentInfo
        {
            AgentId = agentId,
            AgentName = agentName,
            WebSocket = webSocket,
            ConnectedAt = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            Status = "Connected",
            CursorX = null,
            CursorY = null
        };

        _connectedAgents[agentId] = agentInfo;
        _logger.LogInformation("Agent {AgentId} ({AgentName}) connected", agentId, agentName);
    }

    public void UpdateAgentStatus(Guid agentId, AgentStatusMessage statusMessage)
    {
        if (_connectedAgents.TryGetValue(agentId, out var agentInfo))
        {
            agentInfo.LastSeen = DateTimeOffset.UtcNow;
            agentInfo.Status = statusMessage.Status;
            agentInfo.CursorX = statusMessage.CursorX;
            agentInfo.CursorY = statusMessage.CursorY;
            agentInfo.PreviousCommands = statusMessage.PreviousCommands;
            agentInfo.CurrentCommand = statusMessage.CurrentCommand;
            agentInfo.NextCommands = statusMessage.NextCommands;
            
            _ = BroadcastAgentStatusAsync(agentId, statusMessage);
        }
    }

    private async Task BroadcastAgentStatusAsync(Guid agentId, AgentStatusMessage statusMessage)
    {
        var agentInfo = _connectedAgents.GetValueOrDefault(agentId);
        
        var message = new
        {
            Type = "AgentStatus",
            AgentId = agentId,
            AgentName = agentInfo?.AgentName,
            Status = statusMessage.Status,
            CursorX = statusMessage.CursorX,
            CursorY = statusMessage.CursorY,
            PreviousCommands = statusMessage.PreviousCommands,
            CurrentCommand = statusMessage.CurrentCommand,
            NextCommands = statusMessage.NextCommands,
            Timestamp = DateTimeOffset.UtcNow
        };

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        var segment = new ArraySegment<byte>(messageBytes);

        _logger.LogDebug("[WS-OUT] Broadcasting AgentStatus to {Count} web clients", _webClients.Count);

        var disconnectedClientKeys = new List<int>();
        
        foreach (var kvp in _webClients)
        {
            if (kvp.Value.State == WebSocketState.Open)
            {
                try
                {
                    await kvp.Value.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send message to web client {ClientId}", kvp.Key);
                    disconnectedClientKeys.Add(kvp.Key);
                }
            }
            else
            {
                disconnectedClientKeys.Add(kvp.Key);
            }
        }

        // Clean up disconnected web clients
        foreach (var key in disconnectedClientKeys)
        {
            _webClients.TryRemove(key, out _);
        }
    }

    public void UnregisterAgent(Guid agentId)
    {
        if (_connectedAgents.TryRemove(agentId, out var agentInfo))
        {
            _logger.LogInformation("Agent {AgentId} ({AgentName}) disconnected after {Duration}",
                agentId, agentInfo.AgentName, DateTimeOffset.UtcNow - agentInfo.ConnectedAt);
            
            _ = BroadcastAgentDisconnectedAsync(agentId);
        }
    }

    private async Task BroadcastAgentDisconnectedAsync(Guid agentId)
    {
        var message = new
        {
            Type = "AgentDisconnected",
            AgentId = agentId,
            Timestamp = DateTimeOffset.UtcNow
        };

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        var segment = new ArraySegment<byte>(messageBytes);

        _logger.LogDebug("[WS-OUT] Broadcasting AgentDisconnected to {Count} web clients: {Message}", _webClients.Count, messageJson);

        var disconnectedClientKeys = new List<int>();

        foreach (var kvp in _webClients)
        {
            if (kvp.Value.State == WebSocketState.Open)
            {
                try
                {
                    await kvp.Value.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send disconnection message to web client {ClientId}", kvp.Key);
                    disconnectedClientKeys.Add(kvp.Key);
                }
            }
            else
            {
                disconnectedClientKeys.Add(kvp.Key);
            }
        }

        foreach (var key in disconnectedClientKeys)
        {
            _webClients.TryRemove(key, out _);
        }
    }

    public IEnumerable<ConnectedAgentInfo> GetConnectedAgents()
    {
        return _connectedAgents.Values.ToList();
    }

    public ConnectedAgentInfo? GetAgent(Guid agentId)
    {
        _connectedAgents.TryGetValue(agentId, out var agentInfo);
        return agentInfo;
    }

    public int GetConnectedAgentCount()
    {
        return _connectedAgents.Count;
    }

    public async Task CloseAllConnectionsAsync()
    {
        _logger.LogInformation("Closing all WebSocket connections gracefully...");

        var closeAgentTasks = _connectedAgents.Values.Select(async agentInfo =>
        {
            if (agentInfo.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    _logger.LogInformation("Sending close handshake to Agent {AgentId} ({AgentName})", agentInfo.AgentId, agentInfo.AgentName);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await agentInfo.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send close handshake to Agent {AgentId}", agentInfo.AgentId);
                }
            }
        });

        var closeWebClientTasks = _webClients.Values.Select(async client =>
        {
            if (client.State == WebSocketState.Open)
            {
                try
                {
                    _logger.LogDebug("Sending close handshake to web client");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send close handshake to web client");
                }
            }
        });

        await Task.WhenAll(closeAgentTasks.Concat(closeWebClientTasks));
        
        _connectedAgents.Clear();
        _webClients.Clear();
        
        _logger.LogInformation("All WebSocket connections closed");
    }

    public async Task<bool> SendExecutionControlToAgentAsync(Guid agentId, ExecutionControlMessage executionControl)
    {
        if (!_connectedAgents.TryGetValue(agentId, out var agentInfo))
        {
            _logger.LogWarning("Agent {AgentId} not found or not connected", agentId);
            return false;
        }

        if (agentInfo.WebSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Agent {AgentId} WebSocket is not open, removing stale entry", agentId);
            _connectedAgents.TryRemove(agentId, out _);
            return false;
        }

        try
        {
            var messageJson = JsonSerializer.Serialize(executionControl);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var segment = new ArraySegment<byte>(messageBytes);

            await agentInfo.WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            _logger.LogInformation("Sent {SetMode} execution control to Agent {AgentId}", executionControl.SetMode, agentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send execution control to Agent {AgentId}", agentId);
            return false;
        }
    }

    public async Task<bool> SendExecutionCommandToAgentAsync(Guid agentId, string action, Guid? executionId = null, Guid? templateId = null, string? scriptText = null)
    {
        if (!_connectedAgents.TryGetValue(agentId, out var agentInfo))
        {
            _logger.LogWarning("Agent {AgentId} not found or not connected", agentId);
            return false;
        }

        if (agentInfo.WebSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Agent {AgentId} WebSocket is not open, removing stale entry", agentId);
            _connectedAgents.TryRemove(agentId, out _);
            return false;
        }

        try
        {
            var message = new
            {
                Type = "ExecutionControl",
                Action = action,
                ExecutionId = executionId,
                TemplateId = templateId,
                ScriptText = scriptText
            };

            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var segment = new ArraySegment<byte>(messageBytes);

            await agentInfo.WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            _logger.LogInformation("Sent {Action} command to Agent {AgentId}", action, agentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send execution command to Agent {AgentId}", agentId);
            return false;
        }
    }
}

public class ConnectedAgentInfo
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public WebSocket WebSocket { get; set; } = null!;
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public string Status { get; set; } = string.Empty;
    public float? CursorX { get; set; }
    public float? CursorY { get; set; }
    public List<string>? PreviousCommands { get; set; }
    public string? CurrentCommand { get; set; }
    public List<string>? NextCommands { get; set; }
}
