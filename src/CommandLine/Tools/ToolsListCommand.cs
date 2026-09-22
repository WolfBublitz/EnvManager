// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsListCommand.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

using Spectre.Console;

namespace EnvManager.CommandLine.Tools;

/// <summary>
/// The <c>tools list</c> command lists all tools tracked for the current environment.
/// </summary>
public sealed class ToolsListCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="ToolsListCommand"/> class.</summary>
    public ToolsListCommand()
        : base("list", "Lists all tools and configurations.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (configuration.Tools.Count == 0)
            {
                ConsoleReporter.Info($"No tools are tracked for environment '{configuration.Name}'.");
                return;
            }

            Table table = new Table().Title($"Tools for environment '{configuration.Name}'")
                .AddColumn("Name")
                .AddColumn("Package Manager")
                .AddColumn("Version");

            foreach (ToolDefinition tool in configuration.Tools)
            {
                table.AddRow(tool.Name, tool.PackageManager ?? "(auto)", tool.Version ?? "(latest)");
            }

            AnsiConsole.Write(table);
        }));
    }
}
