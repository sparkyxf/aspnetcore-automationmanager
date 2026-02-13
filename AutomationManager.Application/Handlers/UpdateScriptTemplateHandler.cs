using AutomationManager.Application.Commands;
using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Services;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class UpdateScriptTemplateHandler : IRequestHandler<UpdateScriptTemplateCommand, ScriptTemplateDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScriptValidator _scriptValidator;

    public UpdateScriptTemplateHandler(IUnitOfWork unitOfWork, ScriptValidator scriptValidator)
    {
        _unitOfWork = unitOfWork;
        _scriptValidator = scriptValidator;
    }

    public async Task<ScriptTemplateDto> Handle(UpdateScriptTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.ScriptTemplates.GetByIdAsync(request.Id);
        if (template is null) throw new KeyNotFoundException("Script template not found");

        // Validate script text
        var validationResult = _scriptValidator.Validate(request.Dto.ScriptText);
        if (!validationResult.IsValid)
        {
            var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Script validation failed: {errorMessages}");
        }

        template.Name = request.Dto.Name;
        template.Description = request.Dto.Description;
        template.ScriptText = request.Dto.ScriptText;
        template.Mode = request.Dto.Mode;
        template.LoopCount = request.Dto.LoopCount;

        _unitOfWork.ScriptTemplates.Update(template);
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