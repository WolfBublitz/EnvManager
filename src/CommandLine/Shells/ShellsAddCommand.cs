// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellsAddCommand.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Shells;

namespace EnvManager.CommandLine.Shells;

/// <summary>Enables a supported shell and installs the profile loader for it.</summary>
public sealed class ShellsAddCommand : Command
{
    /// <summary>Initializes a new instance of the <see cref="ShellsAddCommand"/> class.</summary>
    public ShellsAddCommand()
        : base("add", "Enables a shell and installs its profile loader.")
    {
        Argument<string> shellNameArgument = new("shell_name")
        {
            Description = "The shell to enable: bash, zsh, fish, powershell, or cmd.",
        };

        this.Add(shellNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string shellName = parseResult.GetValue(shellNameArgument)!;
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            ShellProfileInstaller installer = new(EnvManagerPaths.HomeDirectory, OperatingSystem.IsWindows());
            string normalizedName = installer.NormalizeShellName(shellName);
            bool alreadyConfigured = configuration.Shells.Any(shell => string.Equals(shell, normalizedName, StringComparison.OrdinalIgnoreCase));

            installer.Install(normalizedName);

            if (alreadyConfigured)
            {
                ConsoleReporter.Info($"Shell '{normalizedName}' is already configured.");

                return;
            }

            configuration.Shells.Add(normalizedName);
            await repository.SaveConfigurationAsync(configuration, $"Enable shell '{normalizedName}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Enabled shell '{normalizedName}' and installed its profile loader.");
        }));
    }
}
