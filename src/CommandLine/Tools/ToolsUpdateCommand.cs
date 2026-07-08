using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class ToolsUpdateCommand : BaseCommand
{
    private readonly ConfigurationService configurationService;
    private readonly Logger logger;

    private readonly IPackageManager packageManager;

    public ToolsUpdateCommand(ConfigurationService configurationService, IPackageManager packageManager, Logger logger)
        : base("update", "Update an existing tool in the environment")
    {
        this.configurationService = configurationService;
        this.packageManager = packageManager;
        this.logger = logger;
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await AnsiConsole.Status().StartAsync("Updating tools...", async ctx =>
        {
            Configuration configuration = configurationService.Configuration;
    
            foreach (string toolName in configuration.Tools)
            {
                try
                {
                    await packageManager.UpdatePackageAsync(toolName).ConfigureAwait(false);

                    logger.Success($"Tool '{toolName}' updated successfully!");
                }
                catch (Exception exception)
                {
                    logger.Error($"Failed to update tool '{toolName}': {exception.Message}");
                }
            }
        }).ConfigureAwait(false);
    }
}
