// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsUpdateCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

namespace EnvManager.CommandLine.Tools;

/// <summary>
/// The <c>tools update</c> command updates one tracked tool, or every tracked tool when
/// no name is given, using each tool's recorded package manager.
/// </summary>
public sealed class ToolsUpdateCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="ToolsUpdateCommand"/> class.</summary>
    public ToolsUpdateCommand()
        : base("update", "Updates an existing tool or configuration.")
    {
        Argument<string?> toolNameArgument = new("tool_name")
        {
            Description = "The name of the tool to update. Updates every tracked tool when omitted.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        this.Add(toolNameArgument);
        Option<bool> yesOption = new("--yes", "-y") { Description = "Run custom commands without prompting. Review the commands before using this option." };
        this.Add(yesOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string? toolName = parseResult.GetValue(toolNameArgument);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            List<ToolDefinition> toolsToUpdate = toolName is null
                ? configuration.Tools
                : [configuration.Tools.FirstOrDefault(candidate => candidate.Name == toolName)
                    ?? throw new ConfigurationException($"Tool '{toolName}' is not tracked in the current environment.")];

            if (toolsToUpdate.Count == 0)
            {
                ConsoleReporter.Info($"No tools are tracked for environment '{configuration.Name}'.");
                return;
            }

            if (toolsToUpdate.Count > 1 && toolsToUpdate.All(tool => tool.InstallCommand is null))
            {
                List<(ToolDefinition Tool, IPackageManager PackageManager)> packageOperations = [];
                foreach (ToolDefinition tool in toolsToUpdate)
                {
                    IPackageManager packageManager = tool.PackageManager is null
                        ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                        : PackageManagerFactory.GetByName(tool.PackageManager);
                    packageOperations.Add((tool, packageManager));
                }

                if (!OperatingSystem.IsLinux() || !packageOperations.Any(operation => operation.PackageManager.RequiresElevation))
                {
                    await ConsoleReporter.RunProgressAsync(
                        $"Updating tools in environment '{configuration.Name}'",
                        packageOperations,
                        operation => $"{operation.Tool.Name} via {operation.PackageManager.Name}",
                        operation => operation.PackageManager.UpdateAsync(operation.Tool.Name, cancellationToken)).ConfigureAwait(false);

                    foreach ((ToolDefinition tool, _) in packageOperations)
                    {
                        ConsoleReporter.Success($"Updated tool '{tool.Name}'.");
                    }

                    await repository.SaveConfigurationAsync(configuration, "Update tools.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            foreach (ToolDefinition tool in toolsToUpdate)
            {
                if (tool.InstallCommand is not null)
                {
                    await CustomToolCommand.RunAsync(tool.UpdateCommand, tool.Name, "update", parseResult.GetValue(yesOption), cancellationToken).ConfigureAwait(false);
                    ConsoleReporter.Success($"Updated tool '{tool.Name}'.");
                    continue;
                }

                IPackageManager packageManager = tool.PackageManager is null
                    ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                    : PackageManagerFactory.GetByName(tool.PackageManager);

                await ConsoleReporter.RunPackageOperationAsync(packageManager, $"Updating '{tool.Name}' using '{packageManager.Name}'...",
                    () => packageManager.UpdateAsync(tool.Name, cancellationToken)).ConfigureAwait(false);

                ConsoleReporter.Success($"Updated tool '{tool.Name}'.");
            }

            await repository.SaveConfigurationAsync(configuration, "Update tools.", cancellationToken).ConfigureAwait(false);
        }));
    }
}
