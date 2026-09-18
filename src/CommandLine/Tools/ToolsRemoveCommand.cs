// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsRemoveCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

using Spectre.Console;

namespace EnvManager.CommandLine.Tools;

/// <summary>
/// The <c>tools remove</c> command uninstalls a tool using the package manager it was
/// installed with, then removes it from the current environment's configuration.
/// </summary>
public sealed class ToolsRemoveCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="ToolsRemoveCommand"/> class.</summary>
    public ToolsRemoveCommand()
        : base("remove", "Removes an existing tool or configuration.")
    {
        Argument<string> toolNameArgument = new("tool_name") { Description = "The name of the tool to remove." };

        this.Add(toolNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string toolName = parseResult.GetValue(toolNameArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            ToolDefinition tool = configuration.Tools.FirstOrDefault(candidate => candidate.Name == toolName)
                ?? throw new ConfigurationException($"Tool '{toolName}' is not tracked in the current environment.");

            IPackageManager packageManager = tool.PackageManager is null
                ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                : PackageManagerFactory.GetByName(tool.PackageManager);

            await AnsiConsole.Status().StartAsync($"Removing '{toolName}' using '{packageManager.Name}'...", async _ =>
                await packageManager.RemoveAsync(toolName, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

            configuration.Tools.Remove(tool);

            await repository.SaveConfigurationAsync(configuration, $"Remove tool '{toolName}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Removed tool '{toolName}'.");
        }));
    }
}
