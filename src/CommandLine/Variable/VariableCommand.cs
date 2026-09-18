// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: VariableCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

namespace EnvManager.CommandLine.Variable;

/// <summary>
/// The <c>variable</c> command groups the subcommands used to manage environment
/// variables: <c>set</c>, <c>remove</c>, <c>list</c>, and <c>update</c>.
/// </summary>
public sealed class VariableCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="VariableCommand"/> class.</summary>
    public VariableCommand()
        : base("variable", "Manage environment variables and configurations.")
    {
        this.Add(new VariableSetCommand());
        this.Add(new VariableRemoveCommand());
        this.Add(new VariableListCommand());
        this.Add(new VariableUpdateCommand());
    }
}
