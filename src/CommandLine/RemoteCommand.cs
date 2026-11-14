using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Git;

namespace EnvManager.CommandLine;

/// <summary>Configures the repository used to synchronize environments.</summary>
public sealed class RemoteCommand : Command
{
    public RemoteCommand()
        : base("remote", "Manage the synchronization remote.")
    {
        Command set = new("set", "Add or change the origin remote.");
        Argument<string> url = new("url") { Description = "A Git remote URL or path." };
        set.Add(url);
        set.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string value = parseResult.GetValue(url)!;
            GitClient git = EnvironmentRepository.OpenExisting().Git;
            await git.SetRemoteAsync(value, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success("Configured the origin remote. Run 'EnvManager push' to publish the current environment.");
        }));
        this.Add(set);
    }
}
