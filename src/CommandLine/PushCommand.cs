// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PushCommand.cs                                                          │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;

using Spectre.Console;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>push</c> command lists all pending changes to tracked files, asks for confirmation,
/// commits them with an auto-generated message and pushes the current environment to the
/// remote repository.
/// </summary>
public sealed class PushCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="PushCommand"/> class.</summary>
    public PushCommand()
        : base("push", "Pushes the current environment configuration to the remote repository.")
    {
        Option<bool> yesOption = new("--yes", "-y")
        {
            Description = "Commit and push without asking for confirmation.",
        };

        this.Add(yesOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            bool skipConfirmation = parseResult.GetValue(yesOption);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<(string Status, string Path)> changes = await repository.GetPendingChangesAsync(cancellationToken).ConfigureAwait(false);

            if (changes.Count > 0)
            {
                ConsoleReporter.Info($"The following changes will be committed to environment '{environmentName}':");

                foreach ((string status, string path) in changes)
                {
                    string label = status switch
                    {
                        "A" => "added",
                        "D" => "deleted",
                        _ => "modified",
                    };

                    AnsiConsole.MarkupLineInterpolated($"  [grey]{label,-9}[/] {path}");
                }

                if (!skipConfirmation && !AnsiConsole.Confirm("Commit these changes and push?", defaultValue: false))
                {
                    ConsoleReporter.Warning("Push cancelled. No changes were committed.");
                    return;
                }
            }
            else if (!skipConfirmation && !AnsiConsole.Confirm($"No uncommitted changes. Push environment '{environmentName}' to the remote repository?", defaultValue: true))
            {
                ConsoleReporter.Warning("Push cancelled.");
                return;
            }

            ConsoleReporter.Info($"Pushing environment '{environmentName}'...");

            await repository.PushAsync(changes, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Pushed environment '{environmentName}' to the remote repository.");
        }));
    }
}
