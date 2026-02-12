using AutomationManager.Domain.Entities;
using FluentValidation;

namespace AutomationManager.Domain.Validators;

public class ScriptTemplateValidator : AbstractValidator<ScriptTemplate>
{
    public ScriptTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ScriptText).NotEmpty();
        RuleFor(x => x.LoopCount).GreaterThan(0).When(x => x.Mode == ExecutionMode.LoopXTimes);
    }
}