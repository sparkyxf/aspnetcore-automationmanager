using MediatR;

namespace AutomationManager.Application.Commands;

public record PauseExecutionCommand(Guid ExecutionId) : IRequest<Unit>;