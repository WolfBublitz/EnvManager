// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitCommandException.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

namespace EnvManager.Exceptions;

/// <summary>
/// Raised when an invocation of the external <c>git</c> executable fails, either
/// because the process could not be started (git is not installed) or because it
/// exited with a non-zero status code.
/// </summary>
public sealed class GitCommandException : EnvManagerException
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="GitCommandException"/> class.
    /// </summary>
    /// <param name="arguments">The arguments that were passed to git.</param>
    /// <param name="exitCode">The exit code returned by the git process.</param>
    /// <param name="standardError">The captured standard error output.</param>
    public GitCommandException(string arguments, int exitCode, string standardError)
        : base($"Git command 'git {arguments}' failed with exit code {exitCode}.{(string.IsNullOrWhiteSpace(standardError) ? string.Empty : $" {standardError.Trim()}")}")
    {
        this.Arguments = arguments;
        this.ExitCode = exitCode;
        this.StandardError = standardError;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitCommandException"/> class for the
    /// case where the git executable itself could not be located or started.
    /// </summary>
    /// <param name="innerException">The exception raised while starting the process.</param>
    public GitCommandException(Exception innerException)
        : base("Failed to run 'git'. Make sure Git is installed and available on the PATH.", innerException)
    {
        this.Arguments = string.Empty;
        this.ExitCode = -1;
        this.StandardError = innerException.Message;
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the arguments that were passed to the git process.</summary>
    public string Arguments { get; }

    /// <summary>Gets the exit code returned by the git process.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the captured standard error output of the git process.</summary>
    public string StandardError { get; }
}
