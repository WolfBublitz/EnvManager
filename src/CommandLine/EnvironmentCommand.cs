using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine;

/// <summary>Discovers available environment profiles.</summary>
public sealed class EnvironmentCommand : Command
{
    public EnvironmentCommand()
        : base("environment", "Discover environment profiles.")
    {
        Command list = new("list", "Lists local and remote environments.");
        list.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            string current = await repository.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> environments = await repository.ListEnvironmentsAsync(cancellationToken).ConfigureAwait(false);
            foreach (string environment in environments)
            {
                ConsoleReporter.Info(environment == current ? $"* {environment} (current)" : $"  {environment}");
            }
        }));
        this.Add(list);
    }
}
