using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class ToolsListCommand : BaseCommand
{
    private readonly ConfigurationService configurationService;

    public ToolsListCommand(ConfigurationService configurationService)
        : base("list", "List all tools in the environment")
    {
        this.configurationService = configurationService;
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Table table = new();
        table.AddColumn("File");

        Configuration configuration = configurationService.Configuration;

        foreach (string file in configuration.Tools)
        {
            table.AddRow(file);
        }

        AnsiConsole.Write(table);
    }
}
