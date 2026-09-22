// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PackageManagerException.cs                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;

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
    /// Initializes a new instance of the <see cref="PackageManagerException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    public PackageManagerException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageManagerException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that caused the current error.</param>
    public PackageManagerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
