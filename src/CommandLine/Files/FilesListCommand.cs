using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class FilesListCommand : BaseCommand
{
    private readonly GitRepository gitRepository;

    public FilesListCommand(GitRepository gitRepository)
        : base("list", "List all files in the environment")
    {
        this.gitRepository = gitRepository;
    }

    protected override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        IEnumerable<FileInfo> files = [];
        
        await AnsiConsole.Status().StartAsync("Listing files...", async ctx =>
        {
            files = await gitRepository.ListFilesAsync();
        });
        
        if (files is null || !files.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No files found in the environment.[/]");
            return;
        }
        else
        {
            Tree tree = new("Files in the environment");   

            BuildTree(tree, files, gitRepository.WorkingDirectory.FullName);

            AnsiConsole.Write(tree);
        }
    }

    private static void BuildTree(IHasTreeNodes tree, IEnumerable<FileInfo> files, string basePath)
    {
        Queue<FileInfo> fileQueue = new(files);

        while (fileQueue.Count > 0)
        {
            FileInfo file = fileQueue.Dequeue();
            string relativePath = Path.GetRelativePath(basePath, file.FullName);
            string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

            if (pathParts.Length > 1)
            {
                IHasTreeNodes node = tree.AddNode($":file_folder: [blue]{pathParts[0]}[/]");

                List<FileInfo> subFiles = [file];

                while (fileQueue.Count > 0 && Path.GetRelativePath(basePath, fileQueue.Peek().FullName).StartsWith(pathParts[0] + Path.DirectorySeparatorChar))
                {
                    subFiles.Add(fileQueue.Dequeue());
                }
            
                BuildTree(node, subFiles, Path.Combine(basePath, pathParts[0]));
            }
            else
            {
                tree.AddNode($"{file.GetIcon()} {pathParts[0]}");
            }
        }
    }
}