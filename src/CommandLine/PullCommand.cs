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
/// environment from the remote repository with a fast-forward-only update.
/// </summary>
public sealed class PullCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="PullCommand"/> class.</summary>
    public PullCommand()
        : base("pull", "Fetches and fast-forwards the current environment without discarding local changes.")
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

            (string target, string incoming, string localChanges, string localCommits, IReadOnlyList<string> obstructions) =
                (string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
            await ConsoleReporter.RunWithStatusAsync(
                $"Fetching changes for environment '{environmentName}'",
                async () =>
                {
                    (target, incoming, localChanges, localCommits, obstructions) =
                        await repository.Git.PreviewPullAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);

            ConsoleReporter.Info($"Incoming changes (local commit to remote {target}):");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(incoming) ? "  None." : incoming);
            ConsoleReporter.Info("Uncommitted tracked changes (index and work tree):");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(localChanges) ? "  None." : localChanges);
            ConsoleReporter.Info("Local-only commits:");
            ConsoleReporter.Info(string.IsNullOrWhiteSpace(localCommits) ? "  None." : localCommits);
            if (obstructions.Count > 0)
            {
                ConsoleReporter.Warning("Incoming paths may conflict with existing files or directories:");
                foreach (string path in obstructions)
                {
                    ConsoleReporter.Warning($"  {path}");
                }
            }

            ConsoleReporter.Info("Pull only fast-forwards; Git will refuse conflicting changes and divergent histories.");
            if (!parseResult.GetValue(yesOption) && !await ConsoleReporter.ConfirmAsync(
                "Apply this pull?",
                defaultValue: false,
                cancellationToken).ConfigureAwait(false))
            {
                ConsoleReporter.Warning("Pull cancelled. No local changes were applied.");
                return;
            }

            await ConsoleReporter.RunWithStatusAsync(
                $"Applying changes to environment '{environmentName}'",
                async () =>
                {
                    await repository.Git.FastForwardPullAsync(target, cancellationToken).ConfigureAwait(false);
                    await repository.RefreshShellEnvironmentAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);

            ConsoleReporter.Success($"Pulled the latest changes for environment '{environmentName}'.");
        }));
    }

}
