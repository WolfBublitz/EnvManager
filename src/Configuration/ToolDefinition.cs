// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ToolDefinition.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.Configuration;

/// <summary>
/// Describes a single tool that should be installed as part of an environment, along
/// with the package manager and version that were used (or requested) for it.
/// </summary>
public sealed class ToolDefinition
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets or sets the name of the tool, as understood by the package manager.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the package manager used to install the tool (e.g. "brew").</summary>
    public string? PackageManager { get; set; }

    /// <summary>Gets or sets the requested version of the tool, or <see langword="null"/> for the latest version.</summary>
    public string? Version { get; set; }
}
