using AutomationManager.Application.Commands;
using AutomationManager.Application.Interfaces;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class DeleteScriptTemplateHandler : IRequestHandler<DeleteScriptTemplateCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteScriptTemplateHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteScriptTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.ScriptTemplates.GetByIdAsync(request.Id);
        if (template is null) throw new KeyNotFoundException("Script template not found");

        _unitOfWork.ScriptTemplates.Delete(template);
        await _unitOfWork.SaveChangesAsync();

        return Unit.Value;
    }
}