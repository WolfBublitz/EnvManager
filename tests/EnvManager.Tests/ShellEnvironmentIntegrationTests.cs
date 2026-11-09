// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellEnvironmentIntegrationTests.cs                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ShellEnvironmentIntegrationTests
{
    [Test]
    public async Task VariableUpdatesSwitchesAndPullsRefreshTheGeneratedShellExports()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remoteHome = Directory.CreateDirectory(Path.Combine(root.FullName, "remote-home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunGitSuccessAsync(root.FullName, "init", "--bare", "--initial-branch=main", remote);
            await RunEnvSuccessAsync(home, "init", "--environment", "main");
            await RunEnvSuccessAsync(home, "variable", "set", "GREETING", "local");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await RunGitSuccessAsync(home, $"--git-dir={gitDirectory}", $"--work-tree={home}", "remote", "add", "origin", remote);
            await RunEnvSuccessAsync(home, "shells", "add", "bash", "--yes");

            string generatedExports = Path.Combine(home, ".EnvManager", "bash", "00-envmanager-variables.sh");
            await Assert.That(File.ReadAllText(generatedExports)).Contains("export GREETING='local'");

            await RunEnvSuccessAsync(home, "switch", "secondary", "--create");
            await Assert.That(File.Exists(generatedExports)).IsFalse();
            await RunEnvSuccessAsync(home, "switch", "main");
            await Assert.That(File.ReadAllText(generatedExports)).Contains("export GREETING='local'");

            await RunEnvSuccessAsync(remoteHome, "clone", new Uri(remote).AbsoluteUri, "--environment", "main");
            await RunEnvSuccessAsync(remoteHome, "variable", "update", "GREETING", "remote");
            await RunEnvSuccessAsync(remoteHome, "push", "--yes");
            await RunEnvSuccessAsync(home, "pull", "--yes");

            await Assert.That(File.ReadAllText(generatedExports)).Contains("export GREETING='remote'");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ConfigurationSavesStageAllShellDirectoryChangesButExcludeTheInternalRepository()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            string remote = Path.Combine(root.FullName, "remote.git");
            await RunGitSuccessAsync(root.FullName, "init", "--bare", "--initial-branch=main", remote);
            await RunEnvSuccessAsync(home, "init", "--environment", "main");

            string bashDirectory = Path.Combine(home, ".EnvManager", "bash");
            string fishDirectory = Path.Combine(home, ".EnvManager", "fish", "nested");
            string internalDirectory = Path.Combine(home, ".EnvManager", "repo");
            Directory.CreateDirectory(bashDirectory);
            Directory.CreateDirectory(fishDirectory);
            File.WriteAllText(Path.Combine(bashDirectory, "custom.sh"), "export CUSTOM='bash'");
            File.WriteAllText(Path.Combine(fishDirectory, "custom.fish"), "set CUSTOM fish");
            File.WriteAllText(Path.Combine(internalDirectory, "not-a-work-tree-file"), "internal");

            string gitDirectory = Path.Combine(home, ".EnvManager", "repo");
            await RunGitSuccessAsync(home, $"--git-dir={gitDirectory}", "remote", "add", "origin", remote);
            await RunEnvSuccessAsync(home, "shells", "add", "bash", "--yes");

            string trackedFiles = await RunGitSuccessAsync(home, $"--git-dir={gitDirectory}", "ls-tree", "-r", "--name-only", "HEAD");
            await Assert.That(trackedFiles).Contains(".EnvManager/bash/custom.sh");
            await Assert.That(trackedFiles).Contains(".EnvManager/bash/00-envmanager-variables.sh");
            await Assert.That(trackedFiles).Contains(".EnvManager/fish/nested/custom.fish");
            await Assert.That(trackedFiles).DoesNotContain(".EnvManager/repo/not-a-work-tree-file");

            File.Delete(Path.Combine(fishDirectory, "custom.fish"));
            File.WriteAllText(Path.Combine(bashDirectory, "custom.sh"), "export CUSTOM='updated'");
            await RunEnvSuccessAsync(home, "variable", "set", "VERSIONED", "yes");

            trackedFiles = await RunGitSuccessAsync(home, $"--git-dir={gitDirectory}", "ls-tree", "-r", "--name-only", "HEAD");
            await Assert.That(trackedFiles).DoesNotContain(".EnvManager/fish/nested/custom.fish");
            await Assert.That(await RunGitSuccessAsync(home, $"--git-dir={gitDirectory}", "show", "HEAD:.EnvManager/bash/custom.sh"))
                .IsEqualTo("export CUSTOM='updated'");
            await Assert.That(trackedFiles).DoesNotContain(".EnvManager/repo/not-a-work-tree-file");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-ShellEnvironmentIntegration-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static Task<string> RunEnvSuccessAsync(string home, params string[] arguments)
        => RunSuccessAsync(home, "dotnet", [typeof(Program).Assembly.Location, .. arguments]);

    private static Task<string> RunGitSuccessAsync(string workingDirectory, params string[] arguments)
        => RunSuccessAsync(workingDirectory, "git", arguments);

    private static async Task<string> RunSuccessAsync(string workingDirectory, string executable, string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (executable == "dotnet")
        {
            startInfo.Environment["ENVMANAGER_HOME"] = workingDirectory;
        }

        startInfo.Environment["GIT_AUTHOR_NAME"] = "Test User";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Test User";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string combinedOutput = await output + await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} {string.Join(' ', arguments)} failed: {combinedOutput}");
        }

        return combinedOutput.TrimEnd();
    }
}
