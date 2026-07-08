using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class ToolsAddCommand : BaseCommand
{
    private readonly Argument<string> nameArgument = new("name")
    {
        Description = "The name of the tool to add",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly ConfigurationService configurationService;

    private readonly GitRepository repository;

    private readonly Logger logger;

    public ToolsAddCommand(ConfigurationService configurationService, GitRepository repository, Logger logger)
        : base("add", "Add a new tool to the environment")
    {
        this.configurationService = configurationService;
        this.repository = repository;
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

        if (configuration.Tools.Contains(toolName))
        {
            throw new InvalidOperationException($"Tool '{toolName}' already exists in the environment.");
        }
        else
        {
            await AnsiConsole.Status().StartAsync($"Adding tool '{toolName}' ...", async ctx =>
            {
                configuration.Tools.Add(toolName);

                await configurationService.WriteAsync().ConfigureAwait(false);

                await repository.CommitAsync($"Added tool '{toolName}'").ConfigureAwait(false);
                await repository.PushAsync().ConfigureAwait(false);

                logger.Success($"Tool '{toolName}' added successfully!");
            }).ConfigureAwait(false);
        }
    }
}
