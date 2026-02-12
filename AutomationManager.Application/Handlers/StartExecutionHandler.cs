using AutomationManager.Application.Commands;
using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Entities;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class StartExecutionHandler : IRequestHandler<StartExecutionCommand, ScriptExecutionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public StartExecutionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ScriptExecutionDto> Handle(StartExecutionCommand request, CancellationToken cancellationToken)
    {
        var agent = await _unitOfWork.Agents.GetByIdAsync(request.Dto.AgentId);
        if (agent is null) throw new KeyNotFoundException("Agent not found");

        var template = await _unitOfWork.ScriptTemplates.GetByIdAsync(request.Dto.ScriptTemplateId);
        if (template is null) throw new KeyNotFoundException("Script template not found");

        var execution = new ScriptExecution
        {
            Id = Guid.NewGuid(),
            AgentId = request.Dto.AgentId,
            ScriptTemplateId = request.Dto.ScriptTemplateId,
            Status = ExecutionStatus.Pending
        };

        await _unitOfWork.Executions.AddAsync(execution);
        await _unitOfWork.SaveChangesAsync();

        return new ScriptExecutionDto(
            execution.Id,
            execution.AgentId,
            execution.ScriptTemplateId,
            execution.Status,
            execution.PreviousCommands,
            execution.CurrentCommand,
            execution.NextCommands
        );
    }
}