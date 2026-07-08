using System.Threading.Tasks;
using Spectre.Console;

internal sealed class App(SystemInfo systemInfo, CommandLine commandLine)
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        AnsiConsole.Write(new FigletText("EnvManager").Color(Color.Green));
        AnsiConsole.Write(new Rule());

        Grid grid = new();
        grid.AddColumns(2);
        grid.AddRow("Operating System:", $"{systemInfo.OperatingSystem}");
        grid.AddRow("Package Manager:", $"{systemInfo.PackageManager}");

        AnsiConsole.Write(grid);
        AnsiConsole.Write(new Rule());

        int exitCode = await commandLine.ExecuteAsync(args).ConfigureAwait(false);

        AnsiConsole.WriteLine();

        if (exitCode != 0)
        {
            AnsiConsole.Write(new Rule($"[red]Error:[/] The application exited with code {exitCode}."));
        }
        else
        {
            AnsiConsole.Write(new Rule($"[green]Success:[/] The application exited with code {exitCode}."));
        }

        return exitCode;
    }
}