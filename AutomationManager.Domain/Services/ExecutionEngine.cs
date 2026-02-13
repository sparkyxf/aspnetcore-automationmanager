using AutomationManager.Domain.Entities;
using AutomationManager.Domain.Models;

namespace AutomationManager.Domain.Services;

public interface IExecutionEngine
{
    Task ExecuteAsync(ScriptExecution execution, CancellationToken cancellationToken = default);
}

public class ExecutionEngine : IExecutionEngine
{
    private readonly ScriptParser _parser;

    public ExecutionEngine(ScriptParser parser)
    {
        _parser = parser;
    }

    public async Task ExecuteAsync(ScriptExecution execution, CancellationToken cancellationToken = default)
    {
        var parsed = _parser.ParseScript(execution.ScriptTemplate.ScriptText);
        var commands = parsed.Commands;
        var groups = parsed.Groups;
        var index = 0;

        while (index < commands.Count && !cancellationToken.IsCancellationRequested)
        {
            var command = commands[index];
            await ExecuteCommandAsync(command, groups, cancellationToken);

            execution.PreviousCommands = GetPreviousCommands(commands, index);
            execution.CurrentCommand = GetCurrentCommand(command);
            execution.NextCommands = GetNextCommands(commands, index);

            index++;
        }

        execution.Status = cancellationToken.IsCancellationRequested ? ExecutionStatus.Canceled : ExecutionStatus.Completed;
    }

    private async Task ExecuteCommandAsync(ParsedCommand command, Dictionary<string, CommandGroup> groups, CancellationToken cancellationToken)
    {
        if (command.Type == CommandType.ExecuteGroup && command.Parameter is ExecuteGroupParameter groupParam)
        {
            if (!groups.TryGetValue(groupParam.GroupName, out var group))
                throw new InvalidOperationException($"Undefined group: '{groupParam.GroupName}'");

            for (int loop = 0; loop < groupParam.LoopCount && !cancellationToken.IsCancellationRequested; loop++)
            {
                foreach (var groupCmd in group.Commands)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExecuteCommandAsync(groupCmd, groups, cancellationToken);
                }
            }
        }
        else if (command.Parameter is DelayParameter delay)
        {
            await Task.Delay(delay.Milliseconds, cancellationToken);
        }
        else
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private string GetPreviousCommands(List<ParsedCommand> commands, int currentIndex)
    {
        var prev = commands.Take(Math.Min(currentIndex, 2)).Select(c => c.Type.ToString()).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(prev);
    }

    private string GetCurrentCommand(ParsedCommand command)
    {
        return System.Text.Json.JsonSerializer.Serialize(new { Type = command.Type.ToString(), Parameter = command.Parameter });
    }

    private string GetNextCommands(List<ParsedCommand> commands, int currentIndex)
    {
        var next = commands.Skip(currentIndex + 1).Take(2).Select(c => c.Type.ToString()).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(next);
    }
}