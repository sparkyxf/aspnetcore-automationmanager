using AutomationManager.Domain.Services;
using AutomationManager.Domain.Models;
using NUnit.Framework;

namespace AutomationManager.Tests;

[TestFixture]
public class ScriptValidatorTests
{
    private ScriptValidator _validator = null!;
    private ScriptParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ScriptParser();
        _validator = new ScriptValidator(_parser);
    }

    [Test]
    public void Validate_EmptyScript_ReturnsInvalid()
    {
        var result = _validator.Validate("");
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors[0].Message, Does.Contain("empty"));
    }

    [Test]
    public void Validate_NullScript_ReturnsInvalid()
    {
        var result = _validator.Validate(null!);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_ValidSimpleScript_ReturnsValid()
    {
        var script = "KeyDown(A);\nDelay(100);\nKeyUp(A);";
        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Validate_ValidScriptWithGroups_ReturnsValid()
    {
        var script = @"@group(MyGroup) {
  KeyPress(A);
  Delay(100);
}
ExecuteGroup(MyGroup, 3);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_UndefinedGroupReference_ReturnsInvalid()
    {
        var script = "ExecuteGroup(NonExistent, 1);";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("undefined group")), Is.True);
    }

    [Test]
    public void Validate_NegativeDelay_ReturnsInvalid()
    {
        var script = "Delay(-100);";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("greater than 0")), Is.True);
    }

    [Test]
    public void Validate_ExcessiveDelay_ReturnsInvalid()
    {
        var script = "Delay(99999999);";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("maximum")), Is.True);
    }

    [Test]
    public void Validate_NegativeMouseCoordinates_ReturnsInvalid()
    {
        var script = "MouseMove(-10, 200);";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("non-negative")), Is.True);
    }

    [Test]
    public void Validate_ZeroLoopCount_ReturnsInvalid()
    {
        var script = @"@group(G) {
  Delay(100);
}
ExecuteGroup(G, 0);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("loop count must be greater than 0")), Is.True);
    }

    [Test]
    public void Validate_ExcessiveLoopCount_ReturnsInvalid()
    {
        var script = @"@group(G) {
  Delay(100);
}
ExecuteGroup(G, 999999);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("maximum")), Is.True);
    }

    [Test]
    public void Validate_EmptyGroup_ReturnsInvalid()
    {
        var script = @"@group(Empty) {
}
ExecuteGroup(Empty, 1);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("no commands")), Is.True);
    }

    [Test]
    public void Validate_InvalidSyntax_ReturnsInvalid()
    {
        var script = "NotACommand(blah);";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("Parse error")), Is.True);
    }

    [Test]
    public void Validate_MissingSemicolon_ReturnsInvalid()
    {
        var script = "Delay(100)";
        var result = _validator.Validate(script);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_ValidMouseCommands_ReturnsValid()
    {
        var script = "MouseMove(100, 200);\nMouseClick(Left);\nMouseDown(Right);\nMouseUp(Middle);";
        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_ValidKeyCommands_ReturnsValid()
    {
        var script = "KeyDown(A);\nKeyUp(A);\nKeyPress(B);";
        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var script = @"@group(G) {
  Delay(100);
}
ExecuteGroup(NoExist, 0);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.False);
        // Should have at least 2 errors: unknown group reference + zero loop count
        Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Validate_GroupCommandsValidated_ReturnsErrors()
    {
        var script = @"@group(BadGroup) {
  Delay(-50);
  MouseMove(-1, -2);
}
ExecuteGroup(BadGroup, 1);";

        var result = _validator.Validate(script);
        Assert.That(result.IsValid, Is.False);
        // Should flag negative delay and negative coordinates inside group
        Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(2));
    }
}
