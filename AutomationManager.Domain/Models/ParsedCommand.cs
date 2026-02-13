namespace AutomationManager.Domain.Models;

public record ParsedCommand(CommandType Type, CommandParameter Parameter);

public enum CommandType
{
    KeyDown,
    KeyUp,
    KeyPress,
    MouseDown,
    MouseUp,
    MouseClick,
    MouseMove,
    Delay,
    ExecuteGroup
}

/// <summary>
/// Represents a named group of commands that can be invoked via ExecuteGroup.
/// Defined in script text with: @group(Name) { ... }
/// </summary>
public record CommandGroup(string Name, List<ParsedCommand> Commands);

/// <summary>
/// The complete result of parsing a script, including top-level commands and group definitions.
/// </summary>
public record ParsedScript(List<ParsedCommand> Commands, Dictionary<string, CommandGroup> Groups);

