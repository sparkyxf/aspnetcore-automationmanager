using AutomationManager.Domain.Services;
using AutomationManager.Domain.Models;
using NUnit.Framework;

namespace AutomationManager.Tests;

[TestFixture]
public class ScriptParserTests
{
    private ScriptParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ScriptParser();
    }

    [Test]
    public void Parse_ValidScript_ReturnsCommands()
    {
        var script = "KeyDown(A);\nDelay(1000);\nKeyUp(A);";
        var commands = _parser.Parse(script).ToList();

        Assert.That(commands.Count, Is.EqualTo(3));
        Assert.That(commands[0].Type, Is.EqualTo(CommandType.KeyDown));
        Assert.That(commands[1].Type, Is.EqualTo(CommandType.Delay));
        Assert.That(commands[2].Type, Is.EqualTo(CommandType.KeyUp));
    }

    [Test]
    public void Parse_InvalidCommand_ThrowsException()
    {
        var script = "InvalidCommand();";
        Assert.Throws<InvalidOperationException>(() => _parser.Parse(script).ToList());
    }
}