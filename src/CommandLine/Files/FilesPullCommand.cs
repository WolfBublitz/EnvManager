using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class FilesPullCommand : BaseCommand
{
    private readonly GitRepository gitRepository;

    public FilesPullCommand(GitRepository gitRepository)
        : base("pull", "Pull files")
    {
        this.gitRepository = gitRepository;
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await AnsiConsole.Status().StartAsync("Pulling file(s) ...", ctx =>
        {
            return gitRepository.PullAsync();
        }).ConfigureAwait(false);
    }
}