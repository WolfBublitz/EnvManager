// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConfigurationException.cs                                               │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

namespace EnvManager.Exceptions;

/// <summary>
/// Raised when EnvManager's local configuration repository is missing, uninitialized,
/// or otherwise cannot be used to satisfy the requested operation (for example, running
/// <c>variable set</c> before <c>clone</c> or <c>init</c> has been run).
/// </summary>
public sealed class ConfigurationException : EnvManagerException
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that caused the current error.</param>
    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
