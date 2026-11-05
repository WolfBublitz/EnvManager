// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PackageManagerException.cs                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;

namespace EnvManager.Exceptions;

/// <summary>
/// Raised when a package manager operation (install, remove, update, list) fails, or
/// when no supported package manager could be detected on the current system.
/// </summary>
public sealed class PackageManagerException : EnvManagerException
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageManagerException"/> class for errors
    /// that are not tied to an executed command.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public PackageManagerException(string message)
        : base(FormatMessage(message, Environment.GetEnvironmentVariable("PATH")))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageManagerException"/> class for a failed command.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="command">The executable that was run.</param>
    /// <param name="arguments">The arguments the executable was run with.</param>
    /// <param name="workingDirectory">The working directory the process ran in.</param>
    /// <param name="environmentVariables">The environment variables the process ran with.</param>
    /// <param name="innerException">The exception that caused the current error, if any.</param>
    public PackageManagerException(
        string message,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        Exception? innerException = null)
        : base(FormatMessage(message, GetPath(environmentVariables)), innerException!)
    {
        this.Command = command;
        this.Arguments = arguments;
        this.WorkingDirectory = workingDirectory;
        this.EnvironmentVariables = environmentVariables;
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the executable that was run, or <see langword="null"/> when no command was involved.</summary>
    public string? Command { get; }

    /// <summary>Gets the arguments the executable was run with.</summary>
    public IReadOnlyList<string> Arguments { get; } = [];

    /// <summary>Gets the working directory the process ran in, or <see langword="null"/> when no command was involved.</summary>
    public string? WorkingDirectory { get; }

    /// <summary>Gets the environment variables the process ran with. May contain sensitive values; do not log blindly.</summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private static string FormatMessage(string message, string? path)
        => $"{message}{Environment.NewLine}PATH: {path ?? "<not set>"}";

    private static string? GetPath(IReadOnlyDictionary<string, string> environmentVariables)
    {
        foreach (KeyValuePair<string, string> environmentVariable in environmentVariables)
        {
            if (string.Equals(environmentVariable.Key, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                return environmentVariable.Value;
            }
        }

        return null;
    }
}
