// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvironmentConfiguration.cs                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;

namespace EnvManager.Configuration;

/// <summary>
/// The full, serializable state of a single environment. Every environment is stored as
/// one <c>environment.yaml</c> file at the root of its own branch in the EnvManager
/// configuration repository.
/// </summary>
public sealed class EnvironmentConfiguration
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets or sets the name of the environment (matches the git branch name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the shells this environment's variables should be applied to.</summary>
    public List<string> Shells { get; set; } = [];

    /// <summary>Gets or sets the user-scoped environment variables managed for this environment.</summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>Gets or sets the tools managed for this environment.</summary>
    public List<ToolDefinition> Tools { get; set; } = [];
}
