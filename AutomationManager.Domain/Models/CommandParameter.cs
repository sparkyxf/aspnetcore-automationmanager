namespace AutomationManager.Domain.Models;

public abstract record CommandParameter;

public record KeyParameter(char Key) : CommandParameter;

public record MouseButtonParameter(MouseButton Button) : CommandParameter;

public record MouseMoveParameter(int X, int Y) : CommandParameter;

public record DelayParameter(int Milliseconds) : CommandParameter;

public record ExecuteGroupParameter(string GroupName, int LoopCount) : CommandParameter;

public enum MouseButton
{
    Left,
    Right,
    Middle
}