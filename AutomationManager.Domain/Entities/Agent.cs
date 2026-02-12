namespace AutomationManager.Domain.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ConnectionStatus Status { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    // Navigation
    public ICollection<ScriptExecution> Executions { get; set; } = new List<ScriptExecution>();
}

public enum ConnectionStatus
{
    Disconnected,
    Connected,
    Busy
}