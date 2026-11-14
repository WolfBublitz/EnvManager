using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ShellsAddCommandTests
{
    [Test]
    public async Task SwitchBackupCanBeListedAndRestoredWithoutOverwriting()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            File.WriteAllText(Path.Combine(home, "settings"), "original");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "variable", "set", "EXAMPLE", "value");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDir}", "branch", "other");
            File.WriteAllText(Path.Combine(home, "settings"), "local change");

            string switched = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "switch", "other");
            string backupsRoot = Path.Combine(gitDir, "overwritten-files");
            string backupId = Path.GetFileName(Directory.GetDirectories(backupsRoot)[0]);
            await Assert.That(switched).Contains(backupId);
            string listing = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "backups", "list");
            await Assert.That(listing).Contains("settings");

            (int invalidExitCode, _) = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "backups", "restore", backupId, "../outside", "--to", "escaped");
            await Assert.That(invalidExitCode).IsNotEqualTo(0);
            await Assert.That(File.Exists(Path.Combine(home, "escaped"))).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("original");
            File.WriteAllText(Path.Combine(home, "settings"), "current");
            (int exitCode, _) = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "backups", "restore", backupId, "settings");
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("current");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "backups", "restore", backupId, "settings", "--to", "recovered");
            await Assert.That(File.ReadAllText(Path.Combine(home, "recovered"))).IsEqualTo("local change");
            await Assert.That(File.ReadAllText(Path.Combine(backupsRoot, backupId, "settings"))).IsEqualTo("local change");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ToolApplyPreviewDoesNotExecuteRecordedCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string marker = Path.Combine(home, "marker");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "tools", "add", "custom", "--install-command", "echo run >> marker", "--yes");

            string preview = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "tools", "apply", "--preview");
            await Assert.That(preview).Contains("echo run >> marker");
            await Assert.That(File.ReadAllLines(marker).Length).IsEqualTo(1);
            (int exitCode, string output) = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "tools", "apply");
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("interactive terminal");
            await Assert.That(File.ReadAllLines(marker).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task RemoteAndStatusShowLocalAndPublishedState()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunSuccessAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");

            string localStatus = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "status");
            await Assert.That(localStatus).Contains("Remote: not configured");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "remote", "set", remote);
            string unpublished = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "status");
            await Assert.That(unpublished).Contains("has not been published");

            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");
            string published = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "status");
            await Assert.That(published).Contains("0 local commit(s) ahead, 0 remote commit(s) behind");
            string environments = await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "environment", "list");
            await Assert.That(environments).Contains("main (current)");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddSavesMissingProfilesLocallyAndRepairsAnAlreadyConfiguredShell()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunSuccessAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "add", "origin", remote);
            File.WriteAllText(Path.Combine(home, ".bashrc"), "original");

            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash", "--yes");
            string remoteBranches = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "branch", "--list");
            await Assert.That(remoteBranches).IsEqualTo(string.Empty);
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");

            string bashrc = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bashrc");
            string bashProfile = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bash_profile");
            string migratedProfile = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.EnvManager/bash/00_Profile.sh");
            string envManagerProfile = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.EnvManager/bash/01_EnvManager.sh");
            await Assert.That(bashrc).Contains("$HOME/.EnvManager/bash/");
            await Assert.That(bashrc).DoesNotContain("$HOME/.local/bin");
            await Assert.That(bashProfile).IsEqualTo(bashrc);
            await Assert.That(migratedProfile).IsEqualTo("original");
            await Assert.That(envManagerProfile).IsEqualTo("export PATH=\"$HOME/.local/bin:$PATH\"\nalias em='EnvManager'");
            await Assert.That(File.Exists(Path.Combine(home, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc"))).IsFalse();
            string firstCommit = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main");

            string localBashrc = Path.Combine(home, ".bashrc");
            string loader = File.ReadAllText(localBashrc);
            File.WriteAllText(localBashrc, "already staged");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "add", ".bashrc");
            File.WriteAllText(localBashrc, loader);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "set-url", "origin", Path.Combine(root.FullName, "missing.git"));
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash", "--yes");
            await Assert.That(await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "show", ":.bashrc")).IsEqualTo("already staged");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(firstCommit);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "restore", "--staged", ".bashrc");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "set-url", "origin", remote);

            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "rm", "--cached", "--", ".bash_profile");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "commit", "-m", "Remove profile from tracking");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "push", "origin", "main");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash", "--yes");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bash_profile")).IsEqualTo(bashrc);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddSucceedsWithoutRemoteAndCanBePublishedLater()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            (int exitCode, string output) = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash", "--yes");
            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("saved locally");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "show", "HEAD:.bashrc")).Contains("$HOME/.EnvManager/bash/");
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunSuccessAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "add", "origin", remote);
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bashrc")).Contains("$HOME/.EnvManager/bash/");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddRequiresExplicitTrustBeforeChangingShellProfiles()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");

            (int exitCode, string output) = await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Shell integration cancelled");
            await Assert.That(File.Exists(Path.Combine(home, ".bashrc"))).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(home, ".EnvManager", "config.yml"))).Contains("shells: []");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-ShellAddTests-{Guid.NewGuid():N}"));
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
}
