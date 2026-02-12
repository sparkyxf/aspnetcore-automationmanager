using AutomationManager.Application.Commands;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Entities;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class PauseExecutionHandler : IRequestHandler<PauseExecutionCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;

    public PauseExecutionHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(PauseExecutionCommand request, CancellationToken cancellationToken)
    {
        var execution = await _unitOfWork.Executions.GetByIdAsync(request.ExecutionId);
        if (execution is null) throw new KeyNotFoundException("Execution not found");

        execution.Status = ExecutionStatus.Paused;
        _unitOfWork.Executions.Update(execution);
        await _unitOfWork.SaveChangesAsync();

        return Unit.Value;
    }
}