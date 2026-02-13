using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Application.Queries;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class GetScriptTemplateByIdHandler : IRequestHandler<GetScriptTemplateByIdQuery, ScriptTemplateDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetScriptTemplateByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ScriptTemplateDto?> Handle(GetScriptTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.ScriptTemplates.GetByIdAsync(request.Id);
        
        if (template == null)
            return null;
        
        return new ScriptTemplateDto(
            template.Id,
            template.Name,
            template.Description,
            template.ScriptText,
            template.Mode,
            template.LoopCount
        );
    }
}
