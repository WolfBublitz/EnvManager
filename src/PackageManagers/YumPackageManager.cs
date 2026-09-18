// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: YumPackageManager.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for older RHEL/CentOS-based systems using <c>yum</c>.
/// </summary>
public sealed class YumPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "yum";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "yum";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        => ["install", "-y", version is null ? toolName : $"{toolName}-{version}"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["remove", "-y", toolName];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["update", "-y", toolName];
}
