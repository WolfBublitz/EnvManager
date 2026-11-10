// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesCopyToEnvironmentCommand.cs                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-10                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files copy-to-environment</c> command copies a tracked file to another
/// environment branch without changing the active environment or working tree.
/// </summary>
public sealed class FilesCopyToEnvironmentCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesCopyToEnvironmentCommand"/> class.</summary>
    public FilesCopyToEnvironmentCommand()
        : base("copy-to-environment", "Copies a tracked file to another environment.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string currentEnvironment = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> files = await repository.ListFilesAsync(cancellationToken).ConfigureAwait(false);

            if (files.Count == 0)
            {
                ConsoleReporter.Info($"No files are tracked for environment '{currentEnvironment}'.");
                return;
            }

            IReadOnlyList<string> environments = (await repository.ListEnvironmentsAsync(cancellationToken).ConfigureAwait(false))
                .Where(environment => !string.Equals(environment, currentEnvironment, StringComparison.Ordinal))
                .ToList();

            if (environments.Count == 0)
            {
                ConsoleReporter.Info("No other environments are available.");
                return;
            }

            bool interactive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
            string fileName = SelectFile(
                files,
                interactive,
                choices => AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a file to copy")
                        .PageSize(10)
                        .AddChoices(choices)));
            string targetEnvironment = SelectEnvironment(
                environments,
                interactive,
                choices => AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a target environment")
                        .PageSize(10)
                        .AddChoices(choices)));

            bool copied = await repository.CopyFileToEnvironmentAsync(fileName, targetEnvironment, cancellationToken).ConfigureAwait(false);

            if (copied)
            {
                ConsoleReporter.Success(
                    $"Copied file '{fileName}' from environment '{currentEnvironment}' to '{targetEnvironment}'. " +
                    $"The copy is committed on the target branch; switch to it and run 'EnvManager push' to publish it.");
            }
            else
            {
                ConsoleReporter.Info($"File '{fileName}' already matches in environment '{targetEnvironment}'.");
            }
        }));
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ internal Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Prompts for a tracked file when the terminal is interactive.</summary>
    /// <param name="files">The tracked file choices.</param>
    /// <param name="interactive">Whether standard input is interactive.</param>
    /// <param name="prompt">The file selection prompt.</param>
    /// <returns>The selected tracked file.</returns>
    /// <exception cref="ConfigurationException">The selection is empty or input is not interactive.</exception>
    internal static string SelectFile(
        IReadOnlyList<string> files,
        bool interactive,
        Func<IReadOnlyList<string>, string> prompt)
    {
        if (files.Count == 0)
        {
            throw new ConfigurationException("No files are tracked in the current environment.");
        }

        if (!interactive)
        {
            throw new ConfigurationException("A file must be selected from an interactive terminal.");
        }

        return prompt(files);
    }

    /// <summary>Prompts for a target environment when the terminal is interactive.</summary>
    /// <param name="environments">The target environment choices.</param>
    /// <param name="interactive">Whether standard input is interactive.</param>
    /// <param name="prompt">The environment selection prompt.</param>
    /// <returns>The selected target environment.</returns>
    /// <exception cref="ConfigurationException">The selection is empty or input is not interactive.</exception>
    internal static string SelectEnvironment(
        IReadOnlyList<string> environments,
        bool interactive,
        Func<IReadOnlyList<string>, string> prompt)
    {
        if (environments.Count == 0)
        {
            throw new ConfigurationException("No other environments are available.");
        }

        if (!interactive)
        {
            throw new ConfigurationException("A target environment must be selected from an interactive terminal.");
        }

        return prompt(environments);
    }
}
