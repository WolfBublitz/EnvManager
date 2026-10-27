// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: EnvironmentRepository.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Git;

namespace EnvManager.Configuration;

/// <summary>
/// Combines a <see cref="GitClient"/> and a <see cref="ConfigurationSerializer"/> into
/// the single entry point commands use to read and mutate the currently checked-out
/// environment, and to switch between environments (branches).
/// </summary>
public sealed class EnvironmentRepository
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Field                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private readonly ConfigurationSerializer serializer = new();

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Constructor                                                             │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private EnvironmentRepository(GitClient git)
    {
        this.Git = git;
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the git client wrapping the local repository checkout.</summary>
    public GitClient Git { get; }

    /// <summary>Gets a value indicating whether an EnvManager repository has already been set up locally.</summary>
    public static bool IsInitialized => new GitClient(EnvManagerPaths.HomeDirectory, EnvManagerPaths.GitDirectory).IsRepository;

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Clones a remote configuration repository into the local EnvManager home
    /// directory and checks out (or creates) the given environment.
    /// </summary>
    /// <param name="repositoryUrl">The URL of the remote git repository.</param>
    /// <param name="environmentName">The environment (branch) to check out after cloning.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task<EnvironmentRepository> CloneAsync(Uri repositoryUrl, string environmentName, CancellationToken cancellationToken = default)
    {
        DirectoryInfo gitDirectory = EnvManagerPaths.GitDirectory;

        if (gitDirectory.Exists && gitDirectory.EnumerateFileSystemInfos().Any())
        {
            throw new ConfigurationException($"An EnvManager repository already exists at '{gitDirectory.FullName}'. Remove it before cloning again.");
        }

        GitClient git = new(EnvManagerPaths.HomeDirectory, gitDirectory);

        await git.CloneAsync(repositoryUrl, environmentName, cancellationToken).ConfigureAwait(false);

        EnvironmentRepository repository = new(git);

        // Route through SwitchAsync (rather than calling GitClient directly) so that a
        // brand-new environment branch is left with a committed environment.yaml, just
        // like when switching to a new environment on an already-cloned repository.
        await repository.SwitchAsync(environmentName, allowCreate: true, cancellationToken).ConfigureAwait(false);

        return repository;
    }

    /// <summary>
    /// Initializes a brand-new, local-only configuration repository (no remote yet)
    /// with a single environment branch.
    /// </summary>
    /// <param name="environmentName">The name of the initial environment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task<EnvironmentRepository> InitAsync(string environmentName, CancellationToken cancellationToken = default)
    {
        DirectoryInfo gitDirectory = EnvManagerPaths.GitDirectory;

        if (gitDirectory.Exists && gitDirectory.EnumerateFileSystemInfos().Any())
        {
            throw new ConfigurationException($"An EnvManager repository already exists at '{gitDirectory.FullName}'. Remove it before initializing again.");
        }

        GitClient git = new(EnvManagerPaths.HomeDirectory, gitDirectory);

        await git.InitAsync(environmentName, cancellationToken).ConfigureAwait(false);

        EnvironmentRepository repository = new(git);

        EnvironmentConfiguration configuration = new() { Name = environmentName };

        await repository.SaveConfigurationAsync(configuration, $"Initialize environment '{environmentName}'.", cancellationToken).ConfigureAwait(false);

        return repository;
    }

    /// <summary>
    /// Opens the already-initialized local configuration repository.
    /// </summary>
    /// <exception cref="ConfigurationException">
    /// Thrown when no repository has been set up yet via <c>clone</c> or <c>init</c>.
    /// </exception>
    public static EnvironmentRepository OpenExisting()
    {
        if (!IsInitialized)
        {
            throw new ConfigurationException(
                $"No EnvManager repository was found at '{EnvManagerPaths.GitDirectory.FullName}'. " +
                "Run 'envmanager clone <repository_url>' or 'envmanager init' first.");
        }

        return new EnvironmentRepository(new GitClient(EnvManagerPaths.HomeDirectory, EnvManagerPaths.GitDirectory));
    }

    /// <summary>Gets the name of the currently checked-out environment.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<string> GetCurrentEnvironmentNameAsync(CancellationToken cancellationToken = default)
        => this.Git.GetCurrentBranchAsync(cancellationToken);

    /// <summary>Loads the configuration for the currently checked-out environment.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<EnvironmentConfiguration> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        string environmentName = await this.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);

        return this.serializer.Load(EnvManagerPaths.EnvironmentFile, environmentName);
    }

    /// <summary>Persists and commits the given configuration for the currently checked-out environment.</summary>
    /// <param name="configuration">The configuration to persist.</param>
    /// <param name="commitMessage">The git commit message describing the change.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SaveConfigurationAsync(EnvironmentConfiguration configuration, string commitMessage, CancellationToken cancellationToken = default)
    {
        this.serializer.Save(EnvManagerPaths.EnvironmentFile, configuration);

        await this.Git.AddAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
        await this.Git.CommitAsync(commitMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Switches to a different environment, optionally creating it if it does not yet exist.</summary>f
    /// <param name="environmentName">The name of the environment (branch) to switch to.</param>
    /// <param name="allowCreate">Whether to create the environment when it does not yet exist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SwitchAsync(string environmentName, bool allowCreate, CancellationToken cancellationToken = default)
    {
        await this.Git.CheckoutBranchAsync(environmentName, allowCreate, cancellationToken).ConfigureAwait(false);

        // A freshly created orphan branch has no environment.yaml yet; write one so
        // subsequent commands have a well-formed configuration to work with.
        if (!EnvManagerPaths.EnvironmentFile.Exists)
        {
            await this.SaveConfigurationAsync(new EnvironmentConfiguration { Name = environmentName }, $"Initialize environment '{environmentName}'.", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Lists all known environments (local and remote branches).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
        => this.Git.ListEnvironmentsAsync(cancellationToken);

    /// <summary>
    /// Stages an existing file from the home directory for the currently checked-out
    /// environment without committing it.
    /// </summary>
    /// <param name="fileName">The file to add, relative to (or an absolute path inside) the home directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task AddFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string relativePath = ResolveRelativePath(fileName);
        string fullPath = Path.Combine(EnvManagerPaths.HomeDirectory.FullName, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new ConfigurationException($"File '{fileName}' was not found at '{fullPath}'.");
        }

        await this.Git.AddAsync(relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stages removal of a tracked file for the currently checked-out environment,
    /// leaving the file itself in place in the home directory.
    /// </summary>
    /// <param name="fileName">The file to remove, relative to (or an absolute path inside) the home directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RemoveFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string relativePath = ResolveRelativePath(fileName);

        if (!await this.Git.IsTrackedAsync(relativePath, cancellationToken).ConfigureAwait(false))
        {
            throw new ConfigurationException($"File '{relativePath}' is not tracked in the current environment.");
        }

        await this.Git.RemoveAsync(relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists every file tracked for the currently checked-out environment, excluding
    /// the internal <c>environment.yaml</c> configuration file.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> trackedFiles = await this.Git.ListTrackedFilesAsync(cancellationToken).ConfigureAwait(false);

        return trackedFiles
            .Where(path => !string.Equals(path, EnvManagerPaths.EnvironmentFileRelativePath, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Pushes the current environment's committed changes to the remote repository.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PushAsync(CancellationToken cancellationToken = default)
        => this.Git.PushAsync(cancellationToken);

    /// <summary>Lists pending changes to tracked files as <c>(status, path)</c> pairs.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<IReadOnlyList<(string Status, string Path)>> GetPendingChangesAsync(CancellationToken cancellationToken = default)
        => this.Git.ListPendingChangesAsync(cancellationToken);

    /// <summary>
    /// Commits all changes to tracked files with an auto-generated message (when there are
    /// any) and pushes the current environment to the remote repository.
    /// </summary>
    /// <param name="pendingChanges">The pending changes, used to generate the commit message.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task PushAsync(IReadOnlyList<(string Status, string Path)> pendingChanges, CancellationToken cancellationToken = default)
    {
        if (pendingChanges.Count > 0)
        {
            await this.Git.StageTrackedChangesAsync(cancellationToken).ConfigureAwait(false);
            await this.Git.CommitAsync(BuildCommitMessage(pendingChanges), cancellationToken).ConfigureAwait(false);
        }

        await this.Git.PushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildCommitMessage(IReadOnlyList<(string Status, string Path)> changes)
    {
        const int MaxListedFiles = 3;

        if (changes.Count == 1)
        {
            (string status, string path) = changes[0];
            string verb = status switch
            {
                "A" => "Add",
                "D" => "Remove",
                _ => "Update",
            };

            return $"{verb} file '{path}'.";
        }

        if (changes.Count <= MaxListedFiles)
        {
            return $"Update files {string.Join(", ", changes.Select(change => $"'{change.Path}'"))}.";
        }

        return $"Update {changes.Count} files.";
    }

    /// <summary>Replaces the current environment with the remote branch, discarding local changes and divergent commits.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PullAsync(CancellationToken cancellationToken = default)
        => this.Git.PullAsync(cancellationToken);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Resolves a user-supplied file name (relative to the home directory, or an
    /// absolute path) to a path relative to <see cref="EnvManagerPaths.HomeDirectory"/>,
    /// using forward slashes as git expects.
    /// </summary>
    /// <param name="fileName">The file name or path supplied by the user.</param>
    /// <exception cref="ConfigurationException">
    /// Thrown when the resolved path falls outside of the home directory.
    /// </exception>
    private static string ResolveRelativePath(string fileName)
    {
        string homeDirectory = EnvManagerPaths.HomeDirectory.FullName;

        string fullPath = Path.IsPathRooted(fileName)
            ? Path.GetFullPath(fileName)
            : Path.GetFullPath(Path.Combine(homeDirectory, fileName));

        string relativePath = Path.GetRelativePath(homeDirectory, fullPath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new ConfigurationException($"'{fileName}' is outside of the home directory ('{homeDirectory}') managed by EnvManager.");
        }

        string gitRelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(gitRelativePath, EnvManagerPaths.EnvironmentFileRelativePath, comparison))
        {
            throw new ConfigurationException($"'{EnvManagerPaths.EnvironmentFileRelativePath}' is managed internally and cannot be added or removed as a user file.");
        }

        return gitRelativePath;
    }
}
