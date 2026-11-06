// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsEditCommand.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Settings;

/// <summary>
/// The <c>settings edit</c> command selects a supported setting key and edits its value.
/// </summary>
public sealed class SettingsEditCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SettingsEditCommand"/> class.</summary>
    public SettingsEditCommand()
        : base("edit", "Selects a supported setting key and edits its value.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            string key = await SettingsPrompt.SelectKeyAsync(
                "Select a setting key to edit",
                ["editor"],
                cancellationToken).ConfigureAwait(false);
            string value = await SettingsPrompt.ReadValueAsync(
                $"Enter a value for setting '{key}'",
                cancellationToken).ConfigureAwait(false);

            bool isUpdate = configuration.Settings.ContainsKey(key);
            configuration.Settings[key] = value;

            await repository.SaveConfigurationAsync(configuration, $"Edit setting '{key}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success(isUpdate ? $"Updated setting '{key}'." : $"Set setting '{key}'.");
        }));
    }
}
