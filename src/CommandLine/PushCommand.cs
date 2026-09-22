// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PushCommand.cs                                                          │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>push</c> command pushes the current environment's committed changes to the
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
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string environmentName = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Info($"Pushing environment '{environmentName}'...");

            await repository.PushAsync(cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Pushed environment '{environmentName}' to the remote repository.");
        }));
    }
}
