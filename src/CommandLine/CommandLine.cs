using System;
using System.CommandLine;
using System.Threading.Tasks;
using Humanizer;
using Spectre.Console;

internal sealed class CommandLine(RootCommand rootCommand)
{
    private static readonly InvocationConfiguration invocationConfiguration = new()
    {
        EnableDefaultExceptionHandler = false,
    };

    public async Task<int> ExecuteAsync(string[] args)
    {
        ParseResult result = rootCommand.Parse(args);

        try
        {
            return await result.InvokeAsync(invocationConfiguration).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Style exceptionStyle = new(Color.Red);

            Panel panel = new(exception.GetType().Name.Humanize(LetterCasing.Sentence).ToText(exceptionStyle))
            {
                Border = BoxBorder.Square,
                BorderStyle = exceptionStyle,
                Padding = new Padding(1, 0, 1, 0)  
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(exception.Message);

            if (exception.Data.Count > 0)
            {
                AnsiConsole.WriteLine();

                Grid grid = new();
                grid.AddColumns(2);

                foreach (var key in exception.Data.Keys)
                {
                    grid.AddRow($"[yellow]{key}[/]", $"{exception.Data[key]}");
                }

                AnsiConsole.Write(grid);
            }

            return exception.ExitCode ?? 1;
        }
    }
}
