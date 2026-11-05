// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: MissingPackageManagerTests.cs                                            │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.PackageManagers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class MissingPackageManagerTests
{
    [Test]
    public async Task MissingExecutableProducesFriendlyPackageManagerException()
    {
        MissingManager manager = new();

        PackageManagerException exception = await Assert.That(() => manager.UpdateAsync("htop")).Throws<PackageManagerException>();

        await Assert.That(exception.Message).Contains("not installed or not available on PATH");
    }

    private sealed class MissingManager : PackageManagerBase
    {
        public override string Name => "missing";

        protected override string Executable => "envmanager-definitely-missing-executable";

        protected override string[] BuildInstallArguments(string toolName, string? version) => [toolName];

        protected override string[] BuildRemoveArguments(string toolName) => [toolName];

        protected override string[] BuildUpdateArguments(string toolName) => [toolName];
    }
}
