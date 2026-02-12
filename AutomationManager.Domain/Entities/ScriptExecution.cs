namespace AutomationManager.Domain.Entities;

public class ScriptExecution
{
    public Guid Id { get; set; }

    // Foreign keys
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public Guid ScriptTemplateId { get; set; }
    public ScriptTemplate ScriptTemplate { get; set; } = null!;

    public ExecutionStatus Status { get; set; }
    public string? PreviousCommands { get; set; } // JSON array of command summaries
    public string? CurrentCommand { get; set; } // JSON
    public string? NextCommands { get; set; } // JSON array

    // Navigation
    public ICollection<ExecutionLog> Logs { get; set; } = new List<ExecutionLog>();
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Paused,
    Canceled,
    Completed
}