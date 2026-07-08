using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

internal abstract class BaseCommand : Command
{
    public BaseCommand(string name, string description)
        : base(name, description)
    {
        SetAction(ExecuteAsync);
    }

    protected abstract Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken);
}
