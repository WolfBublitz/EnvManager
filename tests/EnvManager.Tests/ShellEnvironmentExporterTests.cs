// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellEnvironmentExporterTests.cs                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.Shells;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ShellEnvironmentExporterTests
{
    [Test]
    public async Task SynchronizeWritesEscapedValuesForEveryConfiguredShell()
    {
        DirectoryInfo home = CreateHome();
        try
        {
            EnvironmentConfiguration configuration = new()
            {
                Shells = ["bash", "zsh", "fish", "powershell"],
                Variables = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GREETING"] = "O'Reilly",
                },
            };

            ShellEnvironmentExporter.Synchronize(configuration, home, isWindows: false);

            await Assert.That(Read(home, "bash", "00-envmanager-variables.sh")).IsEqualTo("export GREETING='O'\"'\"'Reilly'\n");
            await Assert.That(Read(home, "zsh", "00-envmanager-variables.sh")).IsEqualTo("export GREETING='O'\"'\"'Reilly'\n");
            await Assert.That(Read(home, "fish", "00-envmanager-variables.fish")).IsEqualTo("set -gx GREETING 'O\\'Reilly'\n");
            await Assert.That(Read(home, "powershell", "00-envmanager-variables.ps1")).IsEqualTo("$env:GREETING = 'O''Reilly'\n");
            configuration.Shells = ["cmd"];
            ShellEnvironmentExporter.Synchronize(configuration, home, isWindows: true);
            await Assert.That(Read(home, "cmd", "00-envmanager-variables.cmd")).IsEqualTo("@set \"GREETING=O'Reilly\"\n");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task SynchronizeRejectsInvalidVariableNamesAndUnsafeCmdValues()
    {
        DirectoryInfo home = CreateHome();
        try
        {
            EnvironmentConfiguration invalidName = new()
            {
                Shells = ["bash"],
                Variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["BAD;NAME"] = "value" },
            };

            await Assert.That(() => ShellEnvironmentExporter.Synchronize(invalidName, home, isWindows: false)).Throws<ConfigurationException>();

            EnvironmentConfiguration unsafeCmdValue = new()
            {
                Shells = ["cmd"],
                Variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["VALUE"] = "before%PATH%after" },
            };

            await Assert.That(() => ShellEnvironmentExporter.Synchronize(unsafeCmdValue, home, isWindows: true)).Throws<ConfigurationException>();
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "cmd", "00-envmanager-variables.cmd"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task SynchronizeRemovesGeneratedExportsForShellsNoLongerConfigured()
    {
        DirectoryInfo home = CreateHome();
        try
        {
            EnvironmentConfiguration configuration = new()
            {
                Shells = ["bash"],
                Variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["VALUE"] = "one" },
            };
            ShellEnvironmentExporter.Synchronize(configuration, home, isWindows: false);
            configuration.Shells.Clear();

            ShellEnvironmentExporter.Synchronize(configuration, home, isWindows: false);

            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "bash", "00-envmanager-variables.sh"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateHome()
    {
        DirectoryInfo home = new(Path.Combine(Path.GetTempPath(), $"EnvManager-ShellEnvironmentTests-{Guid.NewGuid():N}"));
        home.Create();
        return home;
    }

    private static string Read(DirectoryInfo home, string shell, string fileName)
        => File.ReadAllText(Path.Combine(home.FullName, ".EnvManager", shell, fileName));
}
