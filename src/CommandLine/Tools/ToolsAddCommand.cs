// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsAddCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.PackageManagers;

using Spectre.Console;

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

        this.Add(toolNameArgument);
        this.Add(packageManagerOption);
        this.Add(versionOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string toolName = parseResult.GetValue(toolNameArgument)!;
            string? requestedPackageManager = parseResult.GetValue(packageManagerOption);
            string? version = parseResult.GetValue(versionOption);

            IPackageManager packageManager = requestedPackageManager is null
                ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                : PackageManagerFactory.GetByName(requestedPackageManager);

            await AnsiConsole.Status().StartAsync($"Installing '{toolName}' using '{packageManager.Name}'...", async _ =>
                await packageManager.InstallAsync(toolName, version, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            configuration.Tools.RemoveAll(tool => tool.Name == toolName);
            configuration.Tools.Add(new ToolDefinition { Name = toolName, PackageManager = packageManager.Name, Version = version });

            await repository.SaveConfigurationAsync(configuration, $"Add tool '{toolName}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Installed and recorded tool '{toolName}' (via '{packageManager.Name}').");
        }));
    }
}
