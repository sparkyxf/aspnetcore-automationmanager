using AutomationManager.Domain.Entities;
using System.Linq;
using AutomationManager.Infrastructure;

namespace AutomationManager.Infrastructure;

public static class SeedData
{
    public static void Initialize(AutomationDbContext context)
    {
        if (context.Agents.Any()) return;

        var agents = new[]
        {
            new Agent { Id = Guid.NewGuid(), Name = "Agent1", Status = ConnectionStatus.Disconnected, LastSeen = DateTimeOffset.Now },
            new Agent { Id = Guid.NewGuid(), Name = "Agent2", Status = ConnectionStatus.Disconnected, LastSeen = DateTimeOffset.Now }
        };

        context.Agents.AddRange(agents);

        var templates = new[]
        {
            new ScriptTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Sample Script",
                Description = "A sample automation script",
                ScriptText = "KeyDown(A);\nDelay(1000);\nKeyUp(A);",
                Mode = ExecutionMode.RunOnce
            }
        };

        context.ScriptTemplates.AddRange(templates);
        context.SaveChanges();
    }
}