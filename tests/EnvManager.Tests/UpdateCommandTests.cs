// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: UpdateCommandTests.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using EnvManager.CommandLine;
using EnvManager.Exceptions;
using EnvManager.Updates;
using Semver;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class UpdateCommandTests
{
    [Test]
    public async Task SelectReleaseAutomaticallySelectsTheOnlyAvailableUpdate()
    {
        UpdateRelease release = CreateRelease("2.0.0");

        UpdateRelease selected = UpdateCommand.SelectRelease([release], requestedVersion: null, interactive: false, _ => throw new InvalidOperationException());

        await Assert.That(selected).IsEqualTo(release);
    }

    [Test]
    public async Task SelectReleaseUsesTheRequestedVersionWhenSeveralAreAvailable()
    {
        UpdateRelease oldest = CreateRelease("2.0.0");
        UpdateRelease newest = CreateRelease("3.0.0");

        UpdateRelease selected = UpdateCommand.SelectRelease(
            [newest, oldest],
            requestedVersion: "v2.0.0",
            interactive: false,
            _ => throw new InvalidOperationException());

        await Assert.That(selected).IsEqualTo(oldest);
    }

    [Test]
    public async Task SelectReleasePromptsForMultipleVersionsWhenInteractive()
    {
        UpdateRelease older = CreateRelease("2.0.0");
        UpdateRelease newer = CreateRelease("3.0.0");
        bool promptInvoked = false;

        UpdateRelease selected = UpdateCommand.SelectRelease(
            [older, newer],
            requestedVersion: null,
            interactive: true,
            releases =>
            {
                promptInvoked = true;
                return releases[1];
            });

        await Assert.That(promptInvoked).IsTrue();
        await Assert.That(selected).IsEqualTo(newer);
    }

    [Test]
    public async Task SelectReleaseRequiresVersionWhenMultipleUpdatesAreNonInteractive()
    {
        await Assert.That(() => UpdateCommand.SelectRelease(
            [CreateRelease("2.0.0"), CreateRelease("3.0.0")],
            requestedVersion: null,
            interactive: false,
            _ => throw new InvalidOperationException())).Throws<ConfigurationException>();
    }

    [Test]
    public async Task SelectReleaseRejectsUnavailableAndInvalidRequestedVersions()
    {
        IReadOnlyList<UpdateRelease> releases = [CreateRelease("2.0.0")];

        await Assert.That(() => UpdateCommand.SelectRelease(releases, "3.0.0", false, _ => throw new InvalidOperationException()))
            .Throws<ConfigurationException>();
        await Assert.That(() => UpdateCommand.SelectRelease(releases, "latest", false, _ => throw new InvalidOperationException()))
            .Throws<ConfigurationException>();
    }

    [Test]
    public async Task UpdatePlatformMapsSupportedAssetsAndRejectsUnsupportedTargets()
    {
        await Assert.That(UpdatePlatform.GetAssetName(true, false, false, Architecture.X64))
            .IsEqualTo("EnvManager-windows-x64.exe");
        await Assert.That(UpdatePlatform.GetAssetName(false, true, false, Architecture.Arm64))
            .IsEqualTo("EnvManager-linux-arm64");
        await Assert.That(UpdatePlatform.GetAssetName(false, false, true, Architecture.X64))
            .IsEqualTo("EnvManager-macos-x64");
        await Assert.That(() => UpdatePlatform.GetAssetName(true, false, false, Architecture.Arm64))
            .Throws<ConfigurationException>();
        await Assert.That(() => UpdatePlatform.GetAssetName(false, false, false, Architecture.X64))
            .Throws<ConfigurationException>();
    }

    [Test]
    public async Task UpdatePlatformRecognizesInstalledAndDirectlyDownloadedAssetNames()
    {
        await Assert.That(UpdatePlatform.IsSupportedExecutableName("EnvManager", "EnvManager-linux-x64", false)).IsTrue();
        await Assert.That(UpdatePlatform.IsSupportedExecutableName("EnvManager-linux-x64", "EnvManager-linux-x64", false)).IsTrue();
        await Assert.That(UpdatePlatform.IsSupportedExecutableName("EnvManager-windows-x64.exe", "EnvManager-windows-x64.exe", true)).IsTrue();
        await Assert.That(UpdatePlatform.IsSupportedExecutableName("dotnet", "EnvManager-linux-x64", false)).IsFalse();
    }

    private static UpdateRelease CreateRelease(string version)
        => new(SemVersion.Parse(version), version, new Uri($"https://github.com/WolfBublitz/EnvManager/releases/download/v{version}/asset"), new string('a', 64));
}
