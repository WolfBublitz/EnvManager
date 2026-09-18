// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: IPackageManager.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Threading;
using System.Threading.Tasks;

namespace EnvManager.PackageManagers;

/// <summary>
/// Abstraction over a system package manager (apt, brew, winget, etc.) used to install,
/// remove, and update the tools configured for an environment.
/// </summary>
public interface IPackageManager
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the identifier of the package manager, e.g. "brew" or "apt".</summary>
    string Name { get; }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Determines whether this package manager's executable is available on the current system.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Installs the given tool, optionally pinning it to a specific version.</summary>
    /// <param name="toolName">The name of the tool/package to install.</param>
    /// <param name="version">The specific version to install, or <see langword="null"/> for the latest.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task InstallAsync(string toolName, string? version, CancellationToken cancellationToken = default);

    /// <summary>Removes the given tool.</summary>
    /// <param name="toolName">The name of the tool/package to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveAsync(string toolName, CancellationToken cancellationToken = default);

    /// <summary>Updates the given tool to its latest available version.</summary>
    /// <param name="toolName">The name of the tool/package to update.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(string toolName, CancellationToken cancellationToken = default);
}
