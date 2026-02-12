using AutomationManager.Application.Handlers;
using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Services;
using AutomationManager.Domain.Validators;
using AutomationManager.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace AutomationManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AutomationDbContext>(options =>
        {
            if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("InMemory"))
            {
                options.UseInMemoryDatabase("AutomationDb");
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Domain services
        services.AddScoped<ScriptParser>();
        services.AddScoped<IExecutionEngine, ExecutionEngine>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<AgentValidator>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateScriptTemplateHandler).Assembly));

        return services;
    }
}