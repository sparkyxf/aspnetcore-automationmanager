using AutomationManager.Contracts;
using AutomationManager.SDK;

namespace AutomationManager.Web.Services;

public class AgentStatusService
{
    private readonly AutomationApiClient _apiClient;
    private readonly ILogger<AgentStatusService> _logger;
    private readonly Dictionary<Guid, AgentStatus> _agentStatuses = new();

    public AgentStatusService(AutomationApiClient apiClient, ILogger<AgentStatusService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public event Action? OnStatusChanged;

    public void HandleRealtimeUpdate(AgentStatusUpdate update)
    {
        if (_agentStatuses.TryGetValue(update.AgentId, out var status))
        {
            status.CursorX = update.CursorX;
            status.CursorY = update.CursorY;
            status.IsConnected = update.IsConnected;
            status.LastSeen = update.Timestamp;
            status.ConnectionStatus = update.IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            status.CurrentCommand = update.CurrentCommand;
            status.PreviousCommands = update.PreviousCommands ?? new List<string>();
            status.NextCommands = update.NextCommands ?? new List<string>();
            
            OnStatusChanged?.Invoke();
        }
    }

    public async Task<IEnumerable<AgentStatus>> GetAgentStatusesAsync()
    {
        try
        {
            var agents = await _apiClient.GetAgentsAsync();
            var executions = await _apiClient.GetExecutionsAsync();

            var agentStatuses = agents.Select(agent =>
            {
                var currentExecution = executions.FirstOrDefault(e => 
                    e.TemplateId == agent.Id && e.Status == "Running");

                return new AgentStatus
                {
                    AgentId = agent.Id,
                    AgentName = agent.Name,
                    IsConnected = agent.Status == "Connected",
                    LastSeen = agent.LastSeen.DateTime,
                    CurrentExecution = currentExecution,
                    ConnectionStatus = agent.Status switch
                    {
                        "Connected" => ConnectionStatus.Connected,
                        "Disconnected" => ConnectionStatus.Disconnected,
                        _ => ConnectionStatus.Unknown
                    }
                };
            });

            // Update internal status cache
            foreach (var status in agentStatuses)
            {
                _agentStatuses[status.AgentId] = status;
            }

            return agentStatuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent statuses");
            return Enumerable.Empty<AgentStatus>();
        }
    }

    public async Task<IEnumerable<AgentResponse>> GetIdleAgentsAsync()
    {
        try
        {
            var agents = await _apiClient.GetAgentsAsync();
            var executions = await _apiClient.GetExecutionsAsync();

            var busyAgentIds = executions
                .Where(e => e.Status == "Running")
                .Select(e => e.TemplateId) // Note: Need to fix this logic as ScriptExecutionResponse might not have AgentId
                .ToHashSet();

            return agents.Where(a => a.Status == "Connected" && !busyAgentIds.Contains(a.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get idle agents");
            return Enumerable.Empty<AgentResponse>();
        }
    }

    public void UpdateAgentStatus(Guid agentId, AgentStatus status)
    {
        _agentStatuses[agentId] = status;
        OnStatusChanged?.Invoke();
    }

    public AgentStatus? GetAgentStatus(Guid agentId)
    {
        return _agentStatuses.TryGetValue(agentId, out var status) ? status : null;
    }
}

public class AgentStatus
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime LastSeen { get; set; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public string Status => ConnectionStatus.ToString();
    public ScriptExecutionResponse? CurrentExecution { get; set; }
    public List<string> PreviousCommands { get; set; } = new();
    public string? CurrentCommand { get; set; }
    public List<string> NextCommands { get; set; } = new();
    public float? CursorX { get; set; }
    public float? CursorY { get; set; }
}

public enum ConnectionStatus
{
    Unknown,
    Connected,
    Disconnected,
    Reconnecting
}