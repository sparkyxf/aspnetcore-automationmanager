using AutomationManager.Domain.Entities;
using AutomationManager.Domain.Models;

namespace AutomationManager.Domain.Services;

public class ScriptParser
{
    public IEnumerable<ParsedCommand> Parse(string scriptText)
    {
        var lines = scriptText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//")) continue;

            if (!trimmed.EndsWith(';'))
                throw new InvalidOperationException($"Invalid command: {trimmed}");

            var command = trimmed[..^1]; // remove ;
            yield return ParseCommand(command);
        }
    }

    private ParsedCommand ParseCommand(string command)
    {
        var parts = command.Split('(', 2);
        if (parts.Length != 2 || !parts[1].EndsWith(')'))
            throw new InvalidOperationException($"Invalid command format: {command}");

        var typeStr = parts[0].Trim();
        var paramStr = parts[1][..^1].Trim(); // remove )

        if (!Enum.TryParse<CommandType>(typeStr, out var type))
            throw new InvalidOperationException($"Unknown command type: {typeStr}");

        var parameter = ParseParameter(type, paramStr);
        return new ParsedCommand(type, parameter);
    }

    private CommandParameter ParseParameter(CommandType type, string paramStr)
    {
        return type switch
        {
            CommandType.KeyDown or CommandType.KeyUp or CommandType.KeyPress =>
                paramStr.Length == 1 ? new KeyParameter(paramStr[0]) :
                throw new InvalidOperationException($"Invalid key parameter: {paramStr}"),
            CommandType.MouseDown or CommandType.MouseUp or CommandType.MouseClick =>
                Enum.TryParse<MouseButton>(paramStr, out var button) ? new MouseButtonParameter(button) :
                throw new InvalidOperationException($"Invalid mouse button: {paramStr}"),
            CommandType.MouseMove => ParseMouseMoveParameter(paramStr),
            CommandType.Delay =>
                int.TryParse(paramStr, out var ms) ? new DelayParameter(ms) :
                throw new InvalidOperationException($"Invalid delay parameter: {paramStr}"),
            CommandType.ExecuteGroup => ParseExecuteGroupParameter(paramStr),
            _ => throw new InvalidOperationException($"Unsupported command type: {type}")
        };
    }

    private MouseMoveParameter ParseMouseMoveParameter(string paramStr)
    {
        var coords = paramStr.Split(',');
        if (coords.Length == 2 && int.TryParse(coords[0].Trim(), out var x) && int.TryParse(coords[1].Trim(), out var y))
            return new MouseMoveParameter(x, y);
        throw new InvalidOperationException($"Invalid mouse move parameters: {paramStr}");
    }

    private ExecuteGroupParameter ParseExecuteGroupParameter(string paramStr)
    {
        var groupParts = paramStr.Split(',');
        if (groupParts.Length == 2 && int.TryParse(groupParts[1].Trim(), out var count))
            return new ExecuteGroupParameter(groupParts[0].Trim(), count);
        throw new InvalidOperationException($"Invalid execute group parameters: {paramStr}");
    }
}