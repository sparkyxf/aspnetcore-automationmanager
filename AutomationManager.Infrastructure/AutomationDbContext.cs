using AutomationManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutomationManager.Infrastructure;

public class AutomationDbContext : DbContext
{
    public AutomationDbContext(DbContextOptions<AutomationDbContext> options) : base(options) { }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<ScriptTemplate> ScriptTemplates => Set<ScriptTemplate>();
    public DbSet<ScriptCommandGroup> CommandGroups => Set<ScriptCommandGroup>();
    public DbSet<ScriptCommand> Commands => Set<ScriptCommand>();
    public DbSet<ScriptExecution> Executions => Set<ScriptExecution>();
    public DbSet<ExecutionLog> Logs => Set<ExecutionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScriptCommandGroup>()
            .HasMany(g => g.Commands)
            .WithOne(c => c.Group)
            .HasForeignKey(c => c.GroupId);

        modelBuilder.Entity<ScriptExecution>()
            .HasOne(e => e.Agent)
            .WithMany(a => a.Executions)
            .HasForeignKey(e => e.AgentId);

        modelBuilder.Entity<ScriptExecution>()
            .HasOne(e => e.ScriptTemplate)
            .WithMany(t => t.Executions)
            .HasForeignKey(e => e.ScriptTemplateId);

        modelBuilder.Entity<ExecutionLog>()
            .HasOne(l => l.Execution)
            .WithMany(e => e.Logs)
            .HasForeignKey(l => l.ExecutionId);
    }
}