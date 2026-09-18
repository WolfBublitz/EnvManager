// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ScoopPackageManager.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for Windows using
/// <see href="https://scoop.sh">Scoop</see>.
/// </summary>
public sealed class ScoopPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "scoop";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "scoop";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        => ["install", version is null ? toolName : $"{toolName}@{version}"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["uninstall", toolName];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["update", toolName];
}
