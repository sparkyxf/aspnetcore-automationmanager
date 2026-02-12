using AutomationManager.Domain.Entities;
using AutomationManager.Domain.Services;
using AutomationManager.Domain.Models;
using NUnit.Framework;
using Moq;

namespace AutomationManager.Tests;

[TestFixture]
public class ExecutionEngineTests
{
    private ExecutionEngine _engine = null!;
    private ScriptParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ScriptParser();
        _engine = new ExecutionEngine(_parser);
    }

    [Test]
    public async Task ExecuteAsync_ValidScript_Completes()
    {
        var template = new ScriptTemplate { ScriptText = "Delay(100);" };
        var execution = new ScriptExecution { ScriptTemplate = template, Status = ExecutionStatus.Pending };

        await _engine.ExecuteAsync(execution);

        Assert.That(execution.Status, Is.EqualTo(ExecutionStatus.Completed));
    }
}