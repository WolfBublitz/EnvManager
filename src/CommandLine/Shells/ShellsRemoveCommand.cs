// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellsRemoveCommand.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.Shells;

namespace EnvManager.CommandLine.Shells;

/// <summary>Disables a configured shell and restores its backed-up profile files.</summary>
public sealed class ShellsRemoveCommand : Command
{
    /// <summary>Initializes a new instance of the <see cref="ShellsRemoveCommand"/> class.</summary>
    public ShellsRemoveCommand()
        : base("remove", "Disables a shell and restores its original profile files.")
    {
        Argument<string> shellNameArgument = new("shell_name")
        {
            Description = "The shell to disable: bash, zsh, fish, powershell, or cmd.",
        };

        this.Add(shellNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string shellName = parseResult.GetValue(shellNameArgument)!;
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            ShellProfileInstaller installer = new(EnvManagerPaths.HomeDirectory, OperatingSystem.IsWindows());
            string normalizedName = installer.NormalizeShellName(shellName);
            int removedCount = configuration.Shells.RemoveAll(shell => string.Equals(shell, normalizedName, StringComparison.OrdinalIgnoreCase));

            if (removedCount == 0)
            {
                throw new ConfigurationException($"Shell '{normalizedName}' is not configured in the current environment.");
            }

            installer.Uninstall(normalizedName);
            await repository.SaveConfigurationAsync(configuration, $"Disable shell '{normalizedName}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Disabled shell '{normalizedName}' and restored its profile files.");
        }));
    }
}
