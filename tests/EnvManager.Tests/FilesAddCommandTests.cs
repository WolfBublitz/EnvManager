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
    public async Task AddCommitsLocallyWithoutRemote()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "init", "--environment", "main");
            File.WriteAllText(Path.Combine(home, "settings"), "local content");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");

            string gitDir = Path.Combine(home, ".EnvManager", "repo");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", "HEAD:settings")).IsEqualTo("local content");
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "log", "-1", "--format=%s")).IsEqualTo("Add file 'settings'.");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task AddLeavesRemoteUnchangedUntilExplicitPush()
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
            await RunAsync(home, "git", $"--git-dir={gitDir}", $"--work-tree={home}", "push", "origin", "main");
            string before = await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main");
            File.WriteAllText(Path.Combine(home, "settings"), "new content");

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "files", "add", "settings");

            string localCommit = await RunAsync(home, "git", $"--git-dir={gitDir}", "rev-parse", "HEAD");
            await Assert.That(localCommit).IsNotEqualTo(before);
            await Assert.That(await RunAsync(home, "git", $"--git-dir={gitDir}", "show", "HEAD:settings")).IsEqualTo("new content");
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(before);

            await RunAsync(home, "dotnet", typeof(Program).Assembly.Location, "push", "--yes");

            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "rev-parse", "main")).IsEqualTo(localCommit);
            await Assert.That(await RunAsync(root.FullName, "git", $"--git-dir={remote}", "show", "main:settings")).IsEqualTo("new content");
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
        string result = await output;
        string errorOutput = await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {result}{errorOutput}");
        }

        return result.TrimEnd();
    }
}
