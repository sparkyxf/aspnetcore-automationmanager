using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Queries;

public record GetScriptTemplateByIdQuery(Guid Id) : IRequest<ScriptTemplateDto?>;
