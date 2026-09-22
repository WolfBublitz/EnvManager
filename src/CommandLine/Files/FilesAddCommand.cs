// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesAddCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files add</c> command adds an existing file from the home directory to the
/// environment repository, so that it is tracked and versioned as part of the
/// currently checked-out environment.
/// </summary>
public sealed class FilesAddCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesAddCommand"/> class.</summary>
    public FilesAddCommand()
        : base("add", "Adds a new file or configuration to the repository.")
    {
        Argument<string> fileNameArgument = new("file_name")
        {
            Description = "The name of the file to add, relative to the home directory (or an absolute path within it).",
        };

        this.Add(fileNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string fileName = parseResult.GetValue(fileNameArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();

            await repository.AddFileAsync(fileName, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Added file '{fileName}' to the repository.");
        }));
    }
}
