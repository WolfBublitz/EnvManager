// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitCommandException.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

namespace EnvManager.Exceptions;

/// <summary>
/// Raised when an invocation of the external <c>git</c> executable fails, either
/// because the process could not be started or because it exited with a non-zero status code.
/// </summary>
public sealed class GitCommandException : EnvManagerException
{
    /// <summary>Initializes an exception with captured results from a failed Git command.</summary>
    /// <param name="arguments">The operation arguments passed to git.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="standardError">The captured standard error output.</param>
    public GitCommandException(string arguments, int exitCode, string standardError)
        : this(arguments, BuildCommand(arguments), exitCode, string.Empty, standardError)
    {
    }

    /// <summary>Initializes an exception with captured results from a failed Git command.</summary>
    /// <param name="arguments">The operation arguments passed to git.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="standardOutput">The captured standard output.</param>
    /// <param name="standardError">The captured standard error output.</param>
    public GitCommandException(string arguments, int exitCode, string standardOutput, string standardError)
        : this(arguments, BuildCommand(arguments), exitCode, standardOutput, standardError)
    {
    }

    /// <summary>Initializes an exception with the full command and captured results.</summary>
    /// <param name="arguments">The operation arguments passed to git.</param>
    /// <param name="command">The complete command line used to invoke git.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="standardOutput">The captured standard output.</param>
    /// <param name="standardError">The captured standard error output.</param>
    public GitCommandException(string arguments, string command, int exitCode, string standardOutput, string standardError)
        : base(FormatMessage(command, exitCode, standardOutput, standardError))
    {
        this.Arguments = arguments;
        this.Command = command;
        this.ExitCode = exitCode;
        this.StandardOutput = standardOutput;
        this.StandardError = standardError;
    }

    /// <summary>Initializes an exception for a Git process that could not be started.</summary>
    /// <param name="innerException">The exception raised while starting the process.</param>
    public GitCommandException(Exception innerException)
        : this("git", innerException)
    {
    }

    /// <summary>Initializes an exception for a Git command that could not be started.</summary>
    /// <param name="command">The complete command line that could not be started.</param>
    /// <param name="innerException">The exception raised while starting the process.</param>
    public GitCommandException(string command, Exception innerException)
        : base(FormatMessage(command, -1, string.Empty, innerException.Message), innerException)
    {
        this.Arguments = string.Empty;
        this.Command = command;
        this.ExitCode = -1;
        this.StandardOutput = string.Empty;
        this.StandardError = innerException.Message;
    }

    /// <summary>Gets the operation arguments passed to git.</summary>
    public string Arguments { get; }

    /// <summary>Gets the full command line used to invoke git.</summary>
    public string Command { get; }

    /// <summary>Gets the exit code returned by the git process, or -1 if it could not start.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the captured standard output of the git process.</summary>
    public string StandardOutput { get; }

    /// <summary>Gets the captured standard error output of the git process.</summary>
    public string StandardError { get; }

    private static string BuildCommand(string arguments)
        => string.IsNullOrWhiteSpace(arguments) ? "git" : $"git {arguments}";

    private static string FormatMessage(string command, int exitCode, string standardOutput, string standardError)
        => $"Git command '{command}' failed with exit code {exitCode}.{Environment.NewLine}" +
           $"Standard output:{Environment.NewLine}{FormatOutput(standardOutput)}{Environment.NewLine}" +
           $"Standard error:{Environment.NewLine}{FormatOutput(standardError)}";

    private static string FormatOutput(string output)
        => string.IsNullOrWhiteSpace(output) ? "(empty)" : output.Trim();
}
