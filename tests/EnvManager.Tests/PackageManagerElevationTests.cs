using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using EnvManager.PackageManagers;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class PackageManagerElevationTests
{
    [Test]
    public async Task OnlySystemPackageManagersRequireElevation()
    {
        foreach (string name in new[] { "apt", "dnf", "yum", "zypper" })
        {
            await Assert.That(PackageManagerFactory.GetByName(name).RequiresElevation).IsTrue();
        }

        foreach (string name in new[] { "brew", "scoop", "choco", "winget" })
        {
            await Assert.That(PackageManagerFactory.GetByName(name).RequiresElevation).IsFalse();
        }
    }

    [Test]
    public async Task SystemPackageOperationsUseSudoAndPreserveArguments()
    {
        if (!OperatingSystem.IsLinux() || (await ProcessRunner.RunAsync("id", ["-u"])).StandardOutput == "0")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"EnvManager-PackageTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
            string home = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;
            string log = Path.Combine(root, "sudo.log");
            WriteExecutable(Path.Combine(bin, "sudo"), "#!/bin/sh\nprintf '%s\\n' \"$@\" >> \"$PM_LOG\"\n");

            await RunSuccessAsync(home, bin, log, "init", "--environment", "main");
            foreach ((string manager, string executable, string install, string update) in new[]
            {
                ("apt", "apt-get", "install\n-y\npkg name=1.0", "install\n-y\n--only-upgrade\npkg name"),
                ("dnf", "dnf", "install\n-y\npkg name-1.0", "upgrade\n-y\npkg name"),
                ("yum", "yum", "install\n-y\npkg name-1.0", "update\n-y\npkg name"),
                ("zypper", "zypper", "install\n-y\npkg name=1.0", "update\n-y\npkg name"),
            })
            {
                File.WriteAllText(log, string.Empty);
                await RunSuccessAsync(home, bin, log, "tools", "add", "pkg name", "-p", manager, "-v", "1.0");
                await RunSuccessAsync(home, bin, log, "tools", "update", "pkg name");
                await RunSuccessAsync(home, bin, log, "tools", "remove", "pkg name");
                string expected = $"{executable}\n{install}\n{executable}\n{update}\n{executable}\nremove\n-y\npkg name\n";
                await Assert.That(File.ReadAllText(log)).IsEqualTo(expected);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task UserPackageManagerDoesNotUseSudo()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"EnvManager-PackageTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
            string home = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;
            string log = Path.Combine(root, "brew.log");
            WriteExecutable(Path.Combine(bin, "sudo"), "#!/bin/sh\nexit 99\n");
            WriteExecutable(Path.Combine(bin, "brew"), "#!/bin/sh\nprintf '%s\\n' \"$@\" >> \"$PM_LOG\"\n");

            await RunSuccessAsync(home, bin, log, "init", "--environment", "main");
            await RunSuccessAsync(home, bin, log, "tools", "add", "example", "-p", "brew");
            await RunSuccessAsync(home, bin, log, "tools", "update", "example");
            await RunSuccessAsync(home, bin, log, "tools", "remove", "example");
            await Assert.That(File.ReadAllText(log)).IsEqualTo("install\nexample\nupgrade\nexample\nuninstall\nexample\n");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task MissingSudoIsReported()
    {
        if (!OperatingSystem.IsLinux() || (await ProcessRunner.RunAsync("id", ["-u"])).StandardOutput == "0")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"EnvManager-PackageTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
            string home = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;
            string log = Path.Combine(root, "sudo.log");
            await RunSuccessAsync(home, bin, log, "init", "--environment", "main");
            (int exitCode, string output) = await RunAsync(home, bin, log, false, "tools", "add", "example", "-p", "apt");
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("sudo was not found");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task SudoFailureIsReportedWithoutRecordingTheTool()
    {
        if (!OperatingSystem.IsLinux() || (await ProcessRunner.RunAsync("id", ["-u"])).StandardOutput == "0")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"EnvManager-PackageTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
            string home = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;
            string log = Path.Combine(root, "sudo.log");
            WriteExecutable(Path.Combine(bin, "sudo"), "#!/bin/sh\necho 'authentication_failed' >&2\nexit 1\n");
            await RunSuccessAsync(home, bin, log, "init", "--environment", "main");

            (int exitCode, string output) = await RunAsync(home, bin, log, "tools", "add", "not-installed", "-p", "apt");
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("authentication_failed");
            string tools = await RunSuccessAsync(home, bin, log, "tools", "list");
            await Assert.That(tools.Contains("not-installed", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteExecutable(string path, string content)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }

        File.WriteAllText(path, content);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static async Task<string> RunSuccessAsync(string home, string bin, string log, params string[] arguments)
    {
        (int exitCode, string output) = await RunAsync(home, bin, log, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"EnvManager {string.Join(' ', arguments)} failed: {output}");
        }

        return output;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string home, string bin, string log, params string[] arguments)
        => await RunAsync(home, bin, log, true, arguments);

    private static async Task<(int ExitCode, string Output)> RunAsync(string home, string bin, string log, bool includeSystemPath, params string[] arguments)
    {
        string dotnet = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "dotnet"))
            .FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("dotnet was not found on PATH.");
        ProcessStartInfo startInfo = new(dotnet)
        {
            WorkingDirectory = home,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["PATH"] = includeSystemPath ? bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH") : bin;
        startInfo.Environment["ENVMANAGER_HOME"] = home;
        startInfo.Environment["PM_LOG"] = log;
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
