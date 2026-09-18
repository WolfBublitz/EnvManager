// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitCommandResult.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.Git;

/// <summary>
/// Represents the captured result of a single invocation of the <c>git</c> executable.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured, trimmed standard output.</param>
/// <param name="StandardError">The captured, trimmed standard error output.</param>
public readonly record struct GitCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets a value indicating whether the git process exited successfully.</summary>
    public bool Succeeded => this.ExitCode == 0;
}
