// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitCommandExceptionTests.cs                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-23                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Git;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class GitCommandExceptionTests
{
    [Test]
    public async Task ConstructorIncludesFullCommandExitCodeAndBothStreams()
    {
        GitCommandException exception = new(
            "status --short",
            "git --git-dir=/tmp/repo --work-tree=/tmp/home status --short",
            2,
            "stdout details",
            "stderr details");

        await Assert.That(exception.Command).IsEqualTo("git --git-dir=/tmp/repo --work-tree=/tmp/home status --short");
        await Assert.That(exception.ExitCode).IsEqualTo(2);
        await Assert.That(exception.StandardOutput).IsEqualTo("stdout details");
        await Assert.That(exception.StandardError).IsEqualTo("stderr details");
        await Assert.That(exception.Message).Contains("exit code 2");
        await Assert.That(exception.Message).Contains("stdout details");
        await Assert.That(exception.Message).Contains("stderr details");
    }

    [Test]
    public async Task GitClientAddFailureIncludesCapturedCommandAndOutput()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            DirectoryInfo gitDirectory = new(Path.Combine(home.FullName, ".EnvManager", "repo"));
            GitClient git = new(home, gitDirectory);
            await git.InitAsync("failure-check");

            GitCommandException exception = await CaptureGitCommandExceptionAsync(() => git.AddAsync("missing.file"));

            await Assert.That(exception.Command).Contains("git --git-dir=");
            await Assert.That(exception.Command).Contains("--work-tree=");
            await Assert.That(exception.Command).Contains("add missing.file");
            await Assert.That(exception.ExitCode != 0).IsTrue();
            await Assert.That(exception.StandardOutput).IsEqualTo(string.Empty);
            await Assert.That(exception.StandardError.Length > 0).IsTrue();
            await Assert.That(exception.Message).Contains("Standard output:");
            await Assert.That(exception.Message).Contains("(empty)");
            await Assert.That(exception.Message).Contains("Standard error:");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task GitClientInitFailureIncludesCapturedCommandAndOutput()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            DirectoryInfo gitDirectory = new(Path.Combine(home.FullName, ".EnvManager", "repo"));
            GitClient git = new(home, gitDirectory);
            GitCommandException exception = await CaptureGitCommandExceptionAsync(() => git.InitAsync("invalid branch"));

            await Assert.That(exception.Command).Contains("git init --bare --initial-branch invalid branch");
            await Assert.That(exception.ExitCode != 0).IsTrue();
            await Assert.That(exception.Message).Contains("Standard output:");
            await Assert.That(exception.Message).Contains("Standard error:");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task GitClientCloneFailureIncludesCapturedCommandAndOutput()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            DirectoryInfo gitDirectory = new(Path.Combine(home.FullName, ".EnvManager", "repo"));
            GitClient git = new(home, gitDirectory);
            Uri missingRepository = new($"file://{Path.Combine(home.FullName, "missing-remote")}");
            GitCommandException exception = await CaptureGitCommandExceptionAsync(() => git.CloneAsync(missingRepository, "main"));

            await Assert.That(exception.Command).Contains("git clone --bare file://");
            await Assert.That(exception.Command).Contains(gitDirectory.FullName);
            await Assert.That(exception.ExitCode != 0).IsTrue();
            await Assert.That(exception.Message).Contains("Standard output:");
            await Assert.That(exception.Message).Contains("Standard error:");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateHome()
    {
        DirectoryInfo home = new(Path.Combine(Path.GetTempPath(), $"EnvManager-GitTests-{Guid.NewGuid():N}"));
        home.Create();

        return home;
    }

    private static async Task<GitCommandException> CaptureGitCommandExceptionAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (GitCommandException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The Git operation was expected to fail.");
    }
}
