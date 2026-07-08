using System.CommandLine;

internal sealed class FilesCommand : Command
{
    public FilesCommand(
        FilesListCommand filesListCommand,
        FilesAddCommand filesAddCommand,
        FilesRemoveCommand filesRemoveCommand,
        FilesPushCommand filesPushCommand,
        FilesPullCommand filesPullCommand,
        FilesCommitCommand filesCommitCommand)
        : base("files", "Manage files")
    {
        Add(filesListCommand);
        Add(filesAddCommand);
        Add(filesRemoveCommand);
        Add(filesPushCommand);
        Add(filesPullCommand);
        Add(filesCommitCommand);
    }
}
