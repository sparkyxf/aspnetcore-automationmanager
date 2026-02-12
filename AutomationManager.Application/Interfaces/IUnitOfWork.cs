using AutomationManager.Domain.Entities;

namespace AutomationManager.Application.Interfaces;

public interface IUnitOfWork
{
    IRepository<Agent> Agents { get; }
    IRepository<ScriptTemplate> ScriptTemplates { get; }
    IRepository<ScriptExecution> Executions { get; }
    IRepository<ExecutionLog> Logs { get; }
    Task<int> SaveChangesAsync();
}