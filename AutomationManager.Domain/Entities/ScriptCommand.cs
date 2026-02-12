namespace AutomationManager.Domain.Entities;

using AutomationManager.Domain.Models;

public class ScriptCommand
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public CommandType Type { get; set; }
    public string Parameters { get; set; } = string.Empty; // JSON

    // Foreign key
    public Guid? GroupId { get; set; }
    public ScriptCommandGroup? Group { get; set; }
}