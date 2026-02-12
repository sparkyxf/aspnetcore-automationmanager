using AutomationManager.Domain.Entities;

namespace AutomationManager.Application.DTOs;

public record AgentDto(
    Guid Id,
    string Name,
    ConnectionStatus Status,
    DateTimeOffset LastSeen
);

public record CreateAgentDto(
    string Name
);