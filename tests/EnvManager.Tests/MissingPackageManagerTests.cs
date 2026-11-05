// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: MissingPackageManagerTests.cs                                            │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
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
    public async Task ProcessStartFailureIncludesOperatingSystemDiagnostic()
    {
        MissingManager manager = new();

        PackageManagerException exception = await Assert.That(() => manager.UpdateAsync("htop")).Throws<PackageManagerException>()
            ?? throw new InvalidOperationException("The update should throw a package manager exception.");

        await Assert.That(exception.Message).Contains("OS error");
        await Assert.That(exception.Message).Contains("Verify the executable");
        await Assert.That(exception.Message).DoesNotContain("not installed");
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
