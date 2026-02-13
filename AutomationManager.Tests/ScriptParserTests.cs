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

    [Test]
    public void ParseScript_WithGroupDefinition_ParsesGroups()
    {
        var script = @"@group(MyGroup) {
  KeyDown(A);
  Delay(100);
  KeyUp(A);
}
ExecuteGroup(MyGroup, 3);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.Count, Is.EqualTo(1));
        Assert.That(result.Groups.ContainsKey("MyGroup"), Is.True);
        Assert.That(result.Groups["MyGroup"].Commands.Count, Is.EqualTo(3));
        Assert.That(result.Commands.Count, Is.EqualTo(1));
        Assert.That(result.Commands[0].Type, Is.EqualTo(CommandType.ExecuteGroup));
    }

    [Test]
    public void ParseScript_MultipleGroups_ParsesAll()
    {
        var script = @"@group(GroupA) {
  KeyPress(A);
}
@group(GroupB) {
  MouseClick(Left);
  Delay(200);
}
ExecuteGroup(GroupA, 2);
ExecuteGroup(GroupB, 1);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.Count, Is.EqualTo(2));
        Assert.That(result.Groups["GroupA"].Commands.Count, Is.EqualTo(1));
        Assert.That(result.Groups["GroupB"].Commands.Count, Is.EqualTo(2));
        Assert.That(result.Commands.Count, Is.EqualTo(2));
    }

    [Test]
    public void ParseScript_GroupWithExistingCommands_ParsesBoth()
    {
        var script = @"Delay(100);
@group(Click) {
  MouseClick(Left);
}
KeyPress(B);
ExecuteGroup(Click, 5);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.Count, Is.EqualTo(1));
        Assert.That(result.Commands.Count, Is.EqualTo(3));
        Assert.That(result.Commands[0].Type, Is.EqualTo(CommandType.Delay));
        Assert.That(result.Commands[1].Type, Is.EqualTo(CommandType.KeyPress));
        Assert.That(result.Commands[2].Type, Is.EqualTo(CommandType.ExecuteGroup));
    }

    [Test]
    public void ParseScript_DuplicateGroupName_Throws()
    {
        var script = @"@group(Dup) {
  Delay(100);
}
@group(Dup) {
  Delay(200);
}";

        Assert.Throws<InvalidOperationException>(() => _parser.ParseScript(script));
    }

    [Test]
    public void ParseScript_UnclosedGroup_Throws()
    {
        var script = @"@group(Open) {
  Delay(100);";

        Assert.Throws<InvalidOperationException>(() => _parser.ParseScript(script));
    }

    [Test]
    public void ParseScript_ExecuteGroupInsideGroup_Throws()
    {
        var script = @"@group(Inner) {
  Delay(100);
}
@group(Outer) {
  ExecuteGroup(Inner, 1);
}";

        Assert.Throws<InvalidOperationException>(() => _parser.ParseScript(script));
    }

    [Test]
    public void ParseScript_EmptyGroupName_Throws()
    {
        var script = @"@group() {
  Delay(100);
}";

        Assert.Throws<InvalidOperationException>(() => _parser.ParseScript(script));
    }

    [Test]
    public void ParseScript_ExecuteGroupParameter_ParsesCorrectly()
    {
        var script = "ExecuteGroup(TestGroup, 10);";
        var result = _parser.ParseScript(script);

        Assert.That(result.Commands.Count, Is.EqualTo(1));
        var param = result.Commands[0].Parameter as ExecuteGroupParameter;
        Assert.That(param, Is.Not.Null);
        Assert.That(param!.GroupName, Is.EqualTo("TestGroup"));
        Assert.That(param.LoopCount, Is.EqualTo(10));
    }

    [Test]
    public void ParseScript_GroupIsCaseInsensitive()
    {
        var script = @"@group(MyGroup) {
  Delay(100);
}
ExecuteGroup(mygroup, 1);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.ContainsKey("mygroup"), Is.True);
        Assert.That(result.Groups.ContainsKey("MyGroup"), Is.True);
    }

    [Test]
    public void ParseScript_CommentsInsideGroup_AreIgnored()
    {
        var script = @"@group(G1) {
  // This is a comment
  Delay(100);
  // Another comment
  KeyPress(A);
}
ExecuteGroup(G1, 1);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups["G1"].Commands.Count, Is.EqualTo(2));
    }

    [Test]
    public void ParseScript_NoGroupsLegacyScript_Works()
    {
        var script = "KeyDown(A);\nDelay(500);\nKeyUp(A);";
        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.Count, Is.EqualTo(0));
        Assert.That(result.Commands.Count, Is.EqualTo(3));
    }

    [Test]
    public void ParseScript_GroupClosedWithSemicolon_Works()
    {
        var script = @"@group(MyGroup1) {
  Delay(1001);
  Delay(1002);
  Delay(1003);
  Delay(1004);
  Delay(1005);
};

ExecuteGroup(MyGroup1, 3);";

        var result = _parser.ParseScript(script);

        Assert.That(result.Groups.Count, Is.EqualTo(1));
        Assert.That(result.Groups.ContainsKey("MyGroup1"), Is.True);
        Assert.That(result.Groups["MyGroup1"].Commands.Count, Is.EqualTo(5));
        Assert.That(result.Commands.Count, Is.EqualTo(1));
        Assert.That(result.Commands[0].Type, Is.EqualTo(CommandType.ExecuteGroup));
        var param = result.Commands[0].Parameter as ExecuteGroupParameter;
        Assert.That(param!.GroupName, Is.EqualTo("MyGroup1"));
        Assert.That(param.LoopCount, Is.EqualTo(3));
    }
}