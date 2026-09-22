// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: HomebrewPackageManager.cs                                               │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for macOS and Linux using
/// <see href="https://brew.sh">Homebrew</see>.
/// </summary>
public sealed class HomebrewPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "brew";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "brew";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        // Homebrew does not generally support installing arbitrary pinned versions of a
        // formula, so a requested version is used to select a versioned formula/cask
        // name (e.g. "node@18") when provided.
        => ["install", version is null ? toolName : $"{toolName}@{version}"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["uninstall", toolName];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["upgrade", toolName];
}
