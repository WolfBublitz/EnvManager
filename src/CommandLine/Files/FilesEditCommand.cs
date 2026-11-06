// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesEditCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine.Files;

/// <summary>Opens a tracked file in the text editor configured for the current environment.</summary>
public sealed class FilesEditCommand : Command
{
    private const string EditorSettingKey = "editor";

    /// <summary>Initializes a new instance of the <see cref="FilesEditCommand"/> class.</summary>
    public FilesEditCommand()
        : base("edit", "Opens a tracked file in the configured text editor.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            IReadOnlyList<string> files = await repository.ListFilesAsync(cancellationToken).ConfigureAwait(false);

            if (files.Count == 0)
            {
                ConsoleReporter.Info("No files are tracked for the current environment.");
                return;
            }

            bool interactive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
            string selectedFile = await PromptForFileAsync(files, interactive, cancellationToken).ConfigureAwait(false);

            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            string editor;

            if (configuration.Settings.TryGetValue(EditorSettingKey, out string? configuredEditor)
                && !string.IsNullOrWhiteSpace(configuredEditor))
            {
                editor = configuredEditor;
            }
            else
            {
                editor = await PromptForEditorAsync(interactive, cancellationToken).ConfigureAwait(false);
                configuration.Settings[EditorSettingKey] = editor;
                await repository.SaveConfigurationAsync(configuration, "Set editor setting.", cancellationToken).ConfigureAwait(false);
            }

            string homeDirectory = EnvManagerPaths.HomeDirectory.FullName;
            string filePath = Path.GetFullPath(Path.Combine(homeDirectory, selectedFile));
            ProcessStartInfo startInfo = CreateEditorStartInfo(editor, filePath, homeDirectory);

            using Process process = new() { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    throw new ConfigurationException($"Could not start the configured editor '{editor}'.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new ConfigurationException($"Could not start the configured editor '{editor}': {exception.Message}", exception);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new ConfigurationException($"The configured editor '{editor}' exited with code {process.ExitCode}.");
            }
        }));
    }

    internal static ProcessStartInfo CreateEditorStartInfo(string editor, string filePath, string workingDirectory)
    {
        List<string> commandParts = ParseEditorCommand(editor);
        ProcessStartInfo startInfo = new(commandParts[0])
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        for (int index = 1; index < commandParts.Count; index++)
        {
            startInfo.ArgumentList.Add(commandParts[index]);
        }

        startInfo.ArgumentList.Add(filePath);

        return startInfo;
    }

    private static async Task<string> PromptForFileAsync(
        IReadOnlyList<string> files,
        bool interactive,
        CancellationToken cancellationToken)
    {
        if (interactive)
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a file to edit")
                    .PageSize(15)
                    .AddChoices(files));
        }

        ConsoleReporter.Info("Select a file to edit:");
        for (int index = 0; index < files.Count; index++)
        {
            ConsoleReporter.Info($"  {index + 1}. {files[index]}");
        }

        string? answer = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (!int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out int selection)
            || selection < 1
            || selection > files.Count)
        {
            throw new ConfigurationException("Enter the number of the file to edit.");
        }

        return files[selection - 1];
    }

    private static async Task<string> PromptForEditorAsync(bool interactive, CancellationToken cancellationToken)
    {
        if (interactive)
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>("Set the editor command in settings.editor (for example: code --wait)"));
        }

        ConsoleReporter.Warning("Set the editor command in settings.editor (for example: code --wait):");
        string? editor = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(editor))
        {
            throw new ConfigurationException("An editor is required. Set the editor command in settings.editor.");
        }

        return editor;
    }

    private static List<string> ParseEditorCommand(string editor)
    {
        List<string> parts = [];
        StringBuilder part = new();
        char quote = '\0';
        bool partStarted = false;

        foreach (char character in editor)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    part.Append(character);
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                partStarted = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (partStarted)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                    partStarted = false;
                }
            }
            else
            {
                part.Append(character);
                partStarted = true;
            }
        }

        if (quote != '\0')
        {
            throw new ConfigurationException("The configured editor command contains an unmatched quote.");
        }

        if (partStarted)
        {
            parts.Add(part.ToString());
        }

        if (parts.Count == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new ConfigurationException("The configured editor command is empty. Set the editor command in settings.editor.");
        }

        return parts;
    }
}
