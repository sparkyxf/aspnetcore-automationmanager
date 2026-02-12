namespace AutomationManager.Contracts;

public record AgentResponse(
    Guid Id,
    string Name,
    string Status,
    DateTimeOffset LastSeen
);