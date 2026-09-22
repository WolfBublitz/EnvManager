// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesRemoveCommand.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files remove</c> command stops tracking a file in the environment
/// repository. The file itself is left untouched in the home directory, since the
/// repository's work tree is the home directory rather than a dedicated checkout.
/// </summary>
public sealed class FilesRemoveCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesRemoveCommand"/> class.</summary>
    public FilesRemoveCommand()
        : base("remove", "Removes an existing file or configuration from the repository.")
    {
        Argument<string> fileNameArgument = new("file_name")
        {
            Description = "The name of the file to remove, relative to the home directory (or an absolute path within it).",
        };

        this.Add(fileNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string fileName = parseResult.GetValue(fileNameArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();

            await repository.RemoveFileAsync(fileName, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Removed file '{fileName}' from the repository.");
        }));
    }
}
