// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsSetCommand.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Settings;

/// <summary>
/// The <c>settings set</c> command adds or overwrites a key-value setting in the
/// current environment's configuration.
/// </summary>
public sealed class SettingsSetCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SettingsSetCommand"/> class.</summary>
    public SettingsSetCommand()
        : base("set", "Sets a new setting or overwrites an existing setting.")
    {
        Argument<string> keyArgument = new("setting_key") { Description = "The key of the setting to set." };
        Argument<string> valueArgument = new("setting_value") { Description = "The value of the setting to set." };

        this.Add(keyArgument);
        this.Add(valueArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string key = parseResult.GetValue(keyArgument)!;
            string value = parseResult.GetValue(valueArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            bool isUpdate = configuration.Settings.ContainsKey(key);
            configuration.Settings[key] = value;

            await repository.SaveConfigurationAsync(configuration, $"Set setting '{key}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success(isUpdate ? $"Updated setting '{key}'." : $"Set setting '{key}'.");
        }));
    }
}
