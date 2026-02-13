using AutomationManager.Domain.Models;

namespace AutomationManager.Domain.Services;

/// <summary>
/// Validates a full script text, returning all errors rather than throwing on the first one.
/// Covers syntax, required parameters, parameter ranges, and group reference integrity.
/// </summary>
public class ScriptValidator
{
    private readonly ScriptParser _parser;

    public ScriptValidator(ScriptParser parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Validates the given script text and returns a result containing all errors found.
    /// </summary>
    public ScriptValidationResult Validate(string scriptText)
    {
        var errors = new List<ScriptValidationError>();

        if (string.IsNullOrWhiteSpace(scriptText))
        {
            errors.Add(new ScriptValidationError(0, "Script text is empty."));
            return new ScriptValidationResult(false, errors);
        }

        // Phase 1: Try to parse the script
        ParsedScript parsed;
        try
        {
            parsed = _parser.ParseScript(scriptText);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(new ScriptValidationError(0, $"Parse error: {ex.Message}"));
            return new ScriptValidationResult(false, errors);
        }

        // Phase 2: Validate each top-level command's parameters
        for (int i = 0; i < parsed.Commands.Count; i++)
        {
            var cmd = parsed.Commands[i];
            ValidateCommand(cmd, i + 1, errors);
        }

        // Phase 3: Validate commands inside each group
        foreach (var group in parsed.Groups.Values)
        {
            if (group.Commands.Count == 0)
            {
                errors.Add(new ScriptValidationError(0,
                    $"Group '{group.Name}' has no commands."));
            }

            for (int i = 0; i < group.Commands.Count; i++)
            {
                var cmd = group.Commands[i];
                ValidateCommand(cmd, i + 1, errors, group.Name);
            }
        }

        // Phase 4: Validate ExecuteGroup references point to defined groups
        foreach (var cmd in parsed.Commands)
        {
            if (cmd.Type == CommandType.ExecuteGroup && cmd.Parameter is ExecuteGroupParameter grp)
            {
                if (!parsed.Groups.ContainsKey(grp.GroupName))
                {
                    errors.Add(new ScriptValidationError(0,
                        $"ExecuteGroup references undefined group '{grp.GroupName}'. " +
                        $"Available groups: [{string.Join(", ", parsed.Groups.Keys)}]"));
                }
            }
        }

        return new ScriptValidationResult(errors.Count == 0, errors);
    }

    private void ValidateCommand(ParsedCommand cmd, int commandIndex, List<ScriptValidationError> errors, string? groupName = null)
    {
        var location = groupName != null
            ? $"Group '{groupName}', command #{commandIndex}"
            : $"Command #{commandIndex}";

        // Validate that the correct parameter type is present
        switch (cmd.Type)
        {
            case CommandType.KeyDown:
            case CommandType.KeyUp:
            case CommandType.KeyPress:
                if (cmd.Parameter is not KeyParameter)
                {
                    errors.Add(new ScriptValidationError(commandIndex,
                        $"{location}: {cmd.Type} requires a key parameter ({string.Join(", ", Enum.GetNames(typeof(KeyCode)))})."));
                }
                break;

            case CommandType.MouseDown:
            case CommandType.MouseUp:
            case CommandType.MouseClick:
                if (cmd.Parameter is not MouseButtonParameter)
                {
                    errors.Add(new ScriptValidationError(commandIndex,
                        $"{location}: {cmd.Type} requires a mouse button parameter (Left, Right, Middle)."));
                }
                break;

            case CommandType.MouseMove:
                if (cmd.Parameter is not MouseMoveParameter moveParam)
                {
                    errors.Add(new ScriptValidationError(commandIndex,
                        $"{location}: MouseMove requires X,Y coordinate parameters."));
                }
                break;

            case CommandType.Delay:
                if (cmd.Parameter is not DelayParameter delayParam)
                {
                    errors.Add(new ScriptValidationError(commandIndex,
                        $"{location}: Delay requires a milliseconds parameter."));
                }
                else
                {
                    if (delayParam.Milliseconds <= 0)
                    {
                        errors.Add(new ScriptValidationError(commandIndex,
                            $"{location}: Delay milliseconds must be greater than 0 (got {delayParam.Milliseconds})."));
                    }
                    if (delayParam.Milliseconds > 3600000)
                    {
                        errors.Add(new ScriptValidationError(commandIndex,
                            $"{location}: Delay exceeds maximum of 3600000ms (1 hour). Got {delayParam.Milliseconds}ms."));
                    }
                }
                break;

            case CommandType.ExecuteGroup:
                if (cmd.Parameter is not ExecuteGroupParameter groupParam)
                {
                    errors.Add(new ScriptValidationError(commandIndex,
                        $"{location}: ExecuteGroup requires parameters (GroupName, LoopCount)."));
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(groupParam.GroupName))
                    {
                        errors.Add(new ScriptValidationError(commandIndex,
                            $"{location}: ExecuteGroup group name cannot be empty."));
                    }
                    if (groupParam.LoopCount <= 0)
                    {
                        errors.Add(new ScriptValidationError(commandIndex,
                            $"{location}: ExecuteGroup loop count must be greater than 0 (got {groupParam.LoopCount})."));
                    }
                    if (groupParam.LoopCount > 100000)
                    {
                        errors.Add(new ScriptValidationError(commandIndex,
                            $"{location}: ExecuteGroup loop count exceeds maximum of 100000 (got {groupParam.LoopCount})."));
                    }
                }
                break;

            default:
                errors.Add(new ScriptValidationError(commandIndex,
                    $"{location}: Unsupported command type '{cmd.Type}'."));
                break;
        }
    }

    private static bool IsAllowedSpecialKey(char key)
    {
        // Allow common keyboard characters
        return key is ' ' or '\t' or '.' or ',' or ';' or ':' or '!' or '?'
            or '-' or '_' or '+' or '=' or '/' or '\\' or '(' or ')' or '['
            or ']' or '{' or '}' or '<' or '>' or '@' or '#' or '$' or '%'
            or '^' or '&' or '*' or '~' or '`' or '\'' or '"';
    }
}

/// <summary>
/// Result of script validation containing all errors found.
/// </summary>
public record ScriptValidationResult(bool IsValid, List<ScriptValidationError> Errors);

/// <summary>
/// A single validation error with optional line/command index reference.
/// </summary>
public record ScriptValidationError(int CommandIndex, string Message);
