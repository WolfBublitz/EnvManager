using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class FilesRemoveCommand : BaseCommand
{
    private readonly Argument<string> nameArgument = new("name")
    {
        Description = "The name of the tool to add",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly GitRepository gitRepository;
    private readonly CancellationService cancellationServices;

    public FilesRemoveCommand(GitRepository gitRepository, CancellationService cancellationServices)
        : base("remove", "Remove files")
    {
        this.gitRepository = gitRepository;
        this.cancellationServices = cancellationServices;

        Add(nameArgument);
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        DirectoryInfo directory = new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        IEnumerable<FileInfo> files = parseResult.GetValue(nameArgument) switch
        {
            string name => [new FileInfo(name)],
            null => await FileChooser.ChooseFileAsync("Select a file", directory, cancellationServices.CancellationToken).ConfigureAwait(false)
        };

        if (files == null)
        {
            return;
        }

        await AnsiConsole.Status().StartAsync($"Removing {files.Count()} file(s)...", async ctx =>
        {
            StringBuilder messageBuilder = new();
            messageBuilder.AppendLine($"Removed {files.Count()} file(s):");

            foreach (FileInfo file in files)
            {
                await gitRepository.RemoveFileAsync(file).ConfigureAwait(false);

                messageBuilder.AppendLine($"- {file.Name}");
            }

            await gitRepository.CommitAsync(messageBuilder.ToString()).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}