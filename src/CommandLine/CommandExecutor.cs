// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: CommandExecutor.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.CommandLine;

/// <summary>
/// Wraps the body of every CLI command action so that all known failure modes
/// (missing configuration, git failures, package manager failures, user cancellation)
/// are reported consistently and turn into a non-zero exit code instead of an
/// unhandled exception and stack trace.
/// </summary>
public static class CommandExecutor
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Executes the given command body, translating exceptions into exit codes.</summary>
    /// <param name="action">The command logic to run.</param>
    public static async Task<int> RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);

            return 0;
        }
        catch (OperationCanceledException)
        {
            ConsoleReporter.Warning("Operation was canceled.");

            return 130;
        }
        catch (EnvManagerException exception)
        {
            ConsoleReporter.Error(exception.Message);

            return 1;
        }
        catch (Exception exception)
        {
            ConsoleReporter.Error($"Unexpected error: {exception.Message}");

            return 1;
        }
    }
}
