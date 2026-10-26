using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Git;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class GitPullTests
{
    [Test]
    public async Task PullReplacesDivergedBranchAndConflictingFiles()
    {
        DirectoryInfo root = CreateRoot();

        try
        {
            (DirectoryInfo source, GitClient client) = await CreateRepositoriesAsync(root);
            string home = client.WorkingDirectory.FullName;

            File.WriteAllText(Path.Combine(home, "settings"), "local commit");
            File.WriteAllText(Path.Combine(home, "local-only"), "local file");
            await RunClientAsync(client, "add", "settings", "local-only");
            await RunClientAsync(client, "commit", "-m", "local change");
            File.WriteAllText(Path.Combine(home, "settings"), "local staged");
            await RunClientAsync(client, "add", "settings");
            File.WriteAllText(Path.Combine(home, "settings"), "local unstaged");
            File.WriteAllText(Path.Combine(home, "incoming"), "untracked conflict");
            Directory.CreateDirectory(Path.Combine(home, "incoming-directory"));
            File.WriteAllText(Path.Combine(home, "incoming-directory", "untracked"), "obstruction");
            File.WriteAllText(Path.Combine(home, "unrelated"), "keep me");

            File.WriteAllText(Path.Combine(source.FullName, "settings"), "remote change");
            File.WriteAllText(Path.Combine(source.FullName, "incoming"), "remote file");
            File.WriteAllText(Path.Combine(source.FullName, "incoming-directory"), "remote replaces directory");
            File.Delete(Path.Combine(source.FullName, "obsolete"));
            await RunGitAsync(source, "add", ".");
            await RunGitAsync(source, "commit", "-m", "remote change");
            await RunGitAsync(source, "push", "origin", "main");
            string remoteCommit = await RunGitAsync(source, "rev-parse", "HEAD");

            await client.PullAsync();

            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(remoteCommit);
            await Assert.That(await RunClientAsync(client, "symbolic-ref", "--short", "HEAD")).IsEqualTo("main");
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("remote change");
            await Assert.That(File.ReadAllText(Path.Combine(home, "incoming"))).IsEqualTo("remote file");
            await Assert.That(File.ReadAllText(Path.Combine(home, "incoming-directory"))).IsEqualTo("remote replaces directory");
            await Assert.That(File.ReadAllText(Path.Combine(home, "unrelated"))).IsEqualTo("keep me");
            await Assert.That(File.Exists(Path.Combine(home, "local-only"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(home, "obsolete"))).IsFalse();
            await Assert.That(await RunClientAsync(client, "status", "--porcelain", "--untracked-files=no")).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PullRestoresLocalChangesEvenWhenRemoteHasNotAdvanced()
    {
        DirectoryInfo root = CreateRoot();

        try
        {
            (_, GitClient client) = await CreateRepositoriesAsync(root);
            string settings = Path.Combine(client.WorkingDirectory.FullName, "settings");
            File.WriteAllText(settings, "modified");
            await RunClientAsync(client, "add", "settings");
            File.WriteAllText(settings, "modified again");

            await client.PullAsync();

            await Assert.That(File.ReadAllText(settings)).IsEqualTo("initial");
            await Assert.That(await RunClientAsync(client, "status", "--porcelain", "--untracked-files=no")).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task FailedFetchLeavesBranchIndexAndWorkTreeUntouched()
    {
        DirectoryInfo root = CreateRoot();

        try
        {
            (_, GitClient client) = await CreateRepositoriesAsync(root);
            string settings = Path.Combine(client.WorkingDirectory.FullName, "settings");
            string originalCommit = await RunClientAsync(client, "rev-parse", "HEAD");
            File.WriteAllText(settings, "staged");
            await RunClientAsync(client, "add", "settings");
            File.WriteAllText(settings, "unstaged");
            string originalStatus = await RunClientAsync(client, "status", "--porcelain", "--untracked-files=no");
            await RunGitAsync(root, $"--git-dir={Path.Combine(root.FullName, "remote.git")}", "update-ref", "-d", "refs/heads/main");

            await Assert.That(async () => await client.PullAsync()).Throws<GitCommandException>();

            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(originalCommit);
            await Assert.That(await RunClientAsync(client, "status", "--porcelain", "--untracked-files=no")).IsEqualTo(originalStatus);
            await Assert.That(File.ReadAllText(settings)).IsEqualTo("unstaged");
            await Assert.That(await RunClientAsync(client, "show", ":settings")).IsEqualTo("staged");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-PullTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task<(DirectoryInfo Source, GitClient Client)> CreateRepositoriesAsync(DirectoryInfo root)
    {
        DirectoryInfo source = root.CreateSubdirectory("source");
        DirectoryInfo remote = new(Path.Combine(root.FullName, "remote.git"));
        DirectoryInfo home = root.CreateSubdirectory("home");
        await RunGitAsync(root, "init", "--bare", "--initial-branch=main", remote.FullName);
        await RunGitAsync(source, "init", "--initial-branch=main");
        await RunGitAsync(source, "config", "user.name", "Test User");
        await RunGitAsync(source, "config", "user.email", "test@example.com");
        File.WriteAllText(Path.Combine(source.FullName, "settings"), "initial");
        File.WriteAllText(Path.Combine(source.FullName, "obsolete"), "removed remotely");
        await RunGitAsync(source, "add", "settings", "obsolete");
        await RunGitAsync(source, "commit", "-m", "initial");
        await RunGitAsync(source, "remote", "add", "origin", remote.FullName);
        await RunGitAsync(source, "push", "origin", "main");

        GitClient client = new(home, new DirectoryInfo(Path.Combine(home.FullName, ".EnvManager", "repo")));
        await client.CloneAsync(new Uri(remote.FullName), "main");
        await RunClientAsync(client, "config", "user.name", "Test User");
        await RunClientAsync(client, "config", "user.email", "test@example.com");
        return (source, client);
    }

    private static async Task<string> RunClientAsync(GitClient client, params string[] arguments)
    {
        GitCommandResult result = await client.RunAsync(arguments);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {result.StandardError}");
        }

        return result.StandardOutput;
    }

    private static async Task<string> RunGitAsync(DirectoryInfo directory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = directory.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {await error}");
        }

        return (await output).TrimEnd();
    }
}
