using MediatR;

namespace AutomationManager.Application.Commands;

public record DeleteScriptTemplateCommand(Guid Id) : IRequest<Unit>;