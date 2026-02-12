namespace AutomationManager.Domain.Entities;

public class ScriptTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ScriptText { get; set; } = string.Empty;
    public ExecutionMode Mode { get; set; }
    public int? LoopCount { get; set; }

    // Navigation
    public ICollection<ScriptCommandGroup> Groups { get; set; } = new List<ScriptCommandGroup>();
    public ICollection<ScriptExecution> Executions { get; set; } = new List<ScriptExecution>();
}

public enum ExecutionMode
{
    RunOnce,
    LoopXTimes,
    LoopUntilStopped
}