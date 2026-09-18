// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConsoleReporter.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using Spectre.Console;

namespace EnvManager.CommandLine;

/// <summary>
/// Centralizes the color-coded console output used by every command, per the
/// EnvManager CLI conventions: green for success, red for errors, yellow for
/// warnings, and blue for informational messages.
/// </summary>
public static class ConsoleReporter
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Writes a green success message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Success(string message)
        => AnsiConsole.MarkupLineInterpolated($"[green]✓ {message}[/]");

    /// <summary>Writes a red error message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Error(string message)
        => AnsiConsole.MarkupLineInterpolated($"[red]✗ {message}[/]");

    /// <summary>Writes a yellow warning message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Warning(string message)
        => AnsiConsole.MarkupLineInterpolated($"[yellow]! {message}[/]");

    /// <summary>Writes a blue informational message.</summary>
    /// <param name="message">The message to render. Treated as plain text, not markup.</param>
    public static void Info(string message)
        => AnsiConsole.MarkupLineInterpolated($"[blue]{message}[/]");
}
