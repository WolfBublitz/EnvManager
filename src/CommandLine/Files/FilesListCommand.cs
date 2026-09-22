// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesListCommand.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;

using Spectre.Console;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files list</c> command lists all files tracked in the environment repository
/// for the currently checked-out environment.
/// </summary>
public sealed class FilesListCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesListCommand"/> class.</summary>
    public FilesListCommand()
        : base("list", "Lists all files and configurations in the repository.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> files = await repository.ListFilesAsync(cancellationToken).ConfigureAwait(false);

            if (files.Count == 0)
            {
                ConsoleReporter.Info($"No files are tracked for environment '{environmentName}'.");
                return;
            }

            Table table = new Table().Title($"Files for environment '{environmentName}'")
                .AddColumn("Path");

            foreach (string file in files)
            {
                table.AddRow(file);
            }

            AnsiConsole.Write(table);
        }));
    }
}
