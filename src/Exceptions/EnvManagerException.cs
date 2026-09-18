// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvManagerException.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

namespace EnvManager.Exceptions;

/// <summary>
/// Base class for all recoverable, user-facing errors raised by EnvManager. Callers at
/// the CLI boundary catch this type to render a friendly, color-coded error message
/// instead of an unhandled stack trace.
/// </summary>
public class EnvManagerException : Exception
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvManagerException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public EnvManagerException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvManagerException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that caused the current error.</param>
    public EnvManagerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
