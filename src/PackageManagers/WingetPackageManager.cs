// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: WingetPackageManager.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

namespace EnvManager.PackageManagers;

/// <summary>
/// <see cref="IPackageManager"/> implementation for Windows using the built-in
/// <see href="https://learn.microsoft.com/windows/package-manager/">winget</see> tool.
/// </summary>
public sealed class WingetPackageManager : PackageManagerBase
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public override string Name => "winget";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string Executable => "winget";

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    protected override string[] BuildInstallArguments(string toolName, string? version)
        => version is null
            ? ["install", "--id", toolName, "--silent", "--accept-package-agreements", "--accept-source-agreements"]
            : ["install", "--id", toolName, "--version", version, "--silent", "--accept-package-agreements", "--accept-source-agreements"];

    /// <inheritdoc/>
    protected override string[] BuildRemoveArguments(string toolName)
        => ["uninstall", "--id", toolName, "--silent"];

    /// <inheritdoc/>
    protected override string[] BuildUpdateArguments(string toolName)
        => ["upgrade", "--id", toolName, "--silent", "--accept-package-agreements", "--accept-source-agreements"];
}
