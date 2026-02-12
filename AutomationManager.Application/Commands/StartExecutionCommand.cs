using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Commands;

public record StartExecutionCommand(StartExecutionDto Dto) : IRequest<ScriptExecutionDto>;