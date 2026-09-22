// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PackageManagerFactory.cs                                               │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.PackageManagers;

/// <summary>
/// Detects which package manager is installed and preferred on the current operating
/// system, and resolves an explicitly requested package manager by name.
/// </summary>
public static class PackageManagerFactory
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Field                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private static readonly IReadOnlyList<IPackageManager> AllPackageManagers =
    [
        new AptPackageManager(),
        new DnfPackageManager(),
        new YumPackageManager(),
        new ZypperPackageManager(),
        new HomebrewPackageManager(),
        new ScoopPackageManager(),
        new ChocolateyPackageManager(),
        new WingetPackageManager(),
    ];

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Detects the most appropriate available package manager for the current operating
    /// system, checking each candidate (in OS-specific priority order) for availability.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="PackageManagerException">Thrown when no supported package manager could be found.</exception>
    public static async Task<IPackageManager> DetectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IPackageManager candidate in GetCandidatesForCurrentOperatingSystem())
        {
            if (await candidate.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                return candidate;
            }
        }

        throw new PackageManagerException(
            "No supported package manager (apt, dnf, yum, zypper, brew, scoop, choco, winget) was found on this system.");
    }

    /// <summary>Resolves a package manager by its identifier (e.g. "brew").</summary>
    /// <param name="name">The package manager identifier.</param>
    /// <exception cref="PackageManagerException">Thrown when the name is not recognized.</exception>
    public static IPackageManager GetByName(string name)
        => AllPackageManagers.FirstOrDefault(manager => string.Equals(manager.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new PackageManagerException($"Unknown package manager '{name}'. Supported values are: {string.Join(", ", AllPackageManagers.Select(manager => manager.Name))}.");

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Returns the package managers relevant to the current OS, in priority order.</summary>
    private static IEnumerable<IPackageManager> GetCandidatesForCurrentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return AllPackageManagers.First(manager => manager is WingetPackageManager);
            yield return AllPackageManagers.First(manager => manager is ScoopPackageManager);
            yield return AllPackageManagers.First(manager => manager is ChocolateyPackageManager);

            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return AllPackageManagers.First(manager => manager is HomebrewPackageManager);

            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return AllPackageManagers.First(manager => manager is AptPackageManager);
            yield return AllPackageManagers.First(manager => manager is DnfPackageManager);
            yield return AllPackageManagers.First(manager => manager is YumPackageManager);
            yield return AllPackageManagers.First(manager => manager is ZypperPackageManager);
            yield return AllPackageManagers.First(manager => manager is HomebrewPackageManager);

            yield break;
        }

        // Unknown/unsupported OS: fall back to probing every known package manager.
        foreach (IPackageManager manager in AllPackageManagers)
        {
            yield return manager;
        }
    }
}
