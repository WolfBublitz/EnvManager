// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsUpdateCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

using Spectre.Console;

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

            foreach (ToolDefinition tool in toolsToUpdate)
            {
                IPackageManager packageManager = tool.PackageManager is null
                    ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                    : PackageManagerFactory.GetByName(tool.PackageManager);

                await AnsiConsole.Status().StartAsync($"Updating '{tool.Name}' using '{packageManager.Name}'...", async _ =>
                    await packageManager.UpdateAsync(tool.Name, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

                ConsoleReporter.Success($"Updated tool '{tool.Name}'.");
            }

            await repository.SaveConfigurationAsync(configuration, "Update tools.", cancellationToken).ConfigureAwait(false);
        }));
    }
}
