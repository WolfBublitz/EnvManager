using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class CustomToolCommandTests
{
    [Test]
    public async Task CustomCommandsAreRecordedAndRunForEachOperation()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo root = CreateRoot();
        try
        {
            await RunSuccessAsync(root.FullName, "init", "--environment", "main");
            await RunSuccessAsync(root.FullName, "tools", "add", "example",
                "--install-command", "printf 'installed\\n' >> events",
                "--update-command", "printf 'updated\\n' >> events",
                "--remove-command", "printf 'removed\\n' >> events");
            string config = File.ReadAllText(Path.Combine(root.FullName, ".EnvManager", "config.yml"));
            await Assert.That(config).Contains("installCommand:");
            await Assert.That(config).Contains("updateCommand:");
            await Assert.That(config).Contains("removeCommand:");
            await Assert.That(await RunSuccessAsync(root.FullName, "tools", "list")).Contains("custom command");
            await RunSuccessAsync(root.FullName, "tools", "update", "example");
            await RunSuccessAsync(root.FullName, "tools", "remove", "example");
            await Assert.That(File.ReadAllText(Path.Combine(root.FullName, "events"))).IsEqualTo("installed\nupdated\nremoved\n");
            await Assert.That(await RunSuccessAsync(root.FullName, "tools", "list")).Contains("No tools are tracked");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task FailuresAndMissingCommandsDoNotChangeTracking()
    {
        DirectoryInfo root = CreateRoot();
        try
        {
            string successfulCommand = OperatingSystem.IsWindows() ? "ver >nul" : "true";
            string exit7 = OperatingSystem.IsWindows() ? "exit /b 7" : "exit 7";
            string exit8 = OperatingSystem.IsWindows() ? "exit /b 8" : "exit 8";
            await RunSuccessAsync(root.FullName, "init", "--environment", "main");
            (int failedInstall, string installOutput) = await RunAsync(root.FullName, "tools", "add", "bad", "--install-command", exit7);
            await Assert.That(failedInstall).IsNotEqualTo(0);
            await Assert.That(installOutput).Contains("exit code 7");
            await Assert.That(await RunSuccessAsync(root.FullName, "tools", "list")).Contains("No tools are tracked");

            await RunSuccessAsync(root.FullName, "tools", "add", "example", "--install-command", successfulCommand, "--remove-command", exit8);
            (int failedUpdate, string updateOutput) = await RunAsync(root.FullName, "tools", "update", "example");
            await Assert.That(failedUpdate).IsNotEqualTo(0);
            await Assert.That(updateOutput).Contains("No update command");
            (int failedRemove, string removeOutput) = await RunAsync(root.FullName, "tools", "remove", "example");
            await Assert.That(failedRemove).IsNotEqualTo(0);
            await Assert.That(removeOutput).Contains("exit code 8");
            await Assert.That(await RunSuccessAsync(root.FullName, "tools", "list")).Contains("example");

            await RunSuccessAsync(root.FullName, "tools", "add", "no-remove", "--install-command", successfulCommand);
            (int missingRemove, string missingOutput) = await RunAsync(root.FullName, "tools", "remove", "no-remove");
            await Assert.That(missingRemove).IsNotEqualTo(0);
            await Assert.That(missingOutput).Contains("No remove command");
            await Assert.That(await RunSuccessAsync(root.FullName, "tools", "list")).Contains("no-remove");

            (int invalid, string invalidOutput) = await RunAsync(root.FullName, "tools", "add", "other",
                "--install-command", successfulCommand, "--version", "1");
            await Assert.That(invalid).IsNotEqualTo(0);
            await Assert.That(invalidOutput).Contains("--install-command cannot be combined");
            (int empty, string emptyOutput) = await RunAsync(root.FullName, "tools", "add", "other", "--install-command", " ");
            await Assert.That(empty).IsNotEqualTo(0);
            await Assert.That(emptyOutput).Contains("cannot be empty");
            (int orphan, string orphanOutput) = await RunAsync(root.FullName, "tools", "add", "other", "--update-command", successfulCommand);
            await Assert.That(orphan).IsNotEqualTo(0);
            await Assert.That(orphanOutput).Contains("require --install-command");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateRoot()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-CustomToolTests-{Guid.NewGuid():N}"));
        root.Create();
        return root;
    }

    private static async Task<string> RunSuccessAsync(string home, params string[] arguments)
    {
        (int exitCode, string output) = await RunAsync(home, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"EnvManager {string.Join(' ', arguments)} failed: {output}");
        }

        return output;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string home, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
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
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start EnvManager.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }
}
