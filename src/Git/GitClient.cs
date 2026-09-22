// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitClient.cs                                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.Git;

/// <summary>
/// A thin wrapper around the external <c>git</c> executable that provides the small
/// subset of operations required to store EnvManager environment configurations as
/// branches of a git repository (clone, checkout, commit, push, pull).
/// </summary>
/// <remarks>
/// <para>
/// EnvManager intentionally shells out to the system-installed <c>git</c> binary
/// instead of depending on a managed git implementation, so that the user's existing
/// authentication (SSH keys, credential helpers, etc.) is reused transparently.
/// </para>
/// <para>
/// Per the EnvManager Git setup, the configuration history is kept in a <b>bare</b>
/// repository (<see cref="GitDirectory"/>) while the actual tracked files (
/// <c>environment.yaml</c> and any files added via <c>files add</c>) live directly in
/// <see cref="WorkingDirectory"/>, which is the user's home directory. Every git
/// invocation therefore explicitly passes <c>--git-dir</c> and <c>--work-tree</c>
/// instead of relying on a <c>.git</c> directory inside the working tree.
/// </para>
/// </remarks>
public sealed class GitClient
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="GitClient"/> class.
    /// </summary>
    /// <param name="workingDirectory">The work tree that git commands are run against (the user's home directory).</param>
    /// <param name="gitDirectory">The bare repository directory backing <paramref name="workingDirectory"/>.</param>
    public GitClient(DirectoryInfo workingDirectory, DirectoryInfo gitDirectory)
    {
        this.WorkingDirectory = workingDirectory;
        this.GitDirectory = gitDirectory;
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the work tree directory that git commands are executed against.</summary>
    public DirectoryInfo WorkingDirectory { get; }

    /// <summary>Gets the bare repository directory (<c>--git-dir</c>) backing <see cref="WorkingDirectory"/>.</summary>
    public DirectoryInfo GitDirectory { get; }

    /// <summary>Gets a value indicating whether <see cref="GitDirectory"/> already holds a bare git repository.</summary>
    public bool IsRepository => File.Exists(Path.Combine(this.GitDirectory.FullName, "HEAD"));

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Clones a bare copy of a remote repository into <see cref="GitDirectory"/> and
    /// checks out the given branch into <see cref="WorkingDirectory"/>, when it already
    /// exists. If the branch does not yet exist on the remote, the bare repository is
    /// still cloned and the caller is expected to create the branch locally afterwards
    /// (e.g. via <see cref="CheckoutBranchAsync"/> with <c>allowCreate: true</c>).
    /// </summary>
    /// <param name="repositoryUrl">The URL of the remote repository.</param>
    /// <param name="branch">The branch to check out after cloning, when it exists.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task CloneAsync(Uri repositoryUrl, string branch, CancellationToken cancellationToken = default)
    {
        this.WorkingDirectory.Create();
        this.GitDirectory.Parent?.Create();

        GitCommandResult cloneResult = await this.RunBareAsync(
            ["clone", "--bare", repositoryUrl.ToString(), this.GitDirectory.FullName],
            cancellationToken).ConfigureAwait(false);

        if (!cloneResult.Succeeded)
        {
            throw new GitCommandException($"clone --bare {repositoryUrl}", cloneResult.ExitCode, cloneResult.StandardError);
        }

        await this.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        if (await this.BranchExistsLocallyAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: false, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Initializes a brand-new, local-only bare git repository (no remote) at
    /// <see cref="GitDirectory"/> with a single unborn branch named <paramref name="branch"/>.
    /// </summary>
    /// <param name="branch">The name of the initial environment branch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task InitAsync(string branch, CancellationToken cancellationToken = default)
    {
        this.WorkingDirectory.Create();
        this.GitDirectory.Parent?.Create();

        await this.RunPlainOrThrowAsync(["init", "--bare", "--initial-branch", branch, this.GitDirectory.FullName], cancellationToken).ConfigureAwait(false);

        await this.ConfigureAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks out the given environment branch, creating it (either as an orphan branch
    /// or by fetching a matching remote branch) if it does not already exist locally.
    /// </summary>
    /// <param name="branch">The name of the branch/environment to check out.</param>
    /// <param name="allowCreate">Whether a missing branch may be created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="GitCommandException">
    /// Thrown when the branch does not exist and <paramref name="allowCreate"/> is false.
    /// </exception>
    public async Task CheckoutBranchAsync(string branch, bool allowCreate, CancellationToken cancellationToken = default)
    {
        if (await this.BranchExistsLocallyAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(await this.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false), branch, StringComparison.Ordinal))
            {
                return;
            }

            await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: true, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await this.RemoteBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            await this.RunOrThrowAsync(["fetch", "origin", $"{branch}:{branch}"], cancellationToken).ConfigureAwait(false);
            await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: true, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!allowCreate)
        {
            throw new GitCommandException($"checkout {branch}", 1, $"Environment '{branch}' does not exist locally or on the remote.");
        }

        await this.PreserveOverwrittenFilesAsync(destinationBranch: null, includeCurrentBranchFiles: true, cancellationToken).ConfigureAwait(false);

        await this.RunOrThrowAsync(["checkout", "--orphan", branch], cancellationToken).ConfigureAwait(false);

        // 'checkout --orphan' carries the previous branch's tree over into the index
        // (but leaves the work tree files untouched on disk). Empty the index so the
        // new branch starts with no tracked files; 'HEAD' is still unborn at this
        // point, so 'git reset' cannot be used here.
        await this.RunOrThrowAsync(["rm", "--cached", "-r", "--ignore-unmatch", "."], cancellationToken).ConfigureAwait(false);

    }

    /// <summary>Gets the name of the currently checked-out branch.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<string> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
    {
        // 'symbolic-ref' resolves the branch name even on an "unborn" branch (a freshly
        // initialized/orphaned branch with no commits yet), unlike 'rev-parse
        // --abbrev-ref HEAD', which fails in that state.
        GitCommandResult result = await this.RunOrThrowAsync(["symbolic-ref", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput.Trim();
    }

    /// <summary>Lists all environments (branches) known locally and, if configured, on the remote.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult localResult = await this.RunOrThrowAsync(
            ["for-each-ref", "--format=%(refname:short)", "refs/heads"],
            cancellationToken).ConfigureAwait(false);

        IEnumerable<string> branches = localResult.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            GitCommandResult remoteResult = await this.RunOrThrowAsync(["ls-remote", "--heads", "origin"], cancellationToken).ConfigureAwait(false);

            IEnumerable<string> remoteBranches = remoteResult.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('\t').Last())
                .Where(reference => reference.StartsWith("refs/heads/", StringComparison.Ordinal))
                .Select(reference => reference["refs/heads/".Length..]);

            branches = branches.Concat(remoteBranches);
        }

        return branches
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Determines whether the given branch exists locally.</summary>
    /// <param name="branch">The branch name to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> BranchExistsLocallyAsync(string branch, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["show-ref", "--verify", "--quiet", $"refs/heads/{branch}"], cancellationToken).ConfigureAwait(false);

        return result.Succeeded;
    }

    /// <summary>Determines whether the given branch exists on the <c>origin</c> remote.</summary>
    /// <param name="branch">The branch name to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> RemoteBranchExistsAsync(string branch, CancellationToken cancellationToken = default)
    {
        if (!await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        GitCommandResult result = await this.RunAsync(["ls-remote", "--exit-code", "--heads", "origin", branch], cancellationToken).ConfigureAwait(false);

        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    /// <summary>Determines whether the repository has an <c>origin</c> remote configured.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> HasRemoteAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["remote"], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("origin", StringComparer.Ordinal);
    }

    /// <summary>Determines whether there are changes staged for commit.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["diff", "--cached", "--quiet"], cancellationToken).ConfigureAwait(false);

        // 'git diff --quiet' exits 1 when there are differences, 0 when there are none.
        return result.ExitCode == 1;
    }

    /// <summary>Stages the given path (relative to <see cref="WorkingDirectory"/>) for commit.</summary>
    /// <param name="relativePath">The repository-relative path to stage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AddAsync(string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["add", relativePath], cancellationToken);

    /// <summary>
    /// Stops tracking the given path (relative to <see cref="WorkingDirectory"/>) without
    /// deleting it from disk, since the work tree is the user's home directory and the
    /// file may still be in active use.
    /// </summary>
    /// <param name="relativePath">The repository-relative path to untrack.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task RemoveAsync(string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["rm", "--cached", "--", relativePath], cancellationToken);

    /// <summary>Lists every file tracked on the currently checked-out branch.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<string>> ListTrackedFilesAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunOrThrowAsync(["ls-files"], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Determines whether the given path (relative to <see cref="WorkingDirectory"/>) is tracked.</summary>
    /// <param name="relativePath">The repository-relative path to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> IsTrackedAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["ls-files", "--error-unmatch", "--", relativePath], cancellationToken).ConfigureAwait(false);

        return result.Succeeded;
    }

    /// <summary>
    /// Commits staged changes with the given message. When there is nothing staged,
    /// this is a no-op rather than an error, so callers can call it unconditionally.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!await this.HasStagedChangesAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await this.RunOrThrowAsync(["commit", "-m", message], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pushes the current branch to the <c>origin</c> remote, setting up tracking if needed.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task PushAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GitCommandException("push", 1, "No 'origin' remote is configured. Clone from a remote repository before pushing.");
        }

        string branch = await this.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false);

        await this.RunOrThrowAsync(["push", "--set-upstream", "origin", branch], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pulls the latest changes for the current branch from the <c>origin</c> remote.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GitCommandException("pull", 1, "No 'origin' remote is configured. Clone from a remote repository before pulling.");
        }

        string branch = await this.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false);

        // The bare repository mirrors 'refs/heads/*' directly (rather than tracking
        // remote branches under 'refs/remotes/origin/*'), so the remote and branch are
        // named explicitly instead of relying on upstream tracking configuration.
        await this.RunOrThrowAsync(["pull", "--ff-only", "origin", branch], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an arbitrary git command against the repository and returns its captured
    /// result without throwing, even for a non-zero exit code.
    /// </summary>
    /// <param name="arguments">The arguments to pass to git.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        => this.RunProcessAsync(this.WorkingDirectory, this.RepositoryArguments.Concat(arguments).ToList(), cancellationToken);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Property                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the <c>--git-dir</c>/<c>--work-tree</c> arguments prefixed to every repository-scoped git invocation.</summary>
    private IReadOnlyList<string> RepositoryArguments =>
    [
        $"--git-dir={this.GitDirectory.FullName}",
        $"--work-tree={this.WorkingDirectory.FullName}",
    ];

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Configures the freshly created/cloned bare repository as described in the Git setup specification.</summary>
    private Task ConfigureAsync(CancellationToken cancellationToken)
        => this.RunOrThrowAsync(["config", "--local", "status.showUntrackedFiles", "no"], cancellationToken);

    /// <summary>
    /// Moves files that the destination checkout could replace to a unique backup
    /// directory before Git changes the work tree.
    /// </summary>
    /// <param name="destinationBranch">The branch that will be checked out, if it already exists.</param>
    /// <param name="includeCurrentBranchFiles">
    /// Whether files tracked by the current branch should also be preserved.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task PreserveOverwrittenFilesAsync(string? destinationBranch, bool includeCurrentBranchFiles, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> destinationFiles = destinationBranch is null
            ? []
            : await this.ListTrackedFilesAsync(destinationBranch, cancellationToken).ConfigureAwait(false);

        IEnumerable<string> filesToPreserve = destinationFiles;

        if (includeCurrentBranchFiles)
        {
            IReadOnlyList<string> currentFiles = await this.ListTrackedFilesAsync(cancellationToken).ConfigureAwait(false);
            filesToPreserve = currentFiles.Concat(destinationFiles);
        }

        List<string> existingFiles = filesToPreserve
            .Distinct(StringComparer.Ordinal)
            .Where(relativePath => File.Exists(Path.Combine(this.WorkingDirectory.FullName, relativePath)))
            .ToList();

        if (existingFiles.Count == 0)
        {
            return;
        }

        string backupDirectory = Path.Combine(
            this.GitDirectory.FullName,
            "overwritten-files",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");

        foreach (string relativePath in existingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath = Path.Combine(this.WorkingDirectory.FullName, relativePath);
            string destinationPath = Path.Combine(backupDirectory, relativePath);
            string? destinationParent = Path.GetDirectoryName(destinationPath);

            if (destinationParent is null)
            {
                throw new IOException($"Could not determine a backup directory for '{relativePath}'.");
            }

            Directory.CreateDirectory(destinationParent);
            File.Move(sourcePath, destinationPath);
        }
    }

    /// <summary>Lists every file tracked by <paramref name="branch"/>.</summary>
    /// <param name="branch">The branch whose tracked files to list.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task<IReadOnlyList<string>> ListTrackedFilesAsync(string branch, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunOrThrowAsync(["ls-tree", "-r", "--name-only", branch], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Runs git with the given arguments (without <c>--git-dir</c>/<c>--work-tree</c>) and returns the captured result.</summary>
    private Task<GitCommandResult> RunBareAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => this.RunProcessAsync(this.WorkingDirectory, arguments, cancellationToken);

    /// <summary>Runs git with the given arguments (without <c>--git-dir</c>/<c>--work-tree</c>) and throws on failure.</summary>
    private async Task RunPlainOrThrowAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunBareAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new GitCommandException(string.Join(' ', arguments), result.ExitCode, result.StandardError);
        }
    }

    /// <summary>Runs git with the given repository-scoped arguments and throws <see cref="GitCommandException"/> on failure.</summary>
    private async Task<GitCommandResult> RunOrThrowAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new GitCommandException(string.Join(' ', arguments), result.ExitCode, result.StandardError);
        }

        return result;
    }

    /// <summary>Runs git with the given arguments in a specific directory and returns the captured result.</summary>
    private async Task<GitCommandResult> RunProcessAsync(DirectoryInfo directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = directory.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo,
        };

        StringBuilder standardOutput = new();
        StringBuilder standardError = new();

        process.OutputDataReceived += (_, eventArguments) =>
        {
            if (eventArguments.Data is not null)
            {
                standardOutput.AppendLine(eventArguments.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArguments) =>
        {
            if (eventArguments.Data is not null)
            {
                standardError.AppendLine(eventArguments.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new GitCommandException(exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new GitCommandResult(process.ExitCode, standardOutput.ToString().TrimEnd(), standardError.ToString().TrimEnd());
    }
}
