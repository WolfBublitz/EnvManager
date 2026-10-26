// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellsAddCommand.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;
using System.Collections.Generic;
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

            IReadOnlyList<string> profilePaths = installer.Install(normalizedName);
            bool addedProfile = false;

            foreach (string profilePath in profilePaths)
            {
                if (!await repository.Git.IsTrackedAsync(profilePath, cancellationToken).ConfigureAwait(false))
                {
                    await repository.Git.AddAsync(profilePath, cancellationToken).ConfigureAwait(false);
                    addedProfile = true;
                }
            }

            if (!alreadyConfigured)
            {
                configuration.Shells.Add(normalizedName);
                await repository.SaveConfigurationAsync(configuration, $"Enable shell '{normalizedName}'.", cancellationToken).ConfigureAwait(false);
            }
            else if (addedProfile)
            {
                await repository.Git.CommitAsync($"Add shell '{normalizedName}' profiles.", cancellationToken).ConfigureAwait(false);
            }

            if (addedProfile)
            {
                await repository.PushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (alreadyConfigured)
            {
                ConsoleReporter.Info($"Shell '{normalizedName}' is already configured.");
            }
            else
            {
                ConsoleReporter.Success($"Enabled shell '{normalizedName}' and installed its profile loader.");
            }
        }));
    }
}
