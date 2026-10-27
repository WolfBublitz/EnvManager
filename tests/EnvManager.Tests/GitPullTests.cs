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
    public async Task ClonePreservesFilesFromDefaultBranchWhenCheckingOutAnotherBranch()
    {
        DirectoryInfo root = CreateRoot();

        try
        {
            DirectoryInfo source = root.CreateSubdirectory("source");
            DirectoryInfo remote = new(Path.Combine(root.FullName, "remote.git"));
            DirectoryInfo home = root.CreateSubdirectory("home");
            await RunGitAsync(source, "init", "--initial-branch=main");
            await RunGitAsync(source, "config", "user.name", "Test User");
            await RunGitAsync(source, "config", "user.email", "test@example.com");
            File.WriteAllText(Path.Combine(source.FullName, ".bashrc"), "default branch");
            File.WriteAllText(Path.Combine(source.FullName, ".zshrc"), "default branch");
            File.WriteAllText(Path.Combine(source.FullName, "profile settings"), "default branch");
            if (!OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path.Combine(source.FullName, "profile\nsettings"), "default branch");
            }
            await RunGitAsync(source, "add", ".");
            await RunGitAsync(source, "commit", "-m", "default branch");
            await RunGitAsync(source, "checkout", "--orphan", "Linux");
            await RunGitAsync(source, "rm", "--cached", "-r", ".");
            File.Delete(Path.Combine(source.FullName, ".bashrc"));
            File.Delete(Path.Combine(source.FullName, ".zshrc"));
            File.Delete(Path.Combine(source.FullName, "profile settings"));
            if (!OperatingSystem.IsWindows())
            {
                File.Delete(Path.Combine(source.FullName, "profile\nsettings"));
            }
            File.WriteAllText(Path.Combine(source.FullName, "settings"), "Linux branch");
            await RunGitAsync(source, "add", "settings");
            await RunGitAsync(source, "commit", "-m", "Linux branch");
            await RunGitAsync(source, "checkout", "main");
            await RunGitAsync(root, "clone", "--bare", source.FullName, remote.FullName);

            File.WriteAllText(Path.Combine(home.FullName, ".bashrc"), "existing bash");
            File.WriteAllText(Path.Combine(home.FullName, ".zshrc"), "existing zsh");
            File.WriteAllText(Path.Combine(home.FullName, "profile settings"), "existing profile");
            if (!OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path.Combine(home.FullName, "profile\nsettings"), "existing multiline profile");
            }
            File.WriteAllText(Path.Combine(home.FullName, "settings"), "existing settings");
            File.WriteAllText(Path.Combine(home.FullName, "unrelated"), "keep me");
            GitClient client = new(home, new DirectoryInfo(Path.Combine(home.FullName, ".EnvManager", "repo")));

            await client.CloneAsync(new Uri(remote.FullName), "Linux");

            string[] backups = Directory.GetDirectories(Path.Combine(client.GitDirectory.FullName, "overwritten-files"));
            await Assert.That(backups.Length).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(backups[0], ".bashrc"))).IsEqualTo("existing bash");
            await Assert.That(File.ReadAllText(Path.Combine(backups[0], ".zshrc"))).IsEqualTo("existing zsh");
            await Assert.That(File.ReadAllText(Path.Combine(backups[0], "profile settings"))).IsEqualTo("existing profile");
            if (!OperatingSystem.IsWindows())
            {
                await Assert.That(File.ReadAllText(Path.Combine(backups[0], "profile\nsettings"))).IsEqualTo("existing multiline profile");
            }
            await Assert.That(File.ReadAllText(Path.Combine(backups[0], "settings"))).IsEqualTo("existing settings");
            await Assert.That(File.ReadAllText(Path.Combine(home.FullName, "settings"))).IsEqualTo("Linux branch");
            await Assert.That(File.ReadAllText(Path.Combine(home.FullName, "unrelated"))).IsEqualTo("keep me");
            await Assert.That(await client.GetCurrentBranchAsync()).IsEqualTo("Linux");

            DirectoryInfo retryHome = root.CreateSubdirectory("retry-home");
            DirectoryInfo retryGit = new(Path.Combine(retryHome.FullName, ".EnvManager", "repo"));
            retryGit.Parent!.Create();
            await RunGitAsync(root, "clone", "--bare", remote.FullName, retryGit.FullName);
            File.WriteAllText(Path.Combine(retryHome.FullName, ".bashrc"), "retry bash");
            File.WriteAllText(Path.Combine(retryHome.FullName, ".zshrc"), "retry zsh");
            GitClient retryClient = new(retryHome, retryGit);
            GitCommandResult failedCheckout = await retryClient.RunAsync(["checkout", "Linux"]);
            await Assert.That(failedCheckout.Succeeded).IsFalse();

            await retryClient.CheckoutBranchAsync("Linux", allowCreate: false);

            string[] retryBackups = Directory.GetDirectories(Path.Combine(retryGit.FullName, "overwritten-files"));
            await Assert.That(retryBackups.Length).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(retryBackups[0], ".bashrc"))).IsEqualTo("retry bash");
            await Assert.That(File.ReadAllText(Path.Combine(retryBackups[0], ".zshrc"))).IsEqualTo("retry zsh");
            await Assert.That(File.ReadAllText(Path.Combine(retryHome.FullName, "settings"))).IsEqualTo("Linux branch");
            await Assert.That(await retryClient.GetCurrentBranchAsync()).IsEqualTo("Linux");

            await retryClient.CheckoutBranchAsync("new-environment", allowCreate: true);

            await Assert.That(await retryClient.GetCurrentBranchAsync()).IsEqualTo("new-environment");
            await Assert.That(File.Exists(Path.Combine(retryHome.FullName, "settings"))).IsFalse();
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

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

    [Test]
    public async Task PullCommandPreviewsChangesAndDecliningPreservesLocalState()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (DirectoryInfo source, GitClient client) = await CreateRepositoriesAsync(root);
            string home = client.WorkingDirectory.FullName;
            File.WriteAllText(Path.Combine(home, "settings"), "local commit");
            await RunClientAsync(client, "add", "settings");
            await RunClientAsync(client, "commit", "-m", "local change");
            string localCommit = await RunClientAsync(client, "rev-parse", "HEAD");
            File.WriteAllText(Path.Combine(home, "settings"), "staged");
            await RunClientAsync(client, "add", "settings");
            File.WriteAllText(Path.Combine(home, "settings"), "unstaged");
            File.WriteAllText(Path.Combine(home, "incoming"), "untracked obstruction");
            Directory.CreateDirectory(Path.Combine(home, "incoming-directory"));
            File.WriteAllText(Path.Combine(home, "incoming-directory", "untracked"), "obstruction");
            File.WriteAllText(Path.Combine(source.FullName, "settings"), "remote");
            File.WriteAllText(Path.Combine(source.FullName, "incoming"), "remote incoming");
            File.WriteAllText(Path.Combine(source.FullName, "incoming-directory"), "remote file");
            File.Delete(Path.Combine(source.FullName, "obsolete"));
            await RunGitAsync(source, "add", "-A");
            await RunGitAsync(source, "commit", "-m", "remote changes");
            await RunGitAsync(source, "push", "origin", "main");

            (int exitCode, string output) = await RunPullCommandAsync(home, "n\n");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("M\tsettings");
            await Assert.That(output).Contains("A\tincoming");
            await Assert.That(output).Contains("incoming-directory");
            await Assert.That(output).Contains("D\tobsolete");
            await Assert.That(output).Contains("MM settings");
            await Assert.That(output).Contains("local change");
            await Assert.That(output).Contains("overwritten or removed");
            await Assert.That(output).Contains("cancelled");
            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(localCommit);
            await Assert.That(await RunClientAsync(client, "show", ":settings")).IsEqualTo("staged");
            await Assert.That(File.ReadAllText(Path.Combine(home, "settings"))).IsEqualTo("unstaged");
            await Assert.That(File.ReadAllText(Path.Combine(home, "incoming"))).IsEqualTo("untracked obstruction");
            await Assert.That(File.ReadAllText(Path.Combine(home, "incoming-directory", "untracked"))).IsEqualTo("obstruction");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PullCommandAppliesConfirmedChanges()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (DirectoryInfo source, GitClient client) = await CreateRepositoriesAsync(root);
            string settings = Path.Combine(client.WorkingDirectory.FullName, "settings");
            File.WriteAllText(settings, "local");
            File.WriteAllText(Path.Combine(source.FullName, "settings"), "remote");
            await RunGitAsync(source, "add", "settings");
            await RunGitAsync(source, "commit", "-m", "remote change");
            await RunGitAsync(source, "push", "origin", "main");

            (int exitCode, string output) = await RunPullCommandAsync(client.WorkingDirectory.FullName, "y\n");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("settings");
            await Assert.That(output).Contains("Apply this pull?");
            await Assert.That(File.ReadAllText(settings)).IsEqualTo("remote");
            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(await RunGitAsync(source, "rev-parse", "HEAD"));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PullCommandAsksEvenWhenThereAreNoChanges()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (_, GitClient client) = await CreateRepositoriesAsync(root);
            string localCommit = await RunClientAsync(client, "rev-parse", "HEAD");

            (int exitCode, string output) = await RunPullCommandAsync(client.WorkingDirectory.FullName, "n\n");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("None.");
            await Assert.That(output).Contains("Apply this pull?");
            await Assert.That(output).Contains("cancelled");
            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(localCommit);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PullCommandDefaultsToNoWhenNoInputIsProvided()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (_, GitClient client) = await CreateRepositoriesAsync(root);
            string settings = Path.Combine(client.WorkingDirectory.FullName, "settings");
            File.WriteAllText(settings, "local");

            (int exitCode, string output) = await RunPullCommandAsync(client.WorkingDirectory.FullName, string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("cancelled");
            await Assert.That(File.ReadAllText(settings)).IsEqualTo("local");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PullCommandYesOptionAppliesWithoutInput()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (_, GitClient client) = await CreateRepositoriesAsync(root);
            string settings = Path.Combine(client.WorkingDirectory.FullName, "settings");
            File.WriteAllText(settings, "local");

            (int exitCode, string output) = await RunPullCommandAsync(client.WorkingDirectory.FullName, string.Empty, "--yes");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("settings");
            await Assert.That(output.Contains("Apply this pull?")).IsFalse();
            await Assert.That(File.ReadAllText(settings)).IsEqualTo("initial");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task PreviewAppliesFetchedCommitEvenIfRemoteAdvances()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            (DirectoryInfo source, GitClient client) = await CreateRepositoriesAsync(root);
            (string target, _, _, _, _) = await client.PreviewPullAsync();
            File.WriteAllText(Path.Combine(source.FullName, "settings"), "new remote");
            await RunGitAsync(source, "add", "settings");
            await RunGitAsync(source, "commit", "-m", "advance remote");
            await RunGitAsync(source, "push", "origin", "main");

            await client.ApplyPullAsync(target);

            await Assert.That(await RunClientAsync(client, "rev-parse", "HEAD")).IsEqualTo(target);
            await Assert.That(File.ReadAllText(Path.Combine(client.WorkingDirectory.FullName, "settings"))).IsEqualTo("initial");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunPullCommandAsync(string home, string input, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = home,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ENVMANAGER_HOME"] = home;
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("pull");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start EnvManager.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        string result = await output + await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Pull exited with {process.ExitCode}: {result}");
        }

        return (process.ExitCode, result);
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
