using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

internal sealed class RootCommand : System.CommandLine.RootCommand
{
    public RootCommand(InitCommand initCommand, FilesCommand filesCommand, ToolsCommand toolsCommand) 
        : base("EnvManager - A cross-platform environment management tool")
    {
        Add(initCommand);
        Add(filesCommand);
        Add(toolsCommand);
        SetAction(ExecuteAsync);
    }

    private Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}