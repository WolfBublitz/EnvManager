// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellsListCommand.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Linq;
using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Shells;

/// <summary>Lists shells configured for the current environment.</summary>
public sealed class ShellsListCommand : Command
{
    /// <summary>Initializes a new instance of the <see cref="ShellsListCommand"/> class.</summary>
    public ShellsListCommand()
        : base("list", "Lists shells configured for the current environment.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (configuration.Shells.Count == 0)
            {
                ConsoleReporter.Info("No shells are configured for the current environment.");

                return;
            }

            foreach (string shell in configuration.Shells.OrderBy(shell => shell, System.StringComparer.OrdinalIgnoreCase))
            {
                ConsoleReporter.Info(shell);
            }
        }));
    }
}
