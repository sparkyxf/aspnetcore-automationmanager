using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Commands;

public record UpdateScriptTemplateCommand(Guid Id, UpdateScriptTemplateDto Dto) : IRequest<ScriptTemplateDto>;