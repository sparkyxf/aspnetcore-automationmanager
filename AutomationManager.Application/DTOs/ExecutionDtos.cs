using AutomationManager.Domain.Entities;

namespace AutomationManager.Application.DTOs;

public record ScriptExecutionDto(
    Guid Id,
    Guid AgentId,
    Guid ScriptTemplateId,
    ExecutionStatus Status,
    string? PreviousCommands,
    string? CurrentCommand,
    string? NextCommands
);

public record StartExecutionDto(
    Guid AgentId,
    Guid ScriptTemplateId
);