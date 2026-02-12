using MediatR;

namespace AutomationManager.Application.Commands;

public record CancelExecutionCommand(Guid ExecutionId) : IRequest<Unit>;