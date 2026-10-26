// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsAddCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

namespace EnvManager.CommandLine.Tools;

/// <summary>
/// The <c>tools add</c> command installs a tool using the detected (or explicitly
/// requested) package manager, then records it in the current environment's configuration.
/// </summary>
public sealed class ToolsAddCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="ToolsAddCommand"/> class.</summary>
    public ToolsAddCommand()
        : base("add", "Adds a new tool or configuration.")
    {
        Argument<string> toolNameArgument = new("tool_name") { Description = "The name of the tool to add." };
        Option<string?> packageManagerOption = new("--package-manager", "-p") { Description = "The package manager to use (e.g. apt, brew, winget). Auto-detected when omitted." };
        Option<string?> versionOption = new("--version", "-v") { Description = "The specific version to install. Defaults to the latest version." };
        Option<string?> installCommandOption = new("--install-command") { Description = "Shell command used to install the tool instead of a package manager." };
        Option<string?> updateCommandOption = new("--update-command") { Description = "Shell command used to update a custom tool." };
        Option<string?> removeCommandOption = new("--remove-command") { Description = "Shell command used to uninstall a custom tool." };

        this.Add(toolNameArgument);
        this.Add(packageManagerOption);
        this.Add(versionOption);
        this.Add(installCommandOption);
        this.Add(updateCommandOption);
        this.Add(removeCommandOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string toolName = parseResult.GetValue(toolNameArgument)!;
            string? requestedPackageManager = parseResult.GetValue(packageManagerOption);
            string? version = parseResult.GetValue(versionOption);
            string? installCommand = parseResult.GetValue(installCommandOption);
            string? updateCommand = parseResult.GetValue(updateCommandOption);
            string? removeCommand = parseResult.GetValue(removeCommandOption);

            if ((installCommand is not null && (requestedPackageManager is not null || version is not null)) ||
                (installCommand is null && (updateCommand is not null || removeCommand is not null)))
            {
                throw new ConfigurationException("--install-command cannot be combined with --package-manager or --version; --update-command and --remove-command require --install-command.");
            }

            if ((installCommand is not null && string.IsNullOrWhiteSpace(installCommand)) ||
                (updateCommand is not null && string.IsNullOrWhiteSpace(updateCommand)) ||
                (removeCommand is not null && string.IsNullOrWhiteSpace(removeCommand)))
            {
                throw new ConfigurationException("Custom tool commands cannot be empty.");
            }

            EnvironmentRepository repository;
            EnvironmentConfiguration configuration;
            ToolDefinition definition = new() { Name = toolName };
            if (installCommand is not null)
            {
                repository = EnvironmentRepository.OpenExisting();
                configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                await CustomToolCommand.RunAsync(installCommand, toolName, "install", cancellationToken).ConfigureAwait(false);
                definition.InstallCommand = installCommand;
                definition.UpdateCommand = updateCommand;
                definition.RemoveCommand = removeCommand;
            }
            else
            {
                IPackageManager packageManager = requestedPackageManager is null
                    ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                    : PackageManagerFactory.GetByName(requestedPackageManager);

                await ConsoleReporter.RunPackageOperationAsync(packageManager, $"Installing '{toolName}' using '{packageManager.Name}'...",
                    () => packageManager.InstallAsync(toolName, version, cancellationToken)).ConfigureAwait(false);
                definition.PackageManager = packageManager.Name;
                definition.Version = version;
                repository = EnvironmentRepository.OpenExisting();
                configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            }

            configuration.Tools.RemoveAll(tool => tool.Name == toolName);
            configuration.Tools.Add(definition);

            await repository.SaveConfigurationAsync(configuration, $"Add tool '{toolName}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Installed and recorded tool '{toolName}' (via '{definition.PackageManager ?? "custom command"}').");
        }));
    }
}
