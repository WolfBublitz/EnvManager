// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ChocolateyPackageManager.cs                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for Windows using
/// <see href="https://chocolatey.org">Chocolatey</see>.
/// </summary>
public sealed class ChocolateyPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "choco";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "choco";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        => version is null
            ? ["install", toolName, "-y"]
            : ["install", toolName, "--version", version, "-y"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["uninstall", toolName, "-y"];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["upgrade", toolName, "-y"];
}
