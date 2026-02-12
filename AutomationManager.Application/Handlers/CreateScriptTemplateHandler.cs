using AutomationManager.Application.Commands;
using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Entities;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class CreateScriptTemplateHandler : IRequestHandler<CreateScriptTemplateCommand, ScriptTemplateDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateScriptTemplateHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ScriptTemplateDto> Handle(CreateScriptTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new ScriptTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            Description = request.Dto.Description,
            ScriptText = request.Dto.ScriptText,
            Mode = request.Dto.Mode,
            LoopCount = request.Dto.LoopCount
        };

        await _unitOfWork.ScriptTemplates.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

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