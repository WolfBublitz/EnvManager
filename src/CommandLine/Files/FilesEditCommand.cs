// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesEditCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-08                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files edit</c> command lets the user select a tracked file and opens it in
/// the editor configured by the current environment.
/// </summary>
public sealed class FilesEditCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesEditCommand"/> class.</summary>
    public FilesEditCommand()
        : base("edit", "Selects a tracked file and opens it in the configured editor.")
    {
        Argument<string?> fileArgument = new("file")
        {
            Description = "A tracked file, relative to the current directory. Omit to select interactively.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        this.Add(fileArgument);
        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            IReadOnlyList<string> files = await repository.ListFilesAsync(cancellationToken).ConfigureAwait(false);

            if (files.Count == 0)
            {
                ConsoleReporter.Info("No files are tracked in the current environment.");
                return;
            }

            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            if (!configuration.Settings.TryGetValue("editor", out string? editor) || string.IsNullOrWhiteSpace(editor))
            {
                throw new ConfigurationException("No editor is configured. Run 'EnvManager settings edit' to set the 'editor' setting.");
            }

            string? requestedFile = parseResult.GetValue(fileArgument);
            string fileName;
            if (requestedFile is null)
            {
                bool interactive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
                fileName = SelectFile(
                    files,
                    interactive,
                    choices => AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a file to edit")
                            .EnableSearch()
                            .UseConverter(Markup.Escape)
                            .PageSize(10)
                            .AddChoices(choices)));
            }
            else
            {
                fileName = EnvironmentRepository.ResolveRelativePath(requestedFile, Environment.CurrentDirectory);
                if (!files.Contains(fileName, StringComparer.Ordinal))
                {
                    throw new ConfigurationException($"File '{fileName}' is not tracked in the current environment.");
                }
            }

            string filePath = Path.GetFullPath(Path.Combine(
                EnvManagerPaths.HomeDirectory.FullName,
                fileName.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(filePath))
            {
                throw new ConfigurationException($"Tracked file '{fileName}' was not found at '{filePath}'.");
            }

            await OpenInEditorAsync(editor.Trim(), filePath, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Starts the configured editor with the selected file and waits for it to exit.</summary>
    /// <param name="editor">The configured editor executable.</param>
    /// <param name="filePath">The full path of the file to open.</param>
    /// <param name="cancellationToken">A token that cancels waiting for the editor to exit.</param>
    /// <exception cref="EnvManagerException">The editor could not be started or exited with a non-zero code.</exception>
    internal static async Task OpenInEditorAsync(string editor, string filePath, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = CreateEditorStartInfo(editor, filePath);
        using Process process = Process.Start(startInfo)
            ?? throw new EnvManagerException($"Could not start editor '{editor}'.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new EnvManagerException($"Editor '{editor}' exited with code {process.ExitCode}.");
        }
    }

    /// <summary>Creates process arguments without interpreting the configured editor as a shell command.</summary>
    /// <param name="editor">The configured editor executable.</param>
    /// <param name="filePath">The full path of the file to open.</param>
    /// <returns>The process start information for the editor.</returns>
    /// <exception cref="ConfigurationException">The configured editor is empty.</exception>
    internal static ProcessStartInfo CreateEditorStartInfo(string editor, string filePath)
    {
        if (string.IsNullOrWhiteSpace(editor))
        {
            throw new ConfigurationException("No editor is configured. Run 'EnvManager settings edit' to set the 'editor' setting.");
        }

        ProcessStartInfo startInfo = new(editor)
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(filePath);

        return startInfo;
    }
}
