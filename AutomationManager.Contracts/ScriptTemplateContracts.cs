namespace AutomationManager.Contracts;

public record ScriptTemplateRequest(
    string Name,
    string? Description,
    string ScriptText,
    string Mode,
    int? LoopCount
);

public class CreateScriptTemplateRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ScriptText { get; set; } = "";
    public string Mode { get; set; } = "";
    public int? LoopCount { get; set; }
}

public class UpdateScriptTemplateRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ScriptText { get; set; } = "";
    public string Mode { get; set; } = "";
    public int? LoopCount { get; set; }
}

public record ScriptTemplateResponse(
    Guid Id,
    string Name,
    string? Description,
    string ScriptText,
    string Mode,
    int? LoopCount
);

public record ScriptExecutionResponse(
    Guid Id,
    Guid TemplateId,
    string Status,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    string? Output,
    string? ErrorOutput
);

public class StartExecutionRequest
{
    public Guid TemplateId { get; set; }
    public Guid AgentId { get; set; }
    public Guid ScriptTemplateId { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}