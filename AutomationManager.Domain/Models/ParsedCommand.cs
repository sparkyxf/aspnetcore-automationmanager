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