// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesAddCommandTests.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-27                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class FilesAddCommandTests
{
    [Test]
    public async Task MultipleFilesUseCurrentDirectoryAndWarnForSensitiveNames()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string nested = Directory.CreateDirectory(Path.Combine(home, "nested")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            File.WriteAllText(Path.Combine(nested, ".env"), "example");
            File.WriteAllText(Path.Combine(nested, "settings"), "settings");

            string output = await RunFromAsync(nested, home, "dotnet", typeof(Program).Assembly.Location, "files", "add", ".env", "settings");
            await Assert.That(output).Contains("may contain secrets");
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", ":nested/settings")).IsEqualTo("settings");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", ":nested/.env")).IsEqualTo("example");

            await RunFromAsync(nested, home, "dotnet", typeof(Program).Assembly.Location, "files", "remove", "settings");
            await Assert.That(File.Exists(Path.Combine(nested, "settings"))).IsTrue();
            string staged = await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "diff", "--cached", "--name-only");
            await Assert.That(staged).Contains("nested/.env");
            await Assert.That(staged).DoesNotContain("nested/settings");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task CopyCanPublishTargetWithoutSwitching()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "remote", "set", remote);
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            File.WriteAllText(Path.Combine(home, "settings"), "initial");
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");
            await RunAsync(home, "git", $"--git-dir={gitDir}", "branch", "target");
            File.WriteAllText(Path.Combine(home, "settings"), "updated");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "copy-to-environment", "settings", "target", "--publish");

            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "show", "target:settings")).IsEqualTo("updated");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "branch", "--show-current")).IsEqualTo("main");
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("updated");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddStagesGeneratedVariableExportFiles()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string generatedPath = Path.Combine(home, ".EnvManager", "bash", "00-envmanager-variables.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(generatedPath)!);
            File.WriteAllText(generatedPath, "export VALUE='managed'");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", ".EnvManager/bash/00-envmanager-variables.sh");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDirectory}", "show", ":.EnvManager/bash/00-envmanager-variables.sh"))
                .IsEqualTo("export VALUE='managed'");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddStagesWithoutCommittingOrRequiringRemote()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            string before = await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD");
            File.WriteAllText(Path.Combine(home, "other"), "previously staged");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "add", "other");
            File.WriteAllText(Path.Combine(home, "settings"), "local content");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");

            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD")).IsEqualTo(before);
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", ":settings")).IsEqualTo("local content");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", ":other")).IsEqualTo("previously staged");
            string listing = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "list");
            await Assert.That(listing).Contains("settings");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddStagesFilesElsewhereInsideEnvManager()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string filePath = Path.Combine(home, ".EnvManager", "custom", "settings.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "custom: true");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", ".EnvManager/custom/settings.yml");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDirectory}", "show", ":.EnvManager/custom/settings.yml")).IsEqualTo("custom: true");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddRejectsFilesInsideInternalGitRepository()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            string internalFilePath = Path.Combine(gitDirectory, "not-a-work-tree-file");
            File.WriteAllText(internalFilePath, "internal");

            await Assert.That(async () => await RunAsync(
                home,
                "dotnet",
                typeof(Program).Assembly.Location,
                "files",
                "add",
                ".EnvManager/repo/not-a-work-tree-file")).Throws<InvalidOperationException>();

            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDirectory}", "diff", "--cached", "--name-only")).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PushCommitsStagedAddAndRemoveWithoutRestoringRemovedFile()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            await RunAsync(home, "git", $"--git-dir={gitDir}", "remote", "add", "origin", remote);
            File.WriteAllText(Path.Combine(home, "old"), "keep on disk");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "add", "old");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "commit", "-m", "seed tracked file");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "push", "origin", "main");
            string before = await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main");
            File.WriteAllText(Path.Combine(home, "settings"), "new content");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "remove", "old");

            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD")).IsEqualTo(before);
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", ":settings")).IsEqualTo("new content");
            string staged = await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "diff", "--cached", "--name-status", "--no-renames");
            await Assert.That(staged).Contains("A\tsettings");
            await Assert.That(staged).Contains("D\told");
            await Assert.That(File.ReadAllText(Path.Combine(home, "old"))).IsEqualTo("keep on disk");
            string listing = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "list");
            await Assert.That(listing.Contains("old")).IsFalse();
            await Assert.That(listing).Contains("settings");
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(before);

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");

            string localCommit = await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD");
            await Assert.That(localCommit).IsNotEqualTo(before);
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(localCommit);
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:settings")).IsEqualTo("new content");
            string remoteFiles = await RunAsync(root.FullName, "git", $"--git-dir={remote}", "ls-tree", "--name-only", "main");
            await Assert.That(remoteFiles.Contains("old")).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(home, "old"))).IsEqualTo("keep on disk");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task RemoveStagesWithoutCommittingAndPushCommitsRemoval()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            File.WriteAllText(Path.Combine(home, "settings"), "keep on disk");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "add", "settings");
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "commit", "-m", "seed file");
            await RunAsync(home, "git", $"--git-dir={gitDir}", "remote", "add", "origin", remote);
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "push", "origin", "main");
            string before = await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "remove", "settings");

            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD")).IsEqualTo(before);
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "diff", "--cached", "--name-status")).IsEqualTo("D\tsettings");
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(before);
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("keep on disk");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");

            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "log", "-1", "--format=%s")).IsEqualTo("Remove file 'settings'.");
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "ls-tree", "--name-only", "main")).DoesNotContain("settings");
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("keep on disk");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-FilesAddTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task<string> RunAsync(string home, string executable, params string[] arguments)
        => await RunFromAsync(home, home, executable, arguments);

    private static async Task<string> RunFromAsync(string workingDirectory, string home, string executable, params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
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
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string result = await output;
        string errorOutput = await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {result}{errorOutput}");
        }

        return result.TrimEnd();
    }
}
