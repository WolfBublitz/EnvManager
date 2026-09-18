// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: VariableListCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;

using Spectre.Console;

namespace EnvManager.CommandLine.Variable;

/// <summary>
/// The <c>variable list</c> command lists all environment variables configured for the
/// current environment as a table.
/// </summary>
public sealed class VariableListCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="VariableListCommand"/> class.</summary>
    public VariableListCommand()
        : base("list", "Lists all environment variables and configurations.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (configuration.Variables.Count == 0)
            {
                ConsoleReporter.Info($"No variables are configured for environment '{configuration.Name}'.");
                return;
            }

            Table table = new Table().Title($"Variables for environment '{configuration.Name}'").AddColumn("Name").AddColumn("Value");

            foreach ((string name, string value) in configuration.Variables.OrderBy(pair => pair.Key))
            {
                table.AddRow(name, value);
            }

            AnsiConsole.Write(table);
        }));
    }
}
