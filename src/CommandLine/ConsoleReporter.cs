// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConsoleReporter.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.PackageManagers;

using Spectre.Console;

namespace EnvManager.CommandLine;

/// <summary>
/// Centralizes consistent, color-coded console output and interactive feedback.
/// </summary>
public static class ConsoleReporter
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Writes a green success message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Success(string message)
        => AnsiConsole.MarkupLineInterpolated($"[green]✅ {message}[/]");

    /// <summary>Writes a red error message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Error(string message)
        => AnsiConsole.MarkupLineInterpolated($"[red]❌ {message}[/]");

    /// <summary>Writes a formatted exception using Spectre.Console.</summary>
    /// <param name="exception">The exception to render.</param>
    public static void Exception(Exception exception)
        => AnsiConsole.Write(new Panel(new Text(exception.ToString()))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(" Exception "),
        });

    /// <summary>Writes a yellow warning message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Warning(string message)
        => AnsiConsole.MarkupLineInterpolated($"[yellow]⚠️ {message}[/]");

    /// <summary>Writes a blue informational message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Info(string message)
        => AnsiConsole.MarkupLineInterpolated($"[blue]{message}[/]");

    /// <summary>Displays a Spectre confirmation prompt, with a safe fallback for redirected input.</summary>
    public static async Task<bool> ConfirmAsync(string message, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected)
        {
            return AnsiConsole.Confirm(Markup.Escape(message), defaultValue);
        }

        string defaultHint = defaultValue ? "Y/n" : "y/N";
        AnsiConsole.MarkupLineInterpolated($"[yellow]❓ {message} ({defaultHint})[/]");
        string? answer = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        return ParseConfirmationResponse(answer, defaultValue);
    }

    /// <summary>Runs a potentially long operation with a terminal spinner when interactive.</summary>
    public static async Task RunWithStatusAsync(string message, Func<Task> action)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            Info($"⏳ {message}");
            await action().ConfigureAwait(false);
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[blue]⏳ {Markup.Escape(message)}[/]", async _ => await action().ConfigureAwait(false))
            .ConfigureAwait(false);
    }

    /// <summary>Runs a sequence of operations with a progress bar when the output is interactive.</summary>
    public static async Task RunProgressAsync<T>(
        string message,
        IReadOnlyList<T> items,
        Func<T, string> describeItem,
        Func<T, Task> action)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            Info($"📦 {message}");
            foreach (T item in items)
            {
                Info($"  {describeItem(item)}");
                await action(item).ConfigureAwait(false);
            }

            return;
        }

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
            .StartAsync(async context =>
            {
                ProgressTask progressTask = context.AddTask(
                    $"[blue]📦 {Markup.Escape(message)}[/]",
                    maxValue: items.Count);

                foreach (T item in items)
                {
                    progressTask.Description = $"[blue]📦 {Markup.Escape(describeItem(item))}[/]";
                    await action(item).ConfigureAwait(false);
                    progressTask.Increment(1);
                }
            })
            .ConfigureAwait(false);
    }

    /// <summary>Runs a package operation without a spinner when sudo may prompt on the terminal.</summary>
    public static async Task RunPackageOperationAsync(IPackageManager packageManager, string message, Func<Task> action)
    {
        if (OperatingSystem.IsLinux() && packageManager.RequiresElevation)
        {
            Info($"📦 {message}");
            await action().ConfigureAwait(false);
        }
        else
        {
            await RunWithStatusAsync($"📦 {message}", action).ConfigureAwait(false);
        }
    }

    internal static bool ParseConfirmationResponse(string? answer, bool defaultValue)
    {
        if (answer is null)
        {
            return false;
        }

        string response = answer.Trim();
        if (response.Length == 0)
        {
            return defaultValue;
        }

        return string.Equals(response, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
