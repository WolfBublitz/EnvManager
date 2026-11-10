// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellsCommand.cs                                                       │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

namespace EnvManager.CommandLine.Shells;

/// <summary>Groups commands for enabling, disabling, and listing shell integrations.</summary>
public sealed class ShellsCommand : Command
{
    /// <summary>Initializes a new instance of the <see cref="ShellsCommand"/> class.</summary>
    public ShellsCommand()
        : base("shells", "Manage supported shell profiles and addon files.")
    {
        this.Add(new ShellsAddCommand());
        this.Add(new ShellsRemoveCommand());
        this.Add(new ShellsListCommand());
    }
}
