using AutomationManager.Web.Services;
using System.Collections.Concurrent;

namespace AutomationManager.Web.Services;

public class AgentTrackerService : IDisposable
{
    private readonly RealtimeService _realtimeService;
    private readonly ILogger<AgentTrackerService> _logger;
    private readonly ConcurrentDictionary<Guid, TrackedAgent> _trackedAgents = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _disconnectTimeout = TimeSpan.FromSeconds(5);

    public AgentTrackerService(RealtimeService realtimeService, ILogger<AgentTrackerService> logger)
    {
        _realtimeService = realtimeService;
        _logger = logger;

        // Subscribe to real-time updates
        _realtimeService.OnAgentStatusUpdate += HandleAgentStatusUpdate;
        
        // Start cleanup timer that runs every second
        _cleanupTimer = new Timer(CleanupDisconnectedAgents, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        
        // Auto-start the real-time service
        _ = StartRealtimeServiceAsync();
    }

    private async Task StartRealtimeServiceAsync()
    {
        try
        {
            await _realtimeService.StartAsync();
            _logger.LogInformation("Real-time service started for agent tracking");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start real-time service for agent tracking");
        }
    }

    public event Action? OnAgentsChanged;

    private void HandleAgentStatusUpdate(AgentStatusUpdate update)
    {
        // If we receive a disconnect message, remove the agent immediately
        if (!update.IsConnected)
        {
            if (_trackedAgents.TryRemove(update.AgentId, out _))
            {
                _logger.LogInformation("Agent {AgentId} disconnected, removed from tracker", update.AgentId);
                OnAgentsChanged?.Invoke();
            }
            return;
        }

        var agent = _trackedAgents.GetOrAdd(update.AgentId, id => new TrackedAgent
        {
            AgentId = id,
            AgentName = update.AgentName ?? $"Agent-{id:N}"[..20],
            IsConnected = true,
            Status = "Idle"
        });

        // Update agent information
        agent.LastMessageTime = DateTime.UtcNow;
        agent.IsConnected = true;
        agent.Status = update.Status;
        agent.CursorX = update.CursorX;
        agent.CursorY = update.CursorY;
        agent.CurrentCommand = update.CurrentCommand ?? string.Empty;
        agent.PreviousCommands = update.PreviousCommands ?? new List<string>();
        agent.NextCommands = update.NextCommands ?? new List<string>();
        agent.NextCommands.Reverse();
        agent.ScriptExecutionStatus = update.ScriptExecutionStatus;
        agent.CurrentCommandIndex = update.CurrentCommandIndex;
        agent.TotalCommands = update.TotalCommands;
        agent.ErrorMessage = update.ErrorMessage;
        agent.CurrentLoop = update.CurrentLoop;
        agent.TotalLoops = update.TotalLoops;
        agent.ExecutionMode = update.ExecutionMode;
        
        if (!string.IsNullOrEmpty(update.AgentName))
        {
            agent.AgentName = update.AgentName;
        }

        _logger.LogDebug("Updated agent {AgentId} ({AgentName}) status: {Status}", 
            update.AgentId, agent.AgentName, update.Status);
        
        OnAgentsChanged?.Invoke();
    }

    private void CleanupDisconnectedAgents(object? state)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var kvp in _trackedAgents)
        {
            var timeSinceLastMessage = now - kvp.Value.LastMessageTime;
            
            // If no message for 5 seconds, mark as disconnected and remove immediately
            if (timeSinceLastMessage > _disconnectTimeout && kvp.Value.IsConnected)
            {
                _logger.LogWarning("Agent {AgentId} ({AgentName}) marked as disconnected - no messages for {Duration}s. Removing data.", 
                    kvp.Key, kvp.Value.AgentName, timeSinceLastMessage.TotalSeconds);
                _trackedAgents.TryRemove(kvp.Key, out _);
                changed = true;
            }
        }

        if (changed)
        {
            OnAgentsChanged?.Invoke();
        }
    }

    public IEnumerable<TrackedAgent> GetAllAgents()
    {
        return _trackedAgents.Values.OrderBy(a => a.AgentName).ToList();
    }

    public IEnumerable<TrackedAgent> GetConnectedAgents()
    {
        return _trackedAgents.Values
            .Where(a => a.IsConnected)
            .OrderBy(a => a.AgentName)
            .ToList();
    }

    public IEnumerable<TrackedAgent> GetIdleAgents()
    {
        return _trackedAgents.Values
            .Where(a => a.IsConnected && a.Status == "Idle")
            .OrderBy(a => a.AgentName)
            .ToList();
    }

    public TrackedAgent? GetAgent(Guid agentId)
    {
        _trackedAgents.TryGetValue(agentId, out var agent);
        return agent;
    }

    public void Dispose()
    {
        _realtimeService.OnAgentStatusUpdate -= HandleAgentStatusUpdate;
        _cleanupTimer?.Dispose();
        
        try
        {
            _realtimeService.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log but don't throw during disposal
            _logger.LogWarning(ex, "Error stopping realtime service during disposal");
        }
    }
}

public class TrackedAgent
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string Status { get; set; } = "Idle"; // Idle, Active, Running, Paused
    public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    public float? CursorX { get; set; }
    public float? CursorY { get; set; }
    public string CurrentCommand { get; set; } = string.Empty;
    public List<string> PreviousCommands { get; set; } = new();
    public List<string> NextCommands { get; set; } = new();
    public string? ScriptExecutionStatus { get; set; }
    public int? CurrentCommandIndex { get; set; }
    public int? TotalCommands { get; set; }
    public string? ErrorMessage { get; set; }
    public int? CurrentLoop { get; set; }
    public int? TotalLoops { get; set; }
    public string? ExecutionMode { get; set; }
}
