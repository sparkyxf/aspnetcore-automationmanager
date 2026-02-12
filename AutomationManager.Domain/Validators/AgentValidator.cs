using AutomationManager.Domain.Entities;
using FluentValidation;

namespace AutomationManager.Domain.Validators;

public class AgentValidator : AbstractValidator<Agent>
{
    public AgentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}