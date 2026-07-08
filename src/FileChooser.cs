using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal static class FileChooser
{
    internal static async Task<IEnumerable<FileInfo>> ChooseFileAsync(string title, DirectoryInfo directory, CancellationToken cancellationToken)
    {
        FileSystemInfo[] entries = [..directory.GetFiles().OrderBy(f => f.Name), ..directory.GetDirectories().OrderBy(d => d.Name)];

        if (directory.Parent != null)
        {
            entries = [directory.Parent, ..entries];
        }

        MultiSelectionPrompt<FileSystemInfo> prompt = new MultiSelectionPrompt<FileSystemInfo>()
                .Title($"[green]{title}[/]: {directory.FullName}")
                .AddChoices(entries)
                .UseConverter(f => f switch
                {
                    FileInfo file => $":page_facing_up: [blue]{file.Name}[/]",
                    DirectoryInfo parentDirectory when parentDirectory.FullName == directory.Parent?.FullName => ":up_arrow:  [yellow]..[/]",
                    DirectoryInfo subDirectory => $":file_folder: [yellow]{subDirectory.Name}/[/]",
                    _ => f.Name 
                });
 
        List<FileInfo?> files = [];

        IEnumerable<FileSystemInfo> selected = await AnsiConsole.PromptAsync(prompt, cancellationToken).ConfigureAwait(false);
    
        foreach (FileSystemInfo entry in selected)
        {
            switch (entry)
            {
                case FileInfo file:
                    files.Add(file);
                    break;
                case DirectoryInfo parentDirectory when parentDirectory == directory.Parent:
                    files.AddRange(await ChooseFileAsync(title, parentDirectory.Parent ?? parentDirectory, cancellationToken).ConfigureAwait(false));
                    break;
                case DirectoryInfo subDirectory:
                    files.AddRange(await ChooseFileAsync(title, subDirectory, cancellationToken).ConfigureAwait(false));
                    break;
            }
        }

        return files;
    }
}