using AutomationManager.Domain.Entities;

namespace AutomationManager.Application.DTOs;

public record ScriptTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string ScriptText,
    ExecutionMode Mode,
    int? LoopCount
);

public record CreateScriptTemplateDto(
    string Name,
    string? Description,
    string ScriptText,
    ExecutionMode Mode,
    int? LoopCount
);

public record UpdateScriptTemplateDto(
    string Name,
    string? Description,
    string ScriptText,
    ExecutionMode Mode,
    int? LoopCount
);