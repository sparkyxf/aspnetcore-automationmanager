namespace AutomationManager.Domain.Entities;

public class ScriptCommandGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Foreign key
    public Guid ScriptTemplateId { get; set; }
    public ScriptTemplate ScriptTemplate { get; set; } = null!;

    // Navigation
    public ICollection<ScriptCommand> Commands { get; set; } = new List<ScriptCommand>();
}