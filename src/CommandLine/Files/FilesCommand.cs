// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesCommand.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files</c> command groups the subcommands used to manage files tracked in the
/// environment repository: <c>add</c>, <c>remove</c>, and <c>list</c>.
/// </summary>
public sealed class FilesCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesCommand"/> class.</summary>
    public FilesCommand()
        : base("files", "Manage files and configurations in the environment repository.")
    {
        this.Add(new FilesAddCommand());
        this.Add(new FilesRemoveCommand());
        this.Add(new FilesListCommand());
    }
}
