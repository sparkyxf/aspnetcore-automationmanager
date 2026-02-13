using AutomationManager.Application.Commands;
using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Entities;
using AutomationManager.Domain.Services;
using MediatR;

namespace AutomationManager.Application.Handlers;

public class CreateScriptTemplateHandler : IRequestHandler<CreateScriptTemplateCommand, ScriptTemplateDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScriptValidator _scriptValidator;

    public CreateScriptTemplateHandler(IUnitOfWork unitOfWork, ScriptValidator scriptValidator)
    {
        _unitOfWork = unitOfWork;
        _scriptValidator = scriptValidator;
    }

    public async Task<ScriptTemplateDto> Handle(CreateScriptTemplateCommand request, CancellationToken cancellationToken)
    {
        // Validate script text
        var validationResult = _scriptValidator.Validate(request.Dto.ScriptText);
        if (!validationResult.IsValid)
        {
            var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Script validation failed: {errorMessages}");
        }

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