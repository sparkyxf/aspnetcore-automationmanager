using AutomationManager.Domain.Entities;
using AutomationManager.Domain.Models;

namespace AutomationManager.Domain.Services;

public class ScriptParser
{
    /// <summary>
    /// Parses a full script including @group definitions and top-level commands.
    /// Group syntax: @group(Name) { ... }
    /// </summary>
    public ParsedScript ParseScript(string scriptText)
    {
        var groups = new Dictionary<string, CommandGroup>(StringComparer.OrdinalIgnoreCase);
        var topLevelCommands = new List<ParsedCommand>();
        var lines = scriptText.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
            {
                i++;
                continue;
            }

            // Check for @group(Name) {
            if (trimmed.StartsWith("@group(", StringComparison.OrdinalIgnoreCase))
            {
                var (group, nextIndex) = ParseGroupDefinition(lines, i);
                if (groups.ContainsKey(group.Name))
                    throw new InvalidOperationException($"Duplicate group definition: '{group.Name}'");
                groups[group.Name] = group;
                i = nextIndex;
                continue;
            }

            // Regular command line
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                if (!trimmed.EndsWith(';'))
                    throw new InvalidOperationException($"Invalid command (missing ';'): {trimmed}");

                var command = trimmed[..^1];
                topLevelCommands.Add(ParseCommand(command));
            }

            i++;
        }

        return new ParsedScript(topLevelCommands, groups);
    }

    /// <summary>
    /// Legacy method: parses only top-level commands (no group definitions).
    /// Kept for backward compatibility.
    /// </summary>
    public IEnumerable<ParsedCommand> Parse(string scriptText)
    {
        var result = ParseScript(scriptText);
        return result.Commands;
    }

    private (CommandGroup Group, int NextLineIndex) ParseGroupDefinition(string[] lines, int startIndex)
    {
        var header = lines[startIndex].Trim();

        // Parse @group(Name) { or @group(Name){
        var parenOpen = header.IndexOf('(');
        var parenClose = header.IndexOf(')');
        if (parenOpen < 0 || parenClose < 0 || parenClose <= parenOpen + 1)
            throw new InvalidOperationException($"Invalid group definition syntax: {header}");

        var groupName = header[(parenOpen + 1)..parenClose].Trim();
        if (string.IsNullOrWhiteSpace(groupName))
            throw new InvalidOperationException($"Group name cannot be empty: {header}");

        // Expect '{' after the group declaration (on same line or next line)
        var afterParen = header[(parenClose + 1)..].Trim();
        int bodyStart;
        if (afterParen.StartsWith("{"))
        {
            bodyStart = startIndex + 1;
        }
        else
        {
            // Look for '{' on next non-empty line
            bodyStart = startIndex + 1;
            while (bodyStart < lines.Length && string.IsNullOrWhiteSpace(lines[bodyStart].Trim()))
                bodyStart++;
            if (bodyStart >= lines.Length || lines[bodyStart].Trim() != "{")
                throw new InvalidOperationException($"Expected '{{' after group definition: {header}");
            bodyStart++;
        }

        // Collect commands until '}' or '};'
        var groupCommands = new List<ParsedCommand>();
        int j = bodyStart;
        while (j < lines.Length)
        {
            var line = lines[j].Trim();

            if (line == "}" || line == "};")
            {
                return (new CommandGroup(groupName, groupCommands), j + 1);
            }

            if (!string.IsNullOrEmpty(line) && !line.StartsWith("//"))
            {
                if (!line.EndsWith(';'))
                    throw new InvalidOperationException($"Invalid command in group '{groupName}' (missing ';'): {line}");

                var cmd = line[..^1];
                var parsed = ParseCommand(cmd);

                // Groups cannot contain ExecuteGroup to prevent infinite recursion
                if (parsed.Type == CommandType.ExecuteGroup)
                    throw new InvalidOperationException(
                        $"ExecuteGroup cannot be used inside a group definition (group '{groupName}')");

                groupCommands.Add(parsed);
            }

            j++;
        }

        throw new InvalidOperationException($"Unclosed group definition: '{groupName}' (missing closing '}}')");
    }

    internal ParsedCommand ParseCommand(string command)
    {
        var parts = command.Split('(', 2);
        if (parts.Length != 2 || !parts[1].EndsWith(')'))
            throw new InvalidOperationException($"Invalid command format: {command}");

        var typeStr = parts[0].Trim();
        var paramStr = parts[1][..^1].Trim();

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
                Enum.TryParse<KeyCode>(paramStr, out var key) ? new KeyParameter(key) :
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
        throw new InvalidOperationException($"Invalid ExecuteGroup parameters (expected: GroupName, LoopCount): {paramStr}");
    }
}