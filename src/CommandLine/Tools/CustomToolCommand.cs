using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.CommandLine.Tools;

internal static class CustomToolCommand
{
    public static async Task RunAsync(string? command, string toolName, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ConfigurationException($"No {operation} command is configured for custom tool '{toolName}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string shell = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
            : "/bin/sh";
        ProcessStartInfo startInfo = new(shell) { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/D");
            startInfo.ArgumentList.Add("/C");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(command);
        ConsoleReporter.Info($"Running {operation} command for '{toolName}'...");

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new ConfigurationException($"Could not start shell '{shell}' for custom tool '{toolName}': {exception.Message}", exception);
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }

            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new ConfigurationException($"Failed to {operation} custom tool '{toolName}' (exit code {process.ExitCode}).");
        }
    }
}
