// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ProcessRunner.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnvManager.PackageManagers;

/// <summary>
/// Shared helper for running external executables (package manager binaries) and
/// capturing their result, used by every <see cref="IPackageManager"/> implementation.
/// </summary>
public static class ProcessRunner
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Determines whether the given executable can be found and started on the current system.</summary>
    /// <param name="executable">The name of the executable to probe for.</param>
    /// <param name="probeArguments">The arguments to run the probe with, typically a "--version" style flag.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task<bool> IsAvailableAsync(string executable, string[] probeArguments, CancellationToken cancellationToken = default)
    {
        try
        {
            (int exitCode, _, _) = await RunAsync(executable, probeArguments, cancellationToken).ConfigureAwait(false);

            return exitCode == 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Runs the given executable with the given arguments and captures its output.</summary>
    /// <param name="executable">The executable to run.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown when the executable cannot be started.</exception>
    public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo,
        };

        StringBuilder standardOutput = new();
        StringBuilder standardError = new();

        process.OutputDataReceived += (_, eventArguments) =>
        {
            if (eventArguments.Data is not null)
            {
                standardOutput.AppendLine(eventArguments.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArguments) =>
        {
            if (eventArguments.Data is not null)
            {
                standardError.AppendLine(eventArguments.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, standardOutput.ToString().TrimEnd(), standardError.ToString().TrimEnd());
    }
}
