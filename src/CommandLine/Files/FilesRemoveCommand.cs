// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesRemoveCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files remove</c> command stops tracking a file in the environment
/// repository. The file itself is left untouched in the home directory, since the
/// repository's work tree is the home directory rather than a dedicated checkout.
/// </summary>
public sealed class FilesRemoveCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesRemoveCommand"/> class.</summary>
    public FilesRemoveCommand()
        : base("remove", "Removes an existing file or configuration from the repository.")
    {
        Argument<string?> fileNameArgument = new("file_name")
        {
            Description = "The name of the file to remove. If omitted, select a tracked file interactively.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        this.Add(fileNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string? fileName = parseResult.GetValue(fileNameArgument);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();

            if (fileName is null)
            {
                IReadOnlyList<string> files = await repository.ListFilesAsync(cancellationToken).ConfigureAwait(false);
                if (files.Count == 0)
                {
                    ConsoleReporter.Info("No files are tracked in the current environment.");
                    return;
                }

                bool interactive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
                fileName = SelectFile(
                    files,
                    interactive,
                    choices => AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a file to remove")
                            .EnableSearch()
                            .UseConverter(Markup.Escape)
                            .PageSize(10)
                            .AddChoices(choices)));

                bool confirmed = await ConfirmSelectedFileAsync(
                    fileName,
                    message => ConsoleReporter.ConfirmAsync(message, defaultValue: false, cancellationToken)).ConfigureAwait(false);
                if (!confirmed)
                {
                    ConsoleReporter.Info("File removal canceled.");
                    return;
                }

                fileName = Path.Combine(EnvManagerPaths.HomeDirectory.FullName, fileName);
            }

            await repository.RemoveFileAsync(fileName, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Staged file '{fileName}' for removal. Run 'EnvManager push' to commit and publish it.");
        }));
    }

    internal static string SelectFile(
        IReadOnlyList<string> files,
        bool interactive,
        Func<IReadOnlyList<string>, string> prompt)
    {
        if (!interactive)
        {
            throw new ConfigurationException("A file name is required when input is not interactive.");
        }

        return prompt(files);
    }

    internal static Task<bool> ConfirmSelectedFileAsync(string fileName, Func<string, Task<bool>> prompt)
        => prompt($"Stage file '{fileName}' for removal?");
}
