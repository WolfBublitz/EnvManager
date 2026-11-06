using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnvManager.Tests;

public sealed class SettingsCommandTests
{
    [Test]
    public async Task SetPersistsArbitraryStringSettingsToCurrentBranchAndPreservesExistingConfiguration()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            string settingValue = "  value: # literal\nsecond line  ";

            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "variable", "set", "EXISTING", "kept");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "settings", "set", "editor.theme", "initial theme");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "settings", "set", "editor.mode", "compact");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "settings", "set", "editor.theme", settingValue);

            string currentBranch = await RunSuccessAsync(
                home,
                "git",
                $"--git-dir={gitDirectory}",
                $"--work-tree={home}",
                "branch",
                "--show-current");
            string yamlOnDisk = File.ReadAllText(configPath);
            string yamlInCurrentBranch = await RunSuccessAsync(
                home,
                "git",
                $"--git-dir={gitDirectory}",
                $"--work-tree={home}",
                "show",
                "HEAD:.EnvManager/config.yml");

            await Assert.That(currentBranch).IsEqualTo("main");
            await AssertConfigurationAsync(yamlOnDisk, settingValue);
            await AssertConfigurationAsync(yamlInCurrentBranch, settingValue);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static async Task AssertConfigurationAsync(string yaml, string settingValue)
    {
        ConfigurationDocument configuration = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ConfigurationDocument>(yaml)!;

        await Assert.That(configuration.Settings.Count).IsEqualTo(2);
        await Assert.That(configuration.Settings.ContainsKey("editor.theme")).IsTrue();
        await Assert.That(configuration.Settings["editor.theme"]).IsEqualTo(settingValue);
        await Assert.That(configuration.Settings["editor.mode"]).IsEqualTo("compact");
        await Assert.That(configuration.Variables["EXISTING"]).IsEqualTo("kept");
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-SettingsTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task<string> RunSuccessAsync(string home, string executable, params string[] arguments)
    {
        (int exitCode, string output) = await RunAsync(home, executable, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {output}");
        }

        return output.TrimEnd();
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string home, string executable, params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = home,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ENVMANAGER_HOME"] = home;
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Test User";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Test User";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }

    private sealed class ConfigurationDocument
    {
        public Dictionary<string, string> Settings { get; set; } = [];

        public Dictionary<string, string> Variables { get; set; } = [];
    }
}
