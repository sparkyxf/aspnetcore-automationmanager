using AutomationManager.Contracts;
using AutomationManager.Domain.Services;
using AutomationManager.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace AutomationManager.Agent.Services;

public interface IScriptExecutionService
{
    event EventHandler<ExecutionStatusChangedEventArgs>? ExecutionStatusChanged;
    event EventHandler<CommandExecutionReportEventArgs>? OnCommandExecutionReport;
    ScriptExecutionStatus Status { get; }
    int? CurrentCommandIndex { get; }
    int? TotalCommands { get; }
    string? ErrorMessage { get; }
    int CurrentLoop { get; }
    int TotalLoops { get; }
    ExecutionMode ExecutionMode { get; }
    
    Task RunScriptAsync(string scriptText, ExecutionMode mode = ExecutionMode.RunOnce, int? loopCount = null);
    void PauseExecution();
    void ResumeExecution();
    void StopExecution();
}

public class ScriptExecutionService : IScriptExecutionService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    private readonly ScriptParser _scriptParser;
    private readonly ScriptValidator _scriptValidator;
    private readonly ILogger<ScriptExecutionService> _logger;
    private readonly object _executionLock = new();

    // Execution state
    private ScriptExecutionStatus _status = ScriptExecutionStatus.Idle;
    private List<ParsedCommand>? _currentScript;
    private Dictionary<string, CommandGroup>? _currentGroups;
    private int _currentCommandIndex = 0;
    private ExecutionMode _executionMode = ExecutionMode.RunOnce;
    private int _loopCount = 1;
    private int _currentLoop = 0;
    private string? _errorMessage;
    private CancellationTokenSource? _executionCancellation;
    private readonly List<string> _executedCommands = new(); // LIFO stack for executed commands
    private string? _currentExecutingCommand; // Currently executing command

    public event EventHandler<ExecutionStatusChangedEventArgs>? ExecutionStatusChanged;
    public event EventHandler<CommandExecutionReportEventArgs>? OnCommandExecutionReport;

    public ScriptExecutionStatus Status
    {
        get { lock (_executionLock) { return _status; } }
    }

    public int? CurrentCommandIndex
    {
        get { lock (_executionLock) { return _currentScript?.Count > 0 ? _currentCommandIndex : null; } }
    }

    public int? TotalCommands
    {
        get { lock (_executionLock) { return _currentScript?.Count; } }
    }

    public string? ErrorMessage
    {
        get { lock (_executionLock) { return _errorMessage; } }
    }

    public int CurrentLoop
    {
        get { lock (_executionLock) { return _currentLoop; } }
    }

    public int TotalLoops
    {
        get { lock (_executionLock) { return _loopCount; } }
    }

    public ExecutionMode ExecutionMode
    {
        get { lock (_executionLock) { return _executionMode; } }
    }

    public ScriptExecutionService(ScriptParser scriptParser, ScriptValidator scriptValidator, ILogger<ScriptExecutionService> logger)
    {
        _scriptParser = scriptParser;
        _scriptValidator = scriptValidator;
        _logger = logger;
    }

    public async Task RunScriptAsync(string scriptText, ExecutionMode mode = ExecutionMode.RunOnce, int? loopCount = null)
    {
        lock (_executionLock)
        {
            if (_status == ScriptExecutionStatus.Running)
            {
                _logger.LogWarning("Script execution already running");
                return;
            }

            if (string.IsNullOrEmpty(scriptText))
            {
                _logger.LogError("Cannot start script execution: Script text is empty");
                SetErrorState("Script text is empty");
                return;
            }

            try
            {
                // Validate script before parsing
                var validationResult = _scriptValidator.Validate(scriptText);
                if (!validationResult.IsValid)
                {
                    var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                    _logger.LogError("Script validation failed: {Errors}", errorMessages);
                    SetErrorState($"Script validation failed: {errorMessages}");
                    return;
                }

                // Parse the full script including group definitions
                var parsed = _scriptParser.ParseScript(scriptText);
                _currentScript = parsed.Commands;
                _currentGroups = parsed.Groups;
                _currentCommandIndex = 0;
                _executionMode = mode;
                _loopCount = loopCount ?? 1;
                _currentLoop = 0;
                _errorMessage = null;
                _executedCommands.Clear();
                _currentExecutingCommand = null;
                
                // Cancel any existing execution
                _executionCancellation?.Cancel();
                _executionCancellation = new CancellationTokenSource();
                
                SetStatus(ScriptExecutionStatus.Running);
                _logger.LogInformation("Started script execution: Mode={Mode}, LoopCount={LoopCount}, Commands={CommandCount}, Groups={GroupCount}", 
                    _executionMode, _loopCount, _currentScript.Count, _currentGroups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing script");
                SetErrorState($"Script parsing error: {ex.Message}");
                return;
            }
        }

        // Start execution in background
        _ = Task.Run(() => ExecuteScriptAsync(_executionCancellation.Token), _executionCancellation.Token);
    }

    public void PauseExecution()
    {
        lock (_executionLock)
        {
            if (_status == ScriptExecutionStatus.Running)
            {
                SetStatus(ScriptExecutionStatus.Paused);
                _logger.LogInformation("Script execution paused");
            }
        }
    }

    public void ResumeExecution()
    {
        lock (_executionLock)
        {
            if (_status == ScriptExecutionStatus.Paused)
            {
                SetStatus(ScriptExecutionStatus.Running);
                _logger.LogInformation("Script execution resumed");
            }
        }
    }

    public void StopExecution()
    {
        lock (_executionLock)
        {
            _executionCancellation?.Cancel();
            SetStatus(ScriptExecutionStatus.Stopped);
            _currentScript = null;
            _currentGroups = null;
            _currentCommandIndex = 0;
            _errorMessage = null;
            _executedCommands.Clear();
            _currentExecutingCommand = null;
            _logger.LogInformation("Script execution stopped");
        }
    }

    private void SetStatus(ScriptExecutionStatus status)
    {
        _status = status;
        ExecutionStatusChanged?.Invoke(this, new ExecutionStatusChangedEventArgs(status));
    }

    private void SetErrorState(string errorMessage)
    {
        _errorMessage = errorMessage;
        SetStatus(ScriptExecutionStatus.Error);
    }

    private async Task ExecuteScriptAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ExecuteScriptAsync started: Mode={Mode}, LoopCount={LoopCount}", 
                _executionMode, _loopCount);

            bool shouldContinue = true;
            while (shouldContinue && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting loop iteration {CurrentLoop} (Mode={Mode}, LoopCount={LoopCount})", 
                    _currentLoop + 1, _executionMode, _loopCount);

                await ExecuteSingleRunAsync(cancellationToken);
                
                lock (_executionLock)
                {
                    _currentLoop++;

                    _logger.LogInformation("Completed loop iteration {CurrentLoop} (Mode={Mode}, LoopCount={LoopCount}, Status={Status})", 
                        _currentLoop, _executionMode, _loopCount, _status);
                    
                    // If execution was stopped/paused/errored externally, break
                    if (_status != ScriptExecutionStatus.Running)
                    {
                        _logger.LogInformation("Execution status changed to {Status}, stopping loop", _status);
                        shouldContinue = false;
                        break;
                    }

                    switch (_executionMode)
                    {
                        case ExecutionMode.RunOnce:
                            _logger.LogInformation("RunOnce mode completed");
                            SetStatus(ScriptExecutionStatus.Completed);
                            shouldContinue = false;
                            break;

                        case ExecutionMode.LoopXTimes:
                            if (_currentLoop >= _loopCount)
                            {
                                _logger.LogInformation("LoopXTimes completed: {CurrentLoop}/{LoopCount}", _currentLoop, _loopCount);
                                SetStatus(ScriptExecutionStatus.Completed);
                                shouldContinue = false;
                            }
                            else
                            {
                                _logger.LogInformation("LoopXTimes continuing: {CurrentLoop}/{LoopCount}", _currentLoop, _loopCount);
                                _currentCommandIndex = 0;
                                _executedCommands.Clear();
                                _currentExecutingCommand = null;
                            }
                            break;

                        case ExecutionMode.LoopUntilStopped:
                            _logger.LogInformation("LoopUntilStopped iteration {CurrentLoop} completed, continuing...", _currentLoop);
                            _currentCommandIndex = 0;
                            _executedCommands.Clear();
                            _currentExecutingCommand = null;
                            // shouldContinue remains true — loop indefinitely until stopped
                            break;
                    }
                }
            }

            _logger.LogInformation("ExecuteScriptAsync ended: Mode={Mode}, Loops completed={CurrentLoop}, Status={Status}", 
                _executionMode, _currentLoop, _status);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Script execution cancelled");
            lock (_executionLock)
            {
                SetStatus(ScriptExecutionStatus.Stopped);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during script execution");
            lock (_executionLock)
            {
                SetErrorState($"Execution error: {ex.Message}");
            }
        }
    }

    private async Task ExecuteSingleRunAsync(CancellationToken cancellationToken)
    {
        if (_currentScript == null) return;

        for (int i = _currentCommandIndex; i < _currentScript.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Wait if paused
            while (true)
            {
                lock (_executionLock)
                {
                    if (_status != ScriptExecutionStatus.Paused)
                        break;
                }
                await Task.Delay(100, cancellationToken);
            }

            lock (_executionLock)
            {
                if (_status != ScriptExecutionStatus.Running)
                    return;
                    
                _currentCommandIndex = i;
                
                // Set current executing command and notify before execution
                var cmd = _currentScript[i];
                _currentExecutingCommand = $"{cmd.Type}({FormatParameter(cmd.Parameter)})";
            }

            // Notify before command execution
            var previousCommands = _executedCommands.Take(3).ToList(); // Get top 3 executed commands
            var nextCommands = _currentScript.Skip(_currentCommandIndex + 1).Take(3).Select(cmd => $"{cmd.Type}({FormatParameter(cmd.Parameter)})").ToList();
            OnCommandExecutionReport?.Invoke(this, new CommandExecutionReportEventArgs(previousCommands, _currentExecutingCommand ?? string.Empty, nextCommands));

            var command = _currentScript[i];
            await ExecuteCommandAsync(command, cancellationToken);

            // Move executed command to previous commands and clear current
            lock (_executionLock)
            {
                if (_currentExecutingCommand != null)
                {
                    _executedCommands.Insert(0, _currentExecutingCommand); // Add to top (LIFO)
                    _currentExecutingCommand = null; // Clear current command
                    
                    nextCommands = _currentScript.Skip(_currentCommandIndex + 2).Take(3).Select(cmd => $"{cmd.Type}({FormatParameter(cmd.Parameter)})").ToList();

                    if (!nextCommands.Any())
                    {
                        previousCommands = _executedCommands.Take(3).ToList(); // Get top 3 executed commands
                        OnCommandExecutionReport?.Invoke(this, new CommandExecutionReportEventArgs(previousCommands, string.Empty, new List<string>()));
                    }
                }
            }
        }
    }

    private async Task ExecuteCommandAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Executing command: {Type}({Parameter})", command.Type, FormatParameter(command.Parameter));
            
            switch (command.Type)
            {
                case CommandType.Delay:
                    if (command.Parameter is DelayParameter delay)
                        await Task.Delay(delay.Milliseconds, cancellationToken);
                    break;
                    
                case CommandType.KeyPress:
                case CommandType.KeyDown:
                case CommandType.KeyUp:
                    await ExecuteKeyCommandAsync(command, cancellationToken);
                    break;
                    
                case CommandType.MouseClick:
                case CommandType.MouseDown:
                case CommandType.MouseUp:
                case CommandType.MouseMove:
                    await ExecuteMouseCommandAsync(command, cancellationToken);
                    break;

                case CommandType.ExecuteGroup:
                    await ExecuteGroupCommandAsync(command, cancellationToken);
                    break;
                    
                default:
                    _logger.LogWarning("Unsupported command type: {Type}", command.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Type}({Parameter})", command.Type, FormatParameter(command.Parameter));
            throw;
        }
    }

    private async Task ExecuteGroupCommandAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameter is not ExecuteGroupParameter groupParam) return;
        if (_currentGroups == null || !_currentGroups.TryGetValue(groupParam.GroupName, out var group))
        {
            throw new InvalidOperationException($"Undefined group: '{groupParam.GroupName}'");
        }

        _logger.LogInformation("Executing group '{GroupName}' x{LoopCount}", groupParam.GroupName, groupParam.LoopCount);

        for (int loop = 0; loop < groupParam.LoopCount && !cancellationToken.IsCancellationRequested; loop++)
        {
            for (int j = 0; j < group.Commands.Count; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Wait if paused
                while (true)
                {
                    lock (_executionLock)
                    {
                        if (_status != ScriptExecutionStatus.Paused)
                            break;
                    }
                    await Task.Delay(100, cancellationToken);
                }

                lock (_executionLock)
                {
                    if (_status != ScriptExecutionStatus.Running)
                        return;
                }

                var groupCmd = group.Commands[j];
                await ExecuteCommandAsync(groupCmd, cancellationToken);
            }
        }

        _logger.LogInformation("Completed group '{GroupName}'", groupParam.GroupName);
    }

    private async Task ExecuteKeyCommandAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameter is not KeyParameter keyParam) return;

        byte vkCode = (byte)keyParam.KeyCode;
        
        switch (command.Type)
        {
            case CommandType.KeyDown:
                keybd_event(vkCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                break;
            case CommandType.KeyUp:
                keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                break;
            case CommandType.KeyPress:
                keybd_event(vkCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                await Task.Delay(50, cancellationToken);
                keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                break;
        }
    }

    private async Task ExecuteMouseCommandAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        switch (command.Type)
        {
            case CommandType.MouseMove:
                if (command.Parameter is MouseMoveParameter moveParam)
                    SetCursorPos(moveParam.X, moveParam.Y);
                break;
                
            case CommandType.MouseDown:
            case CommandType.MouseUp:
            case CommandType.MouseClick:
                if (command.Parameter is MouseButtonParameter buttonParam)
                    await ExecuteMouseButtonAsync(command.Type, buttonParam.Button, cancellationToken);
                break;
        }
    }

    private async Task ExecuteMouseButtonAsync(CommandType commandType, MouseButton button, CancellationToken cancellationToken)
    {
        uint downFlag = button switch
        {
            MouseButton.Left => MOUSEEVENTF_LEFTDOWN,
            MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
            MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
            _ => MOUSEEVENTF_LEFTDOWN
        };

        uint upFlag = button switch
        {
            MouseButton.Left => MOUSEEVENTF_LEFTUP,
            MouseButton.Right => MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
            _ => MOUSEEVENTF_LEFTUP
        };

        switch (commandType)
        {
            case CommandType.MouseDown:
                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                break;
            case CommandType.MouseUp:
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                break;
            case CommandType.MouseClick:
                mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(50, cancellationToken);
                mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                break;
        }
    }

    private string FormatParameter(CommandParameter parameter)
    {
        return parameter switch
        {
            KeyParameter key => key.KeyCode.ToString(),
            MouseButtonParameter mouse => mouse.Button.ToString(),
            MouseMoveParameter move => $"{move.X},{move.Y}",
            DelayParameter delay => delay.Milliseconds.ToString(),
            ExecuteGroupParameter group => $"{group.GroupName},{group.LoopCount}",
            _ => "Unknown"
        };
    }
}

public class ExecutionStatusChangedEventArgs : EventArgs
{
    public ScriptExecutionStatus Status { get; }

    public ExecutionStatusChangedEventArgs(ScriptExecutionStatus status)
    {
        Status = status;
    }
}

public class CommandExecutionReportEventArgs : EventArgs
{
    public List<string> PreviousCommands { get; }
    public List<string> NextCommands { get; }
    public string CurrentCommand { get; }

    public CommandExecutionReportEventArgs(List<string> previousCommands, string currentCommand, List<string> nextCommands)
    {
        PreviousCommands = previousCommands;
        CurrentCommand = currentCommand;
        NextCommands = nextCommands;
    }
}