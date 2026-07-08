using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class FilesCommitCommand : BaseCommand
{
    private readonly GitRepository gitRepository;

    public FilesCommitCommand(GitRepository gitRepository)
        : base("commit", "Commit all files in the environment")
    {
        this.gitRepository = gitRepository;
    }

    protected override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {   
        IEnumerable<FileInfo> files = await gitRepository.ListChangedFilesAsync().ConfigureAwait(false);

        MultiSelectionPrompt<FileInfo> prompt = new()
        {
            Title = "Select files to commit:",
            PageSize = 10,
            HighlightStyle = new Style(foreground: Color.Green),
            MoreChoicesText = "[grey](Move up and down to reveal more files)[/]",
            InstructionsText = "[grey](Press [blue]<space>[/] to toggle a file, [green]<enter>[/] to accept)[/]",
            Converter = file => file.Name
        };

        prompt.AddChoices(files);

        List<FileInfo> selectedFiles = await AnsiConsole.PromptAsync(prompt, cancellationToken).ConfigureAwait(false);

        await AnsiConsole.Status().StartAsync($"Committing {selectedFiles.Count} files ...", async ctx =>
        {
            foreach (FileInfo file in selectedFiles)
            {
                await gitRepository.AddFileAsync(file).ConfigureAwait(false);
            }

            await gitRepository.CommitAsync($"Committed {selectedFiles.Count} files").ConfigureAwait(false);
            await gitRepository.PushAsync().ConfigureAwait(false);
        }).ConfigureAwait(false); 
    }
}