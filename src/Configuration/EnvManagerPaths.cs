// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvManagerPaths.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;

namespace EnvManager.Configuration;

/// <summary>
/// Resolves the well-known paths EnvManager's git-backed configuration repository
/// uses, per the project's Git setup: a bare repository at
/// <c>$HOME/.EnvManager/repo</c> whose work tree is the user's home directory itself.
/// </summary>
public static class EnvManagerPaths
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Gets the work tree directory EnvManager manages, i.e. the user's home
    /// directory. Defaults to the OS-reported home directory but can be overridden
    /// with the <c>ENVMANAGER_HOME</c> environment variable, which is primarily useful
    /// for testing (so a real home directory is never touched by tests).
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

            return new DirectoryInfo(userProfile);
        }
    }

    /// <summary>Gets the bare git repository directory backing <see cref="HomeDirectory"/>.</summary>
    public static DirectoryInfo GitDirectory => new(Path.Combine(HomeDirectory.FullName, ".EnvManager", "repo"));

    /// <summary>Gets the path of the <c>environment.yaml</c> file at the root of <see cref="HomeDirectory"/>.</summary>
    public static FileInfo EnvironmentFile => new(Path.Combine(HomeDirectory.FullName, "environment.yaml"));
}
