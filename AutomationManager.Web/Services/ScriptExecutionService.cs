using AutomationManager.Contracts;
using AutomationManager.SDK;
using AutomationManager.Web.Models;

namespace AutomationManager.Web.Services;

public class ScriptExecutionService
{
    private readonly AutomationApiClient _apiClient;
    private readonly ILogger<ScriptExecutionService> _logger;

    public ScriptExecutionService(AutomationApiClient apiClient, ILogger<ScriptExecutionService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public event Action? OnExecutionStatusChanged;

    public async Task<ScriptTemplateResponse?> CreateScriptTemplateAsync(CreateScriptTemplateRequest request)
    {
        try
        {
            return await _apiClient.CreateScriptTemplateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create script template");
            return null;
        }
    }

    public async Task<ScriptTemplateResponse?> UpdateScriptTemplateAsync(Guid id, UpdateScriptTemplateRequest request)
    {
        try
        {
            return await _apiClient.UpdateScriptTemplateAsync(id, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update script template");
            return null;
        }
    }

    public async Task<bool> StartExecutionAsync(Guid agentId, Guid templateId)
    {
        try
        {
            var request = new StartExecutionRequest
            {
                AgentId = agentId,
                ScriptTemplateId = templateId
            };
            await _apiClient.StartExecutionAsync(request);
            OnExecutionStatusChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start execution");
            return false;
        }
    }

    public async Task<bool> PauseExecutionAsync(Guid executionId)
    {
        try
        {
            await _apiClient.PauseExecutionAsync(executionId);
            OnExecutionStatusChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause execution");
            return false;
        }
    }

    public async Task<bool> CancelExecutionAsync(Guid executionId)
    {
        try
        {
            await _apiClient.CancelExecutionAsync(executionId);
            OnExecutionStatusChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel execution");
            return false;
        }
    }

    public async Task<IEnumerable<ScriptTemplateResponse>> GetScriptTemplatesAsync()
    {
        try
        {
            var response = await _apiClient.GetScriptTemplatesAsync();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get script templates");
            return Enumerable.Empty<ScriptTemplateResponse>();
        }
    }

    public async Task<ScriptTemplateResponse?> GetScriptTemplateAsync(Guid id)
    {
        try
        {
            return await _apiClient.GetScriptTemplateAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get script template");
            return null;
        }
    }

    public async Task<bool> DeleteScriptTemplateAsync(Guid id)
    {
        try
        {
            await _apiClient.DeleteScriptTemplateAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete script template");
            return false;
        }
    }

    public bool ValidateScript(string scriptText, out List<string> errors)
    {
        errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            errors.Add("Script text cannot be empty");
            return false;
        }

        var lines = scriptText.Split('\n');
        var validCommands = new HashSet<string> { "KeyDown", "KeyUp", "KeyPress", "MouseDown", "MouseUp", "MouseClick", "MouseMove", "Delay", "ExecuteGroup" };
        var definedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedGroups = new List<(string Name, int Line)>();
        int i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
            {
                i++;
                continue;
            }

            // Handle @group definition
            if (trimmed.StartsWith("@group(", StringComparison.OrdinalIgnoreCase))
            {
                var parenClose = trimmed.IndexOf(')');
                if (parenClose < 0)
                {
                    errors.Add($"Line {i + 1}: Invalid group definition syntax: {trimmed}");
                    i++;
                    continue;
                }

                var groupName = trimmed[7..parenClose].Trim();
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    errors.Add($"Line {i + 1}: Group name cannot be empty");
                    i++;
                    continue;
                }

                if (definedGroups.Contains(groupName))
                {
                    errors.Add($"Line {i + 1}: Duplicate group definition '{groupName}'");
                }
                definedGroups.Add(groupName);

                // Find opening brace
                var afterParen = trimmed[(parenClose + 1)..].Trim();
                int bodyStart;
                if (afterParen.StartsWith("{"))
                {
                    bodyStart = i + 1;
                }
                else
                {
                    bodyStart = i + 1;
                    while (bodyStart < lines.Length && string.IsNullOrWhiteSpace(lines[bodyStart].Trim()))
                        bodyStart++;
                    if (bodyStart >= lines.Length || lines[bodyStart].Trim() != "{")
                    {
                        errors.Add($"Line {i + 1}: Expected '{{' after group definition");
                        i++;
                        continue;
                    }
                    bodyStart++;
                }

                // Parse group body until closing brace
                bool closed = false;
                int j = bodyStart;
                int cmdInGroup = 0;
                while (j < lines.Length)
                {
                    var line = lines[j].Trim();
                    if (line == "}" || line == "};")
                    {
                        closed = true;
                        i = j + 1;
                        break;
                    }

                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("//"))
                    {
                        if (!line.EndsWith(';'))
                        {
                            errors.Add($"Line {j + 1}: Command missing semicolon in group '{groupName}'");
                        }
                        else
                        {
                            var cmdText = line[..^1].Trim();
                            ValidateCommandLine(cmdText, j + 1, validCommands, errors, groupName);

                            // Check for ExecuteGroup inside group (not allowed)
                            if (cmdText.StartsWith("ExecuteGroup"))
                            {
                                errors.Add($"Line {j + 1}: ExecuteGroup cannot be used inside a group definition (group '{groupName}')");
                            }
                        }
                        cmdInGroup++;
                    }
                    j++;
                }

                if (!closed)
                {
                    errors.Add($"Line {i + 1}: Unclosed group definition '{groupName}' (missing closing '}}').");
                    i = j;
                }

                if (cmdInGroup == 0)
                {
                    errors.Add($"Group '{groupName}' has no commands.");
                }

                continue;
            }

            // Regular command line
            if (!trimmed.EndsWith(';'))
            {
                errors.Add($"Line {i + 1}: Command missing semicolon: {trimmed}");
                i++;
                continue;
            }

            var command = trimmed[..^1].Trim();
            ValidateCommandLine(command, i + 1, validCommands, errors);

            // Track ExecuteGroup references
            if (command.StartsWith("ExecuteGroup(") && command.EndsWith(")"))
            {
                var paramStr = command[13..^1];
                var parts = paramStr.Split(',');
                if (parts.Length >= 1)
                {
                    referencedGroups.Add((parts[0].Trim(), i + 1));
                }
            }

            i++;
        }

        // Validate group references
        foreach (var (name, line) in referencedGroups)
        {
            if (!definedGroups.Contains(name))
            {
                errors.Add($"Line {line}: ExecuteGroup references undefined group '{name}'. " +
                    $"Available groups: [{string.Join(", ", definedGroups)}]");
            }
        }

        return errors.Count == 0;
    }

    private void ValidateCommandLine(string command, int lineNumber, HashSet<string> validCommands, List<string> errors, string? groupName = null)
    {
        var location = groupName != null ? $"Line {lineNumber} (group '{groupName}')" : $"Line {lineNumber}";

        if (!command.Contains('(') || !command.Contains(')'))
        {
            errors.Add($"{location}: Invalid format. Expected: CommandName(parameters)");
            return;
        }

        var commandName = command.Split('(')[0].Trim();
        if (!validCommands.Contains(commandName))
        {
            errors.Add($"{location}: Unknown command '{commandName}'");
            return;
        }

        var paramStr = command[(command.IndexOf('(') + 1)..command.LastIndexOf(')')].Trim();

        switch (commandName)
        {
            case "KeyDown" or "KeyUp" or "KeyPress":
                if (!Enum.TryParse<KeyCode>(paramStr, out var keyCode))
                    errors.Add($"{location}: {commandName} requires a valid key parameter ({string.Join(", ", Enum.GetNames(typeof(KeyCode)))}).");
                break;
            case "MouseDown" or "MouseUp" or "MouseClick":
                if (paramStr != "Left" && paramStr != "Right" && paramStr != "Middle")
                    errors.Add($"{location}: {commandName} requires Left, Right, or Middle");
                break;
            case "MouseMove":
                var coords = paramStr.Split(',');
                if (coords.Length != 2 || !int.TryParse(coords[0].Trim(), out var x) || !int.TryParse(coords[1].Trim(), out var y))
                    errors.Add($"{location}: MouseMove requires X,Y integer coordinates");
                break;
            case "Delay":
                if (!int.TryParse(paramStr, out var ms))
                    errors.Add($"{location}: Delay requires integer milliseconds");
                else if (ms <= 0)
                    errors.Add($"{location}: Delay must be greater than 0ms");
                else if (ms > 3600000)
                    errors.Add($"{location}: Delay exceeds maximum of 3600000ms (1 hour)");
                break;
            case "ExecuteGroup":
                var groupParts = paramStr.Split(',');
                if (groupParts.Length != 2)
                    errors.Add($"{location}: ExecuteGroup requires GroupName and LoopCount parameters");
                else if (!int.TryParse(groupParts[1].Trim(), out var loopCount))
                    errors.Add($"{location}: ExecuteGroup loop count must be an integer");
                else if (loopCount <= 0)
                    errors.Add($"{location}: ExecuteGroup loop count must be greater than 0");
                else if (loopCount > 100000)
                    errors.Add($"{location}: ExecuteGroup loop count exceeds maximum of 100000");
                break;
        }
    }

    public List<ScriptCommandGroup> ParseCommandGroups(string scriptText)
    {
        var groups = new List<ScriptCommandGroup>();
        // This would be implemented to parse groups from script text
        // For now, return empty list as groups are not yet fully implemented
        return groups;
    }

    public async Task<bool> RunScriptOnAgentAsync(Guid agentId, string scriptText, ExecutionMode mode = ExecutionMode.RunOnce, int? loopCount = null)
    {
        try
        {
            return await _apiClient.RunScriptOnAgentAsync(agentId, scriptText, mode, loopCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run script on agent");
            return false;
        }
    }

    public async Task<bool> PauseAgentAsync(Guid agentId)
    {
        try
        {
            return await _apiClient.PauseAgentAsync(agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause agent");
            return false;
        }
    }

    public async Task<bool> StopAgentAsync(Guid agentId)
    {
        try
        {
            return await _apiClient.StopAgentAsync(agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop agent");
            return false;
        }
    }

    public async Task<bool> ResumeAgentAsync(Guid agentId)
    {
        try
        {
            return await _apiClient.ResumeAgentAsync(agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume agent");
            return false;
        }
    }
}

public class ScriptCommandGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = new();
}