using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Commands;

public record CreateScriptTemplateCommand(CreateScriptTemplateDto Dto) : IRequest<ScriptTemplateDto>;