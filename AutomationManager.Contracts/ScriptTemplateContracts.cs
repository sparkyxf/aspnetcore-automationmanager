namespace AutomationManager.Contracts;

public record ScriptTemplateRequest(
    string Name,
    string? Description,
    string ScriptText,
    string Mode,
    int? LoopCount
);

public record ScriptTemplateResponse(
    Guid Id,
    string Name,
    string? Description,
    string ScriptText,
    string Mode,
    int? LoopCount
);