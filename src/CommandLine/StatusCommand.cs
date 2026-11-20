using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Configuration;
using EnvManager.Git;

namespace EnvManager.CommandLine;

/// <summary>Shows the current environment and its local and remote state.</summary>
public sealed class StatusCommand : Command
{
    public StatusCommand()
        : base("status", "Shows the current environment and synchronization state.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            if (!EnvironmentRepository.IsInitialized)
            {
                ConsoleReporter.Info("No environment is configured. Run 'EnvManager init' or 'EnvManager clone <url>'.");
                return;
            }

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environment = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<(string Status, string Path)> changes = await repository.GetPendingChangesAsync(cancellationToken).ConfigureAwait(false);
            ConsoleReporter.Info($"Environment: {environment}");
            ConsoleReporter.Info(changes.Count == 0 ? "Pending tracked changes: none" : $"Pending tracked changes: {changes.Count}");
            foreach ((string status, string path) in changes)
            {
                ConsoleReporter.Info($"  {status} {path}");
            }

            if (!await repository.Git.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
            {
                ConsoleReporter.Info("Remote: not configured. Run 'EnvManager remote set <url>' to connect one.");
                ConsoleReporter.Info(changes.Count == 0
                    ? "Next action: configure the remote, then run 'EnvManager push'."
                    : "Next action: configure the remote and commit/push these changes with 'EnvManager push'.");
                return;
            }

            ConsoleReporter.Info("Remote: origin");
            await ReportRemoteStateAsync(repository.Git, environment, cancellationToken).ConfigureAwait(false);
        }));
    }

    private static async Task ReportRemoteStateAsync(GitClient git, string environment, CancellationToken cancellationToken)
    {
        (bool remoteConfigured, bool branchPublished, int ahead, int behind, bool diverged, bool unknown) =
            await git.GetRemoteSyncStateAsync(environment, cancellationToken).ConfigureAwait(false);
        if (!remoteConfigured)
        {
            ConsoleReporter.Info("Remote: not configured. Run 'EnvManager remote set <url>' to connect one.");
            return;
        }
        if (unknown)
        {
            ConsoleReporter.Warning("Could not check the remote synchronization state. Check your connection and Git credentials.");
            return;
        }
        if (!branchPublished)
        {
            ConsoleReporter.Info("The environment has not been published. Run 'EnvManager push'.");
            return;
        }

        ConsoleReporter.Info($"Synchronization: {ahead} local commit(s) ahead, {behind} remote commit(s) behind.");
        if (diverged)
        {
            ConsoleReporter.Warning("Local and remote histories have diverged; reconcile them before pulling or pushing.");
            ConsoleReporter.Info("Next action: resolve the divergent histories with 'EnvManager sync' after reviewing them.");
        }
        else if (ahead > 0)
        {
            ConsoleReporter.Info("Run 'EnvManager push' to publish local commits.");
            ConsoleReporter.Info("Next action: 'EnvManager sync' will push safely.");
        }
        else if (behind > 0)
        {
            ConsoleReporter.Info("Run 'EnvManager pull' to inspect incoming changes.");
            ConsoleReporter.Info("Next action: 'EnvManager sync' will pull safely.");
        }
        else
        {
            ConsoleReporter.Success("Environment is synchronized.");
            ConsoleReporter.Info("Next action: 'EnvManager tools apply --preview' to review machine setup.");
        }
    }
}
