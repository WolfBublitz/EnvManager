using System;
using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

using Spectre.Console;

namespace EnvManager.CommandLine.Tools;

/// <summary>Reviews and selectively installs tools recorded in an environment.</summary>
public sealed class ToolsApplyCommand : Command
{
    public ToolsApplyCommand()
        : base("apply", "Review and selectively install tools from this environment.")
    {
        Option<bool> previewOption = new("--preview") { Description = "Show recorded installation actions without running them." };
        this.Add(previewOption);
        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            EnvironmentConfiguration configuration = await EnvironmentRepository.OpenExisting()
                .LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
            if (configuration.Tools.Count == 0)
            {
                ConsoleReporter.Info("No tools are recorded in this environment.");
                return;
            }

            ConsoleReporter.Info("Recorded installation actions (installed state is not checked):");
            foreach (ToolDefinition tool in configuration.Tools)
            {
                string action = tool.InstallCommand is not null
                    ? $"custom command: {tool.InstallCommand}"
                    : $"package manager: {tool.PackageManager ?? "auto-detect"}, version: {tool.Version ?? "latest"}";
                ConsoleReporter.Info($"  {tool.Name}: {action}");
            }

            if (parseResult.GetValue(previewOption))
            {
                return;
            }
            if (!AnsiConsole.Profile.Capabilities.Interactive || Console.IsInputRedirected)
            {
                throw new ConfigurationException("Selecting tools requires an interactive terminal. Use 'tools apply --preview' to review actions.");
            }

            List<ToolDefinition> selected = AnsiConsole.Prompt(new MultiSelectionPrompt<ToolDefinition>()
                .Title("Select tools to install")
                .InstructionsText("[grey]Space selects; Enter confirms.[/]")
                .UseConverter(tool => Markup.Escape(tool.Name))
                .AddChoices(configuration.Tools));

            foreach (ToolDefinition tool in selected)
            {
                if (tool.InstallCommand is not null)
                {
                    await CustomToolCommand.RunAsync(tool.InstallCommand, tool.Name, "install", assumeYes: false, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    IPackageManager manager = tool.PackageManager is null
                        ? await PackageManagerFactory.DetectAsync(cancellationToken).ConfigureAwait(false)
                        : PackageManagerFactory.GetByName(tool.PackageManager);
                    if (!await ConsoleReporter.ConfirmAsync(
                        $"Install '{tool.Name}' using '{manager.Name}' (version: {tool.Version ?? "latest"})?",
                        defaultValue: false, cancellationToken).ConfigureAwait(false))
                    {
                        ConsoleReporter.Info($"Skipped '{tool.Name}'.");
                        continue;
                    }

                    await ConsoleReporter.RunPackageOperationAsync(manager, $"Installing '{tool.Name}' using '{manager.Name}'...",
                        () => manager.InstallAsync(tool.Name, tool.Version, cancellationToken)).ConfigureAwait(false);
                }

                ConsoleReporter.Success($"Installed '{tool.Name}'.");
            }
        }));
    }
}
