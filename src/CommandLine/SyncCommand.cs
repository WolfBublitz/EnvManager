using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.Git;

namespace EnvManager.CommandLine;

/// <summary>Safely pulls and pushes the current environment as one workflow.</summary>
public sealed class SyncCommand : Command
{
    public SyncCommand()
        : base("sync", "Safely pulls and pushes the current environment.")
    {
        Option<bool> yesOption = new("--yes", "-y")
        {
            Description = "Apply safe pull and push operations without asking for confirmation.",
        };
        Option<bool> dryRunOption = new("--dry-run")
        {
            Description = "Fetch and preview the synchronization without applying changes.",
        };
        Option<bool> pullOnlyOption = new("--pull-only")
        {
            Description = "Only pull remote changes.",
        };
        Option<bool> pushOnlyOption = new("--push-only")
        {
            Description = "Only push local commits and tracked changes.",
        };
        this.Add(yesOption);
        this.Add(dryRunOption);
        this.Add(pullOnlyOption);
        this.Add(pushOnlyOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            bool pullOnly = parseResult.GetValue(pullOnlyOption);
            bool pushOnly = parseResult.GetValue(pushOnlyOption);
            if (pullOnly && pushOnly)
            {
                throw new ConfigurationException("Use either '--pull-only' or '--push-only', not both.");
            }

            bool skipConfirmation = parseResult.GetValue(yesOption);
            bool dryRun = parseResult.GetValue(dryRunOption);
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);

            (bool remoteConfigured, bool branchPublished, int ahead, int behind, bool diverged, bool unknown) =
                await repository.Git.GetRemoteSyncStateAsync(environmentName, cancellationToken).ConfigureAwait(false);
            if (!remoteConfigured)
            {
                throw new ConfigurationException("No 'origin' remote is configured. Run 'EnvManager remote set <url>' before syncing.");
            }
            if (unknown)
            {
                throw new ConfigurationException("Could not verify the remote synchronization state. Check your connection and Git credentials.");
            }
            if (diverged)
            {
                throw new ConfigurationException("The local and remote histories have diverged. Reconcile them before syncing.");
            }

            if (!pushOnly)
            {
                (string target, string incoming, string localChanges, string localCommits, IReadOnlyList<string> obstructions) =
                    await ConsoleReporter.RunWithStatusAsync(
                        $"Previewing synchronization for '{environmentName}'",
                        async () => await repository.Git.PreviewPullAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

                ConsoleReporter.Info(string.IsNullOrWhiteSpace(incoming) ? "Incoming changes: none." : $"Incoming changes:\n{incoming}");
                ConsoleReporter.Info(string.IsNullOrWhiteSpace(localChanges) ? "Local uncommitted changes: none." : $"Local uncommitted changes:\n{localChanges}");
                ConsoleReporter.Info(string.IsNullOrWhiteSpace(localCommits) ? "Local-only commits: none." : $"Local-only commits:\n{localCommits}");
                if (obstructions.Count > 0)
                {
                    throw new ConfigurationException(
                        $"Pull stopped; these paths may conflict: {string.Join(", ", obstructions)}");
                }

                GitCommandResult head = await repository.Git.RunAsync(
                    ["rev-parse", "--verify", "HEAD"],
                    cancellationToken).ConfigureAwait(false);
                bool incomingChanges = head.Succeeded
                    && !string.Equals(head.StandardOutput.Trim(), target, StringComparison.Ordinal);

                if (incomingChanges)
                {
                    if (dryRun)
                    {
                        ConsoleReporter.Info("Dry run: remote changes would be pulled.");
                    }
                    else if (skipConfirmation || await ConsoleReporter.ConfirmAsync(
                        "Apply the incoming changes with a fast-forward pull?",
                        defaultValue: false,
                        cancellationToken).ConfigureAwait(false))
                    {
                        await ConsoleReporter.RunWithStatusAsync(
                            $"Pulling environment '{environmentName}'",
                            async () =>
                            {
                                await repository.Git.FastForwardPullAsync(target, cancellationToken).ConfigureAwait(false);
                                await repository.RefreshShellEnvironmentAsync(cancellationToken).ConfigureAwait(false);
                            }).ConfigureAwait(false);
                        ConsoleReporter.Success($"Pulled environment '{environmentName}'.");
                    }
                    else
                    {
                        ConsoleReporter.Warning("Pull skipped.");
                        if (!skipConfirmation)
                        {
                            return;
                        }
                    }
                }
            }

            if (pullOnly)
            {
                if (dryRun)
                {
                    ConsoleReporter.Info("Dry run: no changes were applied.");
                }
                return;
            }

            IReadOnlyList<(string Status, string Path)> pendingChanges =
                await repository.GetPendingChangesAsync(cancellationToken).ConfigureAwait(false);
            (remoteConfigured, branchPublished, ahead, _, diverged, unknown) =
                await repository.Git.GetRemoteSyncStateAsync(environmentName, cancellationToken).ConfigureAwait(false);

            if (unknown)
            {
                throw new ConfigurationException("Could not verify the remote synchronization state after synchronization.");
            }
            if (diverged)
            {
                throw new ConfigurationException("The local and remote histories have diverged. Reconcile them before syncing.");
            }

            if (pendingChanges.Count == 0 && branchPublished && ahead == 0)
            {
                ConsoleReporter.Success($"Environment '{environmentName}' is synchronized.");
                return;
            }
            if (dryRun)
            {
                ConsoleReporter.Info("Dry run: local changes would be committed and pushed.");
                return;
            }
            if (!skipConfirmation && pendingChanges.Count > 0
                && !await ConsoleReporter.ConfirmAsync(
                    "Commit local changes and push the environment?",
                    defaultValue: false,
                    cancellationToken).ConfigureAwait(false))
            {
                ConsoleReporter.Warning("Push skipped. No changes were committed.");
                return;
            }

            await ConsoleReporter.RunWithStatusAsync(
                $"Pushing environment '{environmentName}'",
                () => repository.PushAsync(pendingChanges, cancellationToken)).ConfigureAwait(false);
            ConsoleReporter.Success($"Pushed environment '{environmentName}' to the remote repository.");
        }));
    }
}
