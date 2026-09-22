// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvManagerRootCommand.cs                                               │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.CommandLine.Files;
using EnvManager.CommandLine.Tools;
using EnvManager.CommandLine.Variable;

namespace EnvManager.CommandLine;

/// <summary>
/// The top-level EnvManager CLI command, wiring together every subcommand described in
/// the project specification: <c>clone</c>, <c>init</c>, <c>variable</c>, <c>tools</c>,
/// <c>files</c>, <c>push</c>, <c>pull</c>, and <c>switch</c>.
/// </summary>
public sealed class EnvManagerRootCommand : RootCommand
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="EnvManagerRootCommand"/> class.</summary>
    public EnvManagerRootCommand()
        : base("Manages user environment configurations (variables, tools, and shell setup) backed by a git repository.")
    {
        this.Add(new CloneCommand());
        this.Add(new InitCommand());
        this.Add(new VariableCommand());
        this.Add(new ToolsCommand());
        this.Add(new FilesCommand());
        this.Add(new PushCommand());
        this.Add(new PullCommand());
        this.Add(new SwitchCommand());
    }
}
