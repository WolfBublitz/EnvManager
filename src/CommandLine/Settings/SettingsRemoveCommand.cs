// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsRemoveCommand.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

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
        : base("remove", "Removes an existing setting, prompting for its key when omitted.")
    {
        Argument<string?> keyArgument = new("setting_key")
        {
            Description = "The key of the setting to remove.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        this.Add(keyArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            string? key = parseResult.GetValue(keyArgument);

            if (key is null)
            {
                string[] configuredKeys = configuration.Settings.Keys.OrderBy(settingKey => settingKey).ToArray();
                if (configuredKeys.Length == 0)
                {
                    ConsoleReporter.Info($"No settings are configured for environment '{configuration.Name}'.");
                    return;
                }

                key = await SettingsPrompt.SelectKeyAsync(
                    "Select a setting key to remove",
                    configuredKeys,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!configuration.Settings.Remove(key))
            {
                throw new ConfigurationException($"Setting '{key}' does not exist in the current environment.");
            }

            await repository.SaveConfigurationAsync(configuration, $"Remove setting '{key}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Removed setting '{key}'.");
        }));
    }
}
