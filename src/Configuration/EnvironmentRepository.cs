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
    public static bool IsInitialized => new GitClient(EnvManagerPaths.RepositoryDirectory).IsRepository;

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
        DirectoryInfo repositoryDirectory = EnvManagerPaths.RepositoryDirectory;

        if (repositoryDirectory.Exists && repositoryDirectory.EnumerateFileSystemInfos().Any())
        {
            throw new ConfigurationException($"An EnvManager repository already exists at '{repositoryDirectory.FullName}'. Remove it before cloning again.");
        }

        GitClient git = new(repositoryDirectory);

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
        DirectoryInfo repositoryDirectory = EnvManagerPaths.RepositoryDirectory;

        if (repositoryDirectory.Exists && repositoryDirectory.EnumerateFileSystemInfos().Any())
        {
            throw new ConfigurationException($"An EnvManager repository already exists at '{repositoryDirectory.FullName}'. Remove it before initializing again.");
        }

        GitClient git = new(repositoryDirectory);

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
                $"No EnvManager repository was found at '{EnvManagerPaths.RepositoryDirectory.FullName}'. " +
                "Run 'envmanager clone <repository_url>' or 'envmanager init' first.");
        }

        return new EnvironmentRepository(new GitClient(EnvManagerPaths.RepositoryDirectory));
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

        await this.Git.AddAsync("environment.yaml", cancellationToken).ConfigureAwait(false);
        await this.Git.CommitAsync(commitMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Switches to a different environment, optionally creating it if it does not yet exist.</summary>
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

    /// <summary>Pushes the current environment's committed changes to the remote repository.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PushAsync(CancellationToken cancellationToken = default)
        => this.Git.PushAsync(cancellationToken);

    /// <summary>Pulls the latest committed changes for the current environment from the remote repository.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PullAsync(CancellationToken cancellationToken = default)
        => this.Git.PullAsync(cancellationToken);
}
