// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SettingsCommand.cs                                                       │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

namespace EnvManager.CommandLine.Settings;

/// <summary>
/// The <c>settings</c> command groups subcommands for managing key-value settings
/// stored in the current environment's configuration.
/// </summary>
public sealed class SettingsCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SettingsCommand"/> class.</summary>
    public SettingsCommand()
        : base("settings", "Manage key-value settings for the current environment.")
    {
        this.Add(new SettingsEditCommand());
        this.Add(new SettingsRemoveCommand());
        this.Add(new SettingsListCommand());
    }
}
