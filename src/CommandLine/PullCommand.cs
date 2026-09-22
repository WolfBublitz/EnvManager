// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PullCommand.cs                                                          │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>pull</c> command pulls the latest committed changes for the current
/// environment from the remote repository.
/// </summary>
public sealed class PullCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="PullCommand"/> class.</summary>
    public PullCommand()
        : base("pull", "Pulls the latest environment configuration from the remote repository.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Info($"Pulling environment '{environmentName}'...");

            await repository.PullAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Pulled the latest changes for environment '{environmentName}'.");
        }));
    }
}
