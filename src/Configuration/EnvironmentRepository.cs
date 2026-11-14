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

using EnvManager.CommandLine;
using EnvManager.Exceptions;
using EnvManager.Git;
using EnvManager.Shells;

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
        // brand-new environment branch is left with a committed config.yml, just
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
        await this.MigrateConfigurationAsync(cancellationToken).ConfigureAwait(false);

        string environmentName = await this.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
        EnvironmentConfiguration configuration = this.serializer.Load(EnvManagerPaths.EnvironmentFile, environmentName);
        ShellEnvironmentExporter.Synchronize(configuration, EnvManagerPaths.HomeDirectory, OperatingSystem.IsWindows());
        return configuration;
    }

    /// <summary>Checks and migrates the current configuration to the latest supported schema.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when migration is finished.</returns>
    public async Task MigrateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        FileInfo configurationFile = EnvManagerPaths.EnvironmentFile;

        if (!configurationFile.Exists)
        {
            return;
        }

        if (await this.Git.HasStagedChangesForFileAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false)
            && await this.Git.HasUnstagedChangesForFileAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false))
        {
            throw new ConfigurationException("config.yml has both staged and unstaged changes. Resolve them before EnvManager synchronizes the configuration.");
        }

        string environmentName = await this.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
        EnvironmentConfiguration configuration = this.serializer.Load(configurationFile, environmentName);

        if (this.serializer.Migrate(configurationFile, configuration))
        {
            await this.Git.AddAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
            if (await this.Git.HasStagedChangesForFileAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false))
            {
                await this.Git.CommitFileAsync(
                    $"Migrate configuration to format version {EnvironmentConfiguration.CurrentFormatVersion}.",
                    EnvManagerPaths.EnvironmentFileRelativePath,
                    cancellationToken).ConfigureAwait(false);
                await this.PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
            }

            ConsoleReporter.Info($"Migrated config.yml to format version {EnvironmentConfiguration.CurrentFormatVersion}.");
        }

        ShellProfileInstaller shellProfileInstaller = new(EnvManagerPaths.HomeDirectory, OperatingSystem.IsWindows());
        HashSet<string> migratedShells = new(StringComparer.OrdinalIgnoreCase);
        bool isWindows = OperatingSystem.IsWindows();
        foreach (string shellName in configuration.Shells)
        {
            string normalizedShellName = shellName.Trim().ToLowerInvariant();
            if ((isWindows && normalizedShellName is "bash" or "zsh" or "fish")
                || (!isWindows && normalizedShellName is "cmd")
                || !migratedShells.Add(normalizedShellName))
            {
                continue;
            }

            string? aliasPath = shellProfileInstaller.EnsureEnvManagerAlias(normalizedShellName);
            if (aliasPath is null)
            {
                continue;
            }

            await this.Git.AddAsync(aliasPath, cancellationToken).ConfigureAwait(false);
            if (await this.Git.HasStagedChangesForFileAsync(aliasPath, cancellationToken).ConfigureAwait(false))
            {
                await this.Git.CommitFileAsync($"Add EnvManager alias for shell '{normalizedShellName}'.", aliasPath, cancellationToken).ConfigureAwait(false);
            }

            ConsoleReporter.Info($"Added the 'em' alias for EnvManager to the '{normalizedShellName}' shell profile.");
        }
    }

    /// <summary>Persists and commits the given configuration for the currently checked-out environment.</summary>
    /// <param name="configuration">The configuration to persist.</param>
    /// <param name="commitMessage">The git commit message describing the change.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task SaveConfigurationAsync(EnvironmentConfiguration configuration, string commitMessage, CancellationToken cancellationToken = default)
        => this.SaveConfigurationAsync(configuration, commitMessage, pushToRemote: true, cancellationToken: cancellationToken);

    /// <summary>Persists and commits the given configuration, optionally publishing it to the remote.</summary>
    /// <param name="configuration">The configuration to persist.</param>
    /// <param name="commitMessage">The git commit message describing the change.</param>
    /// <param name="pushToRemote">Whether to push the configuration commit to the remote.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SaveConfigurationAsync(EnvironmentConfiguration configuration, string commitMessage, bool pushToRemote, CancellationToken cancellationToken = default)
    {
        if (this.serializer.Save(EnvManagerPaths.EnvironmentFile, configuration))
        {
            ConsoleReporter.Info("Updated the configuration schema reference in config.yml.");
        }

        ShellEnvironmentExporter.Synchronize(configuration, EnvManagerPaths.HomeDirectory, OperatingSystem.IsWindows());
        await this.StageShellDirectoryChangesAsync(cancellationToken).ConfigureAwait(false);

        await this.Git.AddAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
        bool configurationChanged = await this.Git.HasStagedChangesForFileAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
        await this.Git.CommitAsync(commitMessage, cancellationToken).ConfigureAwait(false);

        if (configurationChanged && pushToRemote)
        {
            await this.PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Commits and publishes external configuration edits detected at startup.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SynchronizeConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (!EnvManagerPaths.EnvironmentFile.Exists)
        {
            return;
        }

        IReadOnlyList<(string Status, string Path)> changes = await this.Git.ListPendingChangesAsync(cancellationToken).ConfigureAwait(false);
        if (!changes.Any(change => string.Equals(change.Path, EnvManagerPaths.EnvironmentFileRelativePath, StringComparison.Ordinal)))
        {
            return;
        }

        await this.Git.AddAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
        if (await this.Git.HasStagedChangesForFileAsync(EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false))
        {
            await this.Git.CommitFileAsync("Update configuration.", EnvManagerPaths.EnvironmentFileRelativePath, cancellationToken).ConfigureAwait(false);
            await this.PushConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PushConfigurationAsync(CancellationToken cancellationToken)
    {
        if (!await this.Git.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            ConsoleReporter.Warning("Configuration changes were committed locally. No 'origin' remote is configured; run 'EnvManager push' after configuring one.");
            return;
        }

        ConsoleReporter.Info("Automatically pushing configuration changes to origin.");
        try
        {
            await this.Git.PushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GitCommandException)
        {
            ConsoleReporter.Warning("Configuration changes remain committed locally. Run 'EnvManager push' to retry.");
            throw;
        }

        ConsoleReporter.Success("Automatically pushed configuration changes to origin.");
    }

    /// <summary>Switches to a different environment, optionally creating it if it does not yet exist.</summary>
    /// <param name="environmentName">The name of the environment (branch) to switch to.</param>
    /// <param name="allowCreate">Whether to create the environment when it does not yet exist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SwitchAsync(string environmentName, bool allowCreate, CancellationToken cancellationToken = default)
    {
        await this.Git.CheckoutBranchAsync(environmentName, allowCreate, cancellationToken).ConfigureAwait(false);

        // A freshly created orphan branch has no config.yml yet; write one so
        // subsequent commands have a well-formed configuration to work with.
        if (!EnvManagerPaths.EnvironmentFile.Exists)
        {
            await this.SaveConfigurationAsync(new EnvironmentConfiguration { Name = environmentName }, $"Initialize environment '{environmentName}'.", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _ = await this.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Refreshes generated shell variable exports after an operation replaces the current configuration.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RefreshShellEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        _ = await this.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists all known environments (local and remote branches).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
        => this.Git.ListEnvironmentsAsync(cancellationToken);

    /// <summary>
    /// Stages an existing file from the current working directory for the currently
    /// checked-out environment without committing it.
    /// </summary>
    /// <param name="fileName">The file to add, relative to the current working directory or an absolute path inside the home directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task AddFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string relativePath = ResolveRelativePath(fileName, Environment.CurrentDirectory);
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
        string relativePath = ResolveRelativePath(fileName, Environment.CurrentDirectory);

        if (!await this.Git.IsTrackedAsync(relativePath, cancellationToken).ConfigureAwait(false))
        {
            throw new ConfigurationException($"File '{relativePath}' is not tracked in the current environment.");
        }

        await this.Git.RemoveAsync(relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists every file tracked for the currently checked-out environment, excluding
    /// the internal <c>config.yml</c> configuration file.
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

    /// <summary>
    /// Copies a tracked file's current working-tree contents to another environment
    /// branch and commits it there without changing the active branch or work tree.
    /// </summary>
    /// <param name="fileName">The tracked file path, relative to the home directory.</param>
    /// <param name="environmentName">The target environment branch.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> if a commit was created; otherwise, <see langword="false"/> if the contents already matched.</returns>
    /// <exception cref="ConfigurationException">The file is not tracked, is missing, or the target is invalid.</exception>
    public async Task<bool> CopyFileToEnvironmentAsync(string fileName, string environmentName, CancellationToken cancellationToken = default)
    {
        string relativePath = ResolveRelativePath(fileName, EnvManagerPaths.HomeDirectory.FullName);
        IReadOnlyList<string> files = await this.ListFilesAsync(cancellationToken).ConfigureAwait(false);

        if (!files.Contains(relativePath, StringComparer.Ordinal))
        {
            throw new ConfigurationException($"File '{relativePath}' is not tracked in the current environment.");
        }

        string fullPath = Path.Combine(EnvManagerPaths.HomeDirectory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new ConfigurationException($"Tracked file '{relativePath}' was not found at '{fullPath}'.");
        }

        string currentEnvironment = await this.GetCurrentEnvironmentNameAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(environmentName, currentEnvironment, StringComparison.Ordinal))
        {
            throw new ConfigurationException("The target environment must be different from the current environment.");
        }

        IReadOnlyList<string> environments = await this.ListEnvironmentsAsync(cancellationToken).ConfigureAwait(false);
        if (!environments.Contains(environmentName, StringComparer.Ordinal))
        {
            throw new ConfigurationException($"Environment '{environmentName}' does not exist.");
        }

        return await this.Git.CopyFileToBranchAsync(relativePath, environmentName, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Fast-forwards the current environment to the remote branch when safe.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PullAsync(CancellationToken cancellationToken = default)
        => this.Git.PullAsync(cancellationToken);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Resolves a user-supplied file name (relative to the provided base directory, or
    /// an absolute path) to a path relative to <see cref="EnvManagerPaths.HomeDirectory"/>,
    /// using forward slashes as git expects.
    /// </summary>
    /// <param name="fileName">The file name or path supplied by the user.</param>
    /// <param name="baseDirectory">The directory used to resolve relative paths.</param>
    /// <exception cref="ConfigurationException">
    /// Thrown when the resolved path falls outside of the home directory.
    /// </exception>
    internal static string ResolveRelativePath(string fileName, string baseDirectory)
    {
        string homeDirectory = ResolveDirectoryPath(EnvManagerPaths.HomeDirectory.FullName);

        string fullPath = Path.IsPathRooted(fileName)
            ? Path.GetFullPath(fileName)
            : Path.GetFullPath(Path.Combine(ResolveDirectoryPath(baseDirectory), fileName));
        string containingDirectory = Path.GetDirectoryName(fullPath)!;
        fullPath = Path.Combine(ResolveDirectoryPath(containingDirectory), Path.GetFileName(fullPath));

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

        string gitDirectoryPath = ResolveDirectoryPath(EnvManagerPaths.GitDirectory.FullName);
        string gitDirectoryRelativePath = Path.GetRelativePath(homeDirectory, gitDirectoryPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (string.Equals(gitRelativePath, gitDirectoryRelativePath, comparison)
            || gitRelativePath.StartsWith($"{gitDirectoryRelativePath}/", comparison))
        {
            throw new ConfigurationException($"'{gitDirectoryRelativePath}' contains EnvManager's internal Git repository and cannot be added or removed as a user file.");
        }

        return gitRelativePath;
    }

    private static string ResolveDirectoryPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)!;
        string currentPath = root;

        foreach (string segment in fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string nextPath = Path.Combine(currentPath, segment);
            DirectoryInfo directory = new(nextPath);
            FileSystemInfo? target = Directory.Exists(nextPath)
                ? directory.ResolveLinkTarget(returnFinalTarget: true)
                : null;
            currentPath = target?.FullName ?? nextPath;
        }

        return Path.GetFullPath(currentPath);
    }

    private async Task StageShellDirectoryChangesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> trackedFiles = await this.Git.ListTrackedFilesAsync(cancellationToken).ConfigureAwait(false);
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (string shellDirectory in ShellEnvironmentExporter.ShellDirectoryRelativePaths)
        {
            string directoryPath = Path.Combine(
                EnvManagerPaths.HomeDirectory.FullName,
                shellDirectory.Replace('/', Path.DirectorySeparatorChar));
            bool hasTrackedFiles = trackedFiles.Any(path => path.StartsWith($"{shellDirectory}/", comparison));

            if (Directory.Exists(directoryPath) || hasTrackedFiles)
            {
                await this.Git.StageDirectoryChangesAsync(shellDirectory, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
