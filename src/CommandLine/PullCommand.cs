// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PullCommand.cs                                                          │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;


namespace EnvManager.CommandLine;

/// <summary>
/// The <c>pull</c> command pulls the latest committed changes for the current
/// environment from the remote repository, replacing local changes.
/// </summary>
public sealed class PullCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="PullCommand"/> class.</summary>
    public PullCommand()
        : base("pull", "Discards local changes and replaces the current environment with the remote version.")
    {
        Option<bool> yesOption = new("--yes", "-y")
        {
            Description = "Apply the pull without asking for confirmation.",
        };
        this.Add(yesOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Info($"Fetching changes for environment '{environmentName}'...");
            (string target, string incoming, string localChanges, string localCommits, IReadOnlyList<string> obstructions) =
                await repository.Git.PreviewPullAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Info($"Incoming changes (local commit to remote {target}):");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(incoming) ? "  None." : incoming);
            ConsoleReporter.Info("Uncommitted tracked changes to discard (index and work tree):");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(localChanges) ? "  None." : localChanges);
            ConsoleReporter.Info("Local-only commits to discard:");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(localCommits) ? "  None." : localCommits);
            if (obstructions.Count > 0)
            {
                ConsoleReporter.Warning("Existing files or directories at incoming paths may be overwritten or removed:");
                foreach (string path in obstructions)
                {
                    ConsoleReporter.Warning($"  {path}");
                }
            }

            ConsoleReporter.Warning("Pull replaces the local branch, index and work tree without a backup.");
            if (!parseResult.GetValue(yesOption))
            {
                ConsoleReporter.Info("Apply this pull? [y/N]");
                string? answer = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleReporter.Warning("Pull cancelled. No local changes were applied.");
                    return;
                }
            }

            await repository.Git.ApplyPullAsync(target, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Pulled the latest changes for environment '{environmentName}'.");
        }));
    }
}
