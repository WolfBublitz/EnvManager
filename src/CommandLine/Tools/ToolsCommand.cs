// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolsCommand.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

namespace EnvManager.CommandLine.Tools;

/// <summary>
/// The <c>tools</c> command groups the subcommands used to manage tools: <c>add</c>,
/// <c>remove</c>, <c>list</c>, and <c>update</c>.
/// </summary>
public sealed class ToolsCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="ToolsCommand"/> class.</summary>
    public ToolsCommand()
        : base("tools", "Manage tools and configurations.")
    {
        this.Add(new ToolsAddCommand());
        this.Add(new ToolsRemoveCommand());
        this.Add(new ToolsListCommand());
        this.Add(new ToolsUpdateCommand());
    }
}
