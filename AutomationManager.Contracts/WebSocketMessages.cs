namespace AutomationManager.Contracts;

public record AgentStatusMessage(
    Guid AgentId,
    string Status,
    List<string>? PreviousCommands,
    string? CurrentCommand,
    List<string>? NextCommands,
    int? CursorX,
    int? CursorY,
    string? ScriptExecutionStatus = null,
    int? CurrentCommandIndex = null,
    int? TotalCommands = null,
    string? ErrorMessage = null,
    int? CurrentLoop = null,
    int? TotalLoops = null,
    string? ExecutionMode = null
);

public record ExecutionControlMessage(
    string SetMode, // run, pause, resume, stop
    string? ScriptText = null, // Required when SetMode is "run"
    ExecutionMode? Mode = null,
    int? LoopCount = null
);

public record ScriptExecutionCommand(
    Guid AgentId,
    string SetMode, // run, pause, resume, stop
    string? ScriptText = null,
    ExecutionMode Mode = ExecutionMode.RunOnce,
    int? LoopCount = null
);

public record ScriptExecutionRequest(
    Guid AgentId,
    string ScriptText,
    ExecutionMode Mode = ExecutionMode.RunOnce,
    int? LoopCount = null
);

public record AgentExecutionResponse(
    bool Success,
    string? Message = null,
    Guid? ExecutionId = null
);

public enum ScriptExecutionStatus
{
    Idle,
    Running,
    Paused,
    Completed,
    Stopped,
    Error
}

public enum ExecutionMode
{
    RunOnce,
    LoopXTimes,
    LoopUntilStopped
}