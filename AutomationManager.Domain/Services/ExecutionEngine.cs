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
        var commands = _parser.Parse(execution.ScriptTemplate.ScriptText).ToList();
        var index = 0;

        while (index < commands.Count && !cancellationToken.IsCancellationRequested)
        {
            var command = commands[index];
            // Simulate execution
            await ExecuteCommandAsync(command, cancellationToken);

            // Update progress
            execution.PreviousCommands = GetPreviousCommands(commands, index);
            execution.CurrentCommand = GetCurrentCommand(command);
            execution.NextCommands = GetNextCommands(commands, index);

            index++;
        }

        execution.Status = cancellationToken.IsCancellationRequested ? ExecutionStatus.Canceled : ExecutionStatus.Completed;
    }

    private async Task ExecuteCommandAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        // In real implementation, this would call Windows API
        // For domain, just simulate delay
        if (command.Parameter is DelayParameter delay)
        {
            await Task.Delay(delay.Milliseconds, cancellationToken);
        }
        else
        {
            await Task.Delay(100, cancellationToken); // simulate
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