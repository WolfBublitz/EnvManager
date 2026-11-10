// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: StartupConfigurationMigrationTests.cs                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnvManager.Tests;

public sealed class StartupConfigurationMigrationTests
{
    private const string CurrentSchemaReference =
        "# yaml-language-server: $schema=https://github.com/WolfBublitz/EnvManager/releases/latest/download/config.schema.1.0.0.yaml";
    private const string LegacyConfiguration =
        "# yaml-language-server: $schema=https://example.com/config.schema.0.9.0.yaml\n" +
        "name: main\n" +
        "shells: []\n" +
        "variables:\n" +
        "  EXISTING: preserved\n" +
        "settings:\n" +
        "  editor: vim\n" +
        "tools: []\n";

    [Test]
    public async Task StartupMigratesLegacySchemaReferenceToBundledSchemaAndCommitsConfiguration()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await InitializeHomeAsync(home);
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            await WriteAndCommitLegacyConfigurationAsync(home, configPath);
            string previousHead = await GetHeadAsync(home);

            (int exitCode, _) = await RunCliAsync(home, "settings", "list");

            await Assert.That(exitCode).IsEqualTo(0);

            string migratedYaml = File.ReadAllText(configPath);
            await Assert.That(migratedYaml.Split('\n')[0]).IsEqualTo(CurrentSchemaReference);
            ConfigurationDocument migrated = Deserialize(migratedYaml);
            await Assert.That(migrated.Name).IsEqualTo("main");
            await Assert.That(migrated.FormatVersion).IsEqualTo("1.0");
            await Assert.That(migrated.Variables["EXISTING"]).IsEqualTo("preserved");
            await Assert.That(migrated.Settings["editor"]).IsEqualTo("vim");

            string migratedHead = await GetHeadAsync(home);
            await Assert.That(migratedHead).IsNotEqualTo(previousHead);
            await Assert.That(await GetCommittedConfigurationHashAsync(home))
                .IsEqualTo(await GetWorkingConfigurationHashAsync(home));
            await Assert.That(await GetConfigurationStatusAsync(home)).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task SecondStartupDoesNotCreateAnotherMigrationCommit()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await InitializeHomeAsync(home);
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            await WriteAndCommitLegacyConfigurationAsync(home, configPath);

            (int firstExitCode, _) = await RunCliAsync(home, "settings", "list");
            await Assert.That(firstExitCode).IsEqualTo(0);
            string firstMigratedHead = await GetHeadAsync(home);
            string firstMigratedConfiguration = File.ReadAllText(configPath);

            (int secondExitCode, _) = await RunCliAsync(home, "settings", "list");

            await Assert.That(secondExitCode).IsEqualTo(0);
            await Assert.That(await GetHeadAsync(home)).IsEqualTo(firstMigratedHead);
            await Assert.That(File.ReadAllText(configPath)).IsEqualTo(firstMigratedConfiguration);
            await Assert.That(await GetConfigurationStatusAsync(home)).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task RestoringUncommittedSchemaMetadataDoesNotCommitUnrelatedStagedChanges()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await InitializeHomeAsync(home);
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            string originalYaml = File.ReadAllText(configPath);
            string trackedPath = Path.Combine(home, "tracked.txt");
            await File.WriteAllTextAsync(trackedPath, "original\n");
            await RunGitSuccessAsync(home, "add", "--", "tracked.txt");
            await RunGitSuccessAsync(home, "commit", "-m", "Seed tracked file");
            string previousHead = await GetHeadAsync(home);

            await File.WriteAllTextAsync(trackedPath, "staged change\n");
            await RunGitSuccessAsync(home, "add", "--", "tracked.txt");
            await File.WriteAllTextAsync(configPath, originalYaml.Replace(CurrentSchemaReference + "\n", string.Empty, StringComparison.Ordinal));

            (int exitCode, string output) = await RunCliAsync(home, "settings", "list");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Migrated config.yml to format version 1.0.");
            await Assert.That(File.ReadAllText(configPath)).IsEqualTo(originalYaml);
            await Assert.That(await GetHeadAsync(home)).IsEqualTo(previousHead);
            await Assert.That(await GetConfigurationStatusAsync(home)).IsEqualTo(string.Empty);
            await Assert.That(await RunGitAsync(home, "diff", "--cached", "--name-only")).IsEqualTo("tracked.txt");
            await Assert.That(await RunGitAsync(home, "show", ":tracked.txt")).IsEqualTo("staged change");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task StartupRejectsUnsupportedFutureFormatWithoutChangingConfiguration()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await InitializeHomeAsync(home);
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            string unsupportedConfiguration =
                "# yaml-language-server: $schema=https://example.com/config.schema.2.0.0.yaml\n" +
                "formatVersion: 2.0\n" +
                "name: main\n" +
                "shells: []\n" +
                "variables: {}\n" +
                "settings: {}\n" +
                "tools: []\n";
            await File.WriteAllTextAsync(configPath, unsupportedConfiguration);
            await RunGitSuccessAsync(home, "add", "--", ".EnvManager/config.yml");
            await RunGitSuccessAsync(home, "commit", "-m", "Seed unsupported configuration format");
            string previousHead = await GetHeadAsync(home);

            (int exitCode, string output) = await RunCliAsync(home, "settings", "list");

            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output.ToLowerInvariant()).Contains("not supported");
            await Assert.That(File.ReadAllText(configPath)).IsEqualTo(unsupportedConfiguration);
            await Assert.That(await GetHeadAsync(home)).IsEqualTo(previousHead);
            await Assert.That(await GetConfigurationStatusAsync(home)).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task StartupAddsMissingEnvManagerAliasesForConfiguredShellsAndPreservesExistingAliases()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await InitializeHomeAsync(home);

            string[] shells = OperatingSystem.IsWindows()
                ? ["powershell", "cmd"]
                : ["bash", "zsh", "fish", "powershell"];
            string existingAliasShell = shells[0];
            string configPath = Path.Combine(home, ".EnvManager", "config.yml");
            string configuration = File.ReadAllText(configPath);
            string shellList = "shells:\n" + string.Join('\n', Array.ConvertAll(shells, shell => $"  - {shell}"));
            await File.WriteAllTextAsync(configPath, configuration.Replace("shells: []", shellList, StringComparison.Ordinal));

            foreach (string shell in shells)
            {
                string extension = GetShellAddonExtension(shell);
                string shellDirectory = Path.Combine(home, ".EnvManager", shell);
                Directory.CreateDirectory(shellDirectory);
                string existingAlias = shell == existingAliasShell ? GetAliasContent(shell) : "original profile\n";
                await File.WriteAllTextAsync(Path.Combine(shellDirectory, $"00_Profile{extension}"), existingAlias);
                await File.WriteAllTextAsync(Path.Combine(shellDirectory, $"01_EnvManager{extension}"), GetPathOnlyContent(shell));
                await RunGitSuccessAsync(home, "add", "--", $".EnvManager/{shell}/00_Profile{extension}", $".EnvManager/{shell}/01_EnvManager{extension}");
            }

            await RunGitSuccessAsync(home, "add", "--", ".EnvManager/config.yml");
            await RunGitSuccessAsync(home, "commit", "-m", "Seed configured shells without aliases");

            (int exitCode, _) = await RunCliAsync(home, "settings", "list");

            await Assert.That(exitCode).IsEqualTo(0);
            foreach (string shell in shells)
            {
                string extension = GetShellAddonExtension(shell);
                string envManagerAddon = File.ReadAllText(Path.Combine(home, ".EnvManager", shell, $"01_EnvManager{extension}"));
                string expectedAddon = shell == existingAliasShell
                    ? GetPathOnlyContent(shell)
                    : GetPathOnlyContent(shell) + GetAliasContent(shell);
                await Assert.That(envManagerAddon).IsEqualTo(expectedAddon);
            }

            string migratedHead = await GetHeadAsync(home);
            (int secondExitCode, _) = await RunCliAsync(home, "settings", "list");
            await Assert.That(secondExitCode).IsEqualTo(0);
            await Assert.That(await GetHeadAsync(home)).IsEqualTo(migratedHead);

            foreach (string shell in shells.Where(shell => shell != existingAliasShell))
            {
                string extension = GetShellAddonExtension(shell);
                await Assert.That(await RunGitAsync(home, "status", "--porcelain", "--", $".EnvManager/{shell}/01_EnvManager{extension}"))
                    .IsEqualTo(string.Empty);
            }
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-StartupMigrationTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task InitializeHomeAsync(string home)
    {
        (int exitCode, string output) = await RunCliAsync(home, "init", "--environment", "main");
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {typeof(Program).Assembly.Location} init failed: {output}");
        }
    }

    private static async Task WriteAndCommitLegacyConfigurationAsync(string home, string configPath)
    {
        await File.WriteAllTextAsync(configPath, LegacyConfiguration);
        await RunGitSuccessAsync(home, "add", "--", ".EnvManager/config.yml");
        await RunGitSuccessAsync(home, "commit", "-m", "Seed legacy configuration schema");
    }

    private static string GetShellAddonExtension(string shell)
        => shell switch
        {
            "bash" or "zsh" => ".sh",
            "fish" => ".fish",
            "powershell" => ".ps1",
            "cmd" => ".cmd",
            _ => throw new InvalidOperationException($"Unexpected shell '{shell}'."),
        };

    private static string GetPathOnlyContent(string shell)
        => shell switch
        {
            "bash" or "zsh" => "export PATH=\"$HOME/.local/bin:$PATH\"\n",
            "fish" => "set -gx PATH \"$HOME/.local/bin\" $PATH\n",
            "powershell" => "$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n",
            "cmd" => "set PATH=%USERPROFILE%\\.local\\bin;%PATH%\n",
            _ => throw new InvalidOperationException($"Unexpected shell '{shell}'."),
        };

    private static string GetAliasContent(string shell)
        => shell switch
        {
            "bash" or "zsh" => "alias em='EnvManager'\n",
            "fish" => "alias em EnvManager\n",
            "powershell" => "Set-Alias -Name em -Value EnvManager\n",
            "cmd" => "doskey em=EnvManager $*\n",
            _ => throw new InvalidOperationException($"Unexpected shell '{shell}'."),
        };

    private static ConfigurationDocument Deserialize(string yaml)
        => new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ConfigurationDocument>(yaml)
            ?? throw new InvalidOperationException("The migrated configuration could not be deserialized.");

    private static Task<(int ExitCode, string Output)> RunCliAsync(string home, params string[] arguments)
    {
        string[] cliArguments = [typeof(Program).Assembly.Location, .. arguments];
        return RunProcessAsync("dotnet", home, home, true, cliArguments);
    }

    private static Task<string> RunGitSuccessAsync(string home, params string[] arguments)
        => RunGitAsync(home, arguments);

    private static async Task<string> RunGitAsync(string home, params string[] arguments)
    {
        string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
        string[] repositoryArguments =
        [
            $"--git-dir={gitDirectory}",
            $"--work-tree={home}",
            .. arguments,
        ];
        (int exitCode, string output) = await RunProcessAsync("git", home, home, true, repositoryArguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', repositoryArguments)} failed: {output}");
        }

        return output.TrimEnd();
    }

    private static async Task<string> GetHeadAsync(string home)
        => await RunGitAsync(home, "rev-parse", "HEAD");

    private static async Task<string> GetCommittedConfigurationHashAsync(string home)
        => await RunGitAsync(home, "rev-parse", "HEAD:.EnvManager/config.yml");

    private static async Task<string> GetWorkingConfigurationHashAsync(string home)
        => await RunGitAsync(home, "hash-object", ".EnvManager/config.yml");

    private static async Task<string> GetConfigurationStatusAsync(string home)
        => await RunGitAsync(home, "status", "--porcelain", "--", ".EnvManager/config.yml");

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string executable,
        string workingDirectory,
        string home,
        bool includeGitIdentity,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ENVMANAGER_HOME"] = home;
        if (includeGitIdentity)
        {
            startInfo.Environment["GIT_AUTHOR_NAME"] = "Test User";
            startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
            startInfo.Environment["GIT_COMMITTER_NAME"] = "Test User";
            startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executable}.");
        await process.StandardInput.WriteAsync(string.Empty);
        process.StandardInput.Close();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await standardOutput + await standardError);
    }

    private sealed class ConfigurationDocument
    {
        public string FormatVersion { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Dictionary<string, string> Variables { get; set; } = [];

        public Dictionary<string, string> Settings { get; set; } = [];
    }
}
