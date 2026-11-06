// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsRemoveCommand.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;

namespace EnvManager.CommandLine.Settings;

/// <summary>The <c>settings remove</c> command removes an existing setting.</summary>
public sealed class SettingsRemoveCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SettingsRemoveCommand"/> class.</summary>
    public SettingsRemoveCommand()
        : base("remove", "Removes an existing setting.")
    {
        Argument<string> keyArgument = new("setting_key") { Description = "The key of the setting to remove." };

        this.Add(keyArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string key = parseResult.GetValue(keyArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (!configuration.Settings.Remove(key))
            {
                throw new ConfigurationException($"Setting '{key}' does not exist in the current environment.");
            }

            await repository.SaveConfigurationAsync(configuration, $"Remove setting '{key}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Removed setting '{key}'.");
        }));
    }
}
