// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsPrompt.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine.Settings;

/// <summary>Provides interactive setting-key and value prompts with redirected-input support.</summary>
internal static class SettingsPrompt
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ internal Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Prompts the user to select one key from the supplied choices.</summary>
    /// <param name="title">The prompt title.</param>
    /// <param name="keys">The available keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected key.</returns>
    /// <exception cref="ConfigurationException">The selection is empty or is not one of the choices.</exception>
    internal static async Task<string> SelectKeyAsync(string title, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            throw new ConfigurationException("There are no settings available to select.");
        }

        if (AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected)
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(Markup.Escape(title))
                    .AddChoices(keys));
        }

        ConsoleReporter.Info(title);
        foreach (string key in keys)
        {
            ConsoleReporter.Info($"  {key}");
        }

        ConsoleReporter.Info("Enter a key:");
        string? selectedKey = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (selectedKey is null || !keys.Contains(selectedKey, StringComparer.Ordinal))
        {
            throw new ConfigurationException($"The selected setting key is invalid. Available keys: {string.Join(", ", keys)}.");
        }

        return selectedKey;
    }

    /// <summary>Reads a setting value from standard input.</summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value entered by the user; an empty line is a valid value.</returns>
    /// <exception cref="ConfigurationException">Input ends before a value is entered.</exception>
    internal static async Task<string> ReadValueAsync(string prompt, CancellationToken cancellationToken)
    {
        ConsoleReporter.Info(prompt);
        string? value = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return value ?? throw new ConfigurationException("No setting value was entered.");
    }
}
