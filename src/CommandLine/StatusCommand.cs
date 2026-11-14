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
                return;
            }

            ConsoleReporter.Info("Remote: origin");
            await ReportRemoteStateAsync(repository.Git, environment, cancellationToken).ConfigureAwait(false);
        }));
    }

    private static async Task ReportRemoteStateAsync(GitClient git, string environment, CancellationToken cancellationToken)
    {
        GitCommandResult remote = await git.RunAsync(["ls-remote", "--heads", "origin", $"refs/heads/{environment}"], cancellationToken).ConfigureAwait(false);
        if (!remote.Succeeded)
        {
            ConsoleReporter.Warning("Could not check the remote synchronization state. Check your connection and Git credentials.");
            return;
        }

        string line = remote.StandardOutput.Trim();
        if (line.Length == 0)
        {
            ConsoleReporter.Info("The environment has not been published. Run 'EnvManager push'.");
            return;
        }

        string remoteCommit = line.Split('\t')[0];
        GitCommandResult counts = await git.RunAsync(["rev-list", "--left-right", "--count", $"HEAD...{remoteCommit}"], cancellationToken).ConfigureAwait(false);
        if (!counts.Succeeded)
        {
            ConsoleReporter.Warning("Could not compare the remote commit with local history. Run 'EnvManager pull' to fetch and inspect it.");
            return;
        }

        string[] values = counts.StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2 || !int.TryParse(values[0], out int ahead) || !int.TryParse(values[1], out int behind))
        {
            ConsoleReporter.Warning("Git returned an unexpected synchronization status.");
            return;
        }

        ConsoleReporter.Info($"Synchronization: {ahead} local commit(s) ahead, {behind} remote commit(s) behind.");
        if (ahead > 0 && behind > 0)
        {
            ConsoleReporter.Warning("Local and remote histories have diverged; reconcile them before pulling or pushing.");
        }
        else if (ahead > 0)
        {
            ConsoleReporter.Info("Run 'EnvManager push' to publish local commits.");
        }
        else if (behind > 0)
        {
            ConsoleReporter.Info("Run 'EnvManager pull' to inspect incoming changes.");
        }
    }
}
