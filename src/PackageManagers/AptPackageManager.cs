// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: AptPackageManager.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for Debian/Ubuntu-based systems using
/// <c>apt-get</c>.
/// </summary>
public sealed class AptPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "apt";

    /// <inheritdoc/>
    public override bool RequiresElevation => true;

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "apt-get";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        => ["install", "-y", version is null ? toolName : $"{toolName}={version}"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["remove", "-y", toolName];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["install", "-y", "--only-upgrade", toolName];
}
