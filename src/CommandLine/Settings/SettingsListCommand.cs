// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsListCommand.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;

using Spectre.Console;

namespace EnvManager.CommandLine.Settings;

/// <summary>The <c>settings list</c> command lists settings configured for the current environment.</summary>
public sealed class SettingsListCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SettingsListCommand"/> class.</summary>
    public SettingsListCommand()
        : base("list", "Lists all settings for the current environment.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (configuration.Settings.Count == 0)
            {
                ConsoleReporter.Info($"No settings are configured for environment '{configuration.Name}'.");
                return;
            }

            Table table = new Table().Title($"Settings for environment '{configuration.Name}'").AddColumn("Key").AddColumn("Value");

            foreach ((string key, string value) in configuration.Settings.OrderBy(pair => pair.Key))
            {
                table.AddRow(key, value);
            }

            AnsiConsole.Write(table);
        }));
    }
}
