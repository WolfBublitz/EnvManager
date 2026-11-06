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
    public async Task EditPromptsForSupportedKeyAndPersistsValue()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            string settingValue = "Visual Studio Code";

            await InitializeHomeAsync(home);
            await RunSuccessWithInputAsync(
                home,
                "editor\n" + settingValue + "\n",
                "dotnet",
                typeof(Program).Assembly.Location,
                "settings",
                "edit");

            string yamlOnDisk = File.ReadAllText(configPath);

            await AssertConfigurationAsync(yamlOnDisk, settingValue);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task EditRejectsUnsupportedKeySelection()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");

            await InitializeHomeAsync(home);
            (int exitCode, string output) = await RunWithInputAsync(
                home,
                "editor.theme\n",
                "dotnet",
                typeof(Program).Assembly.Location,
                "settings",
                "edit");

            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("editor");
            await AssertConfigurationAsync(File.ReadAllText(configPath), null);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ListShowsConfiguredSettingKeysAndValues()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            const string settingValue = "Visual Studio Code";

            await InitializeHomeAsync(home);
            await RunSuccessWithInputAsync(
                home,
                "editor\n" + settingValue + "\n",
                "dotnet",
                typeof(Program).Assembly.Location,
                "settings",
                "edit");

            string output = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "settings", "list");

            await Assert.That(output).Contains("editor");
            await Assert.That(output).Contains(settingValue);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task RemoveIsNotAvailableAndDoesNotChangeSettings()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");

            await InitializeHomeAsync(home);
            await RunSuccessWithInputAsync(
                home,
                "editor\nVisual Studio Code\n",
                "dotnet",
                typeof(Program).Assembly.Location,
                "settings",
                "edit");
            (int exitCode, string output) = await RunWithInputAsync(
                home,
                string.Empty,
                "dotnet",
                typeof(Program).Assembly.Location,
                "settings",
                "remove",
                "editor");

            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("remove");
            await AssertConfigurationAsync(File.ReadAllText(configPath), "Visual Studio Code");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static async Task AssertConfigurationAsync(string yaml, string? settingValue)
    {
        ConfigurationDocument configuration = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ConfigurationDocument>(yaml)!;

        await Assert.That(yaml.Split('\n')[0]).IsEqualTo("formatVersion: 1.0");
        await Assert.That(configuration.FormatVersion).IsEqualTo("1.0");
        await Assert.That(configuration.Settings.Count).IsEqualTo(settingValue is null ? 0 : 1);
        if (settingValue is not null)
        {
            await Assert.That(configuration.Settings.ContainsKey("editor")).IsTrue();
            await Assert.That(configuration.Settings["editor"]).IsEqualTo(settingValue);
        }

        await Assert.That(configuration.Variables["EXISTING"]).IsEqualTo("kept");
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-SettingsTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task InitializeHomeAsync(string home)
    {
        await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
        await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "variable", "set", "EXISTING", "kept");
    }

    private static async Task<string> RunSuccessAsync(string home, string executable, params string[] arguments)
    {
        (int exitCode, string output) = await RunWithInputAsync(home, string.Empty, executable, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {output}");
        }

        return output.TrimEnd();
    }

    private static async Task<string> RunSuccessWithInputAsync(
        string home,
        string standardInput,
        string executable,
        params string[] arguments)
    {
        (int exitCode, string output) = await RunWithInputAsync(home, standardInput, executable, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {output}");
        }

        return output.TrimEnd();
    }

    private static async Task<(int ExitCode, string Output)> RunWithInputAsync(
        string home,
        string standardInput,
        string executable,
        params string[] arguments)
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
        await process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }

    private sealed class ConfigurationDocument
    {
        public string FormatVersion { get; set; } = string.Empty;

        public Dictionary<string, string> Settings { get; set; } = [];

        public Dictionary<string, string> Variables { get; set; } = [];
    }
}
