using System.CommandLine;
using System.Threading.Tasks;

using EnvManager.CommandLine;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class WorkflowCommandTests
{
    [Test]
    public async Task SyncAndAutomationOptionsAreRegistered()
    {
        EnvManagerRootCommand root = new();

        ParseResult sync = root.Parse(["sync", "--dry-run", "--yes"]);
        ParseResult pull = root.Parse(["pull", "--dry-run"]);
        ParseResult push = root.Parse(["push", "--dry-run"]);

        await Assert.That(sync.Errors.Count).IsEqualTo(0);
        await Assert.That(pull.Errors.Count).IsEqualTo(0);
        await Assert.That(push.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToolsApplyAutomationOptionsAreRegistered()
    {
        EnvManagerRootCommand root = new();

        ParseResult parseResult = root.Parse(["tools", "apply", "--dry-run", "--yes"]);

        await Assert.That(parseResult.Errors.Count).IsEqualTo(0);
    }
}
