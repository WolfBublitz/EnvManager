// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvManagerPaths.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;

namespace EnvManager.Configuration;

/// <summary>
/// Resolves the well-known, per-user directories EnvManager uses to store its local
/// clone of the environment configuration repository.
/// </summary>
public static class EnvManagerPaths
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Gets the root directory EnvManager stores its data in. Defaults to
    /// <c>~/.envmanager</c> but can be overridden with the <c>ENVMANAGER_HOME</c>
    /// environment variable, which is primarily useful for testing.
    /// </summary>
    public static DirectoryInfo HomeDirectory
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("ENVMANAGER_HOME");

            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return new DirectoryInfo(overridePath);
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return new DirectoryInfo(Path.Combine(userProfile, ".envmanager"));
        }
    }

    /// <summary>Gets the directory holding the local clone of the configuration repository.</summary>
    public static DirectoryInfo RepositoryDirectory => new(Path.Combine(HomeDirectory.FullName, "repository"));

    /// <summary>Gets the path of the <c>environment.yaml</c> file inside the current checkout.</summary>
    public static FileInfo EnvironmentFile => new(Path.Combine(RepositoryDirectory.FullName, "environment.yaml"));
}
