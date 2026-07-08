using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class FilesPushCommand : BaseCommand
{
    private readonly GitRepository gitRepository;

    public FilesPushCommand(GitRepository gitRepository)
        : base("push", "Push files")
    {
        this.gitRepository = gitRepository;
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await AnsiConsole.Status().StartAsync("Pushing file(s) ...", ctx =>
        {
            return gitRepository.PushAsync();
        }).ConfigureAwait(false);
    }
}