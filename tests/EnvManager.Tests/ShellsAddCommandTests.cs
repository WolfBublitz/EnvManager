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
    public async Task AddPushesMissingProfilesAndRepairsAnAlreadyConfiguredShell()
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

            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash");

            string bashrc = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bashrc");
            string bashProfile = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bash_profile");
            await Assert.That(bashrc).Contains("$HOME/.EnvManager/bash/");
            await Assert.That(bashProfile).IsEqualTo(bashrc);
            await Assert.That(File.ReadAllText(Path.Combine(home, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc"))).IsEqualTo("original");
            string firstCommit = await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main");

            string localBashrc = Path.Combine(home, ".bashrc");
            string loader = File.ReadAllText(localBashrc);
            File.WriteAllText(localBashrc, "already staged");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "add", ".bashrc");
            File.WriteAllText(localBashrc, loader);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "set-url", "origin", Path.Combine(root.FullName, "missing.git"));
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash");
            await Assert.That(await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "show", ":.bashrc")).IsEqualTo("already staged");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(firstCommit);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "restore", "--staged", ".bashrc");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "set-url", "origin", remote);

            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "rm", "--cached", "--", ".bash_profile");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "commit", "-m", "Remove profile from tracking");
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", $"--work-tree={home}", "push", "origin", "main");
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "shells", "add", "bash");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bash_profile")).IsEqualTo(bashrc);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddReportsPushFailureAndKeepsCommitForRetry()
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
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("No 'origin' remote is configured");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "show", "HEAD:.bashrc")).Contains("$HOME/.EnvManager/bash/");
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunSuccessAsync(root.FullName, "git", "init", "--bare", "--initial-branch=main", remote);
            await RunSuccessAsync(home, "git", $"--git-dir={gitDirectory}", "remote", "add", "origin", remote);
            await RunSuccessAsync(home, "dotnet", typeof(Program).Assembly.Location, "push");
            await Assert.That(await RunSuccessAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:.bashrc")).Contains("$HOME/.EnvManager/bash/");
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
        return (process.ExitCode, await output + await error);
    }
}
