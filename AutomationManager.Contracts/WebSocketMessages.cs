namespace AutomationManager.Contracts;

public record AgentStatusMessage(
    Guid AgentId,
    string Status,
    string? PreviousCommands,
    string? CurrentCommand,
    string? NextCommands,
    int? CursorX,
    int? CursorY
);

public record ExecutionControlMessage(
    string Action, // Start, Pause, Cancel
    Guid? ExecutionId,
    Guid? TemplateId
);