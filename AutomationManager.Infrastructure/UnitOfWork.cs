using AutomationManager.Application.Interfaces;
using AutomationManager.Domain.Entities;

namespace AutomationManager.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AutomationDbContext _context;

    public UnitOfWork(AutomationDbContext context)
    {
        _context = context;
        Agents = new Repository<Agent>(_context);
        ScriptTemplates = new Repository<ScriptTemplate>(_context);
        Executions = new Repository<ScriptExecution>(_context);
        Logs = new Repository<ExecutionLog>(_context);
    }

    public IRepository<Agent> Agents { get; }
    public IRepository<ScriptTemplate> ScriptTemplates { get; }
    public IRepository<ScriptExecution> Executions { get; }
    public IRepository<ExecutionLog> Logs { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}