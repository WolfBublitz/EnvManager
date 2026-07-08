using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class ToolsRemoveCommand : BaseCommand
{
    private readonly Argument<string> nameArgument = new("name")
    {
        Description = "The name of the tool to remove",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly Configuration configuration;
    private readonly Logger logger;
    private readonly ConfigurationService configurationService;

    public ToolsRemoveCommand(ConfigurationService configurationService, Logger logger)
        : base("remove", "Remove a tool from the environment")
    {
        this.configurationService = configurationService;
        this.logger = logger;

        Add(nameArgument);
    }

    protected override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        string toolName = parseResult.GetValue(nameArgument) switch
        {
            string name => name,
            null => AnsiConsole.Ask<string>("Enter the name of the tool:")
        };

        Configuration configuration = configurationService.Configuration;

        if (!configuration.Tools.Contains(toolName))
        {
            throw new InvalidOperationException($"Tool '{toolName}' does not exist in the environment.");
        }
        else
        {
            configuration.Tools.Remove(toolName);

            await configurationService.WriteAsync().ConfigureAwait(false);

            logger.Success($"Tool '{toolName}' removed successfully!");
        }
    }
}
