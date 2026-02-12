namespace AutomationManager.Domain.Entities;

public class ExecutionLog
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; }

    // Foreign key
    public Guid ExecutionId { get; set; }
    public ScriptExecution Execution { get; set; } = null!;
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}