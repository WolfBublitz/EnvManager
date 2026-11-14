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
/// <c>.EnvManager/config.yml</c> and any files added via <c>files add</c>) live directly in
/// <see cref="WorkingDirectory"/>, which is the user's home directory. Every git
/// invocation therefore explicitly passes <c>--git-dir</c> and <c>--work-tree</c>
/// instead of relying on a <c>.git</c> directory inside the working tree.
/// </para>
/// </remarks>
public sealed class GitClient
{
    private readonly List<string> backupsCreated = [];
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

    /// <summary>Gets backup directories created by this client during the current operation.</summary>
    public IReadOnlyList<string> BackupsCreated => this.backupsCreated;

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
            string cloneArguments = $"clone --bare {repositoryUrl} {this.GitDirectory.FullName}";
            throw new GitCommandException(cloneArguments, $"git {cloneArguments}", cloneResult.ExitCode, cloneResult.StandardOutput, cloneResult.StandardError);
        }

        await this.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        if (await this.BranchExistsLocallyAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyList<string> preserved = await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: false, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            await this.RestorePreservedCheckoutFilesAsync(preserved, cancellationToken).ConfigureAwait(false);
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

            IReadOnlyList<string> preserved = await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: true, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            await this.RestorePreservedCheckoutFilesAsync(preserved, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await this.RemoteBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            await this.RunOrThrowAsync(["fetch", "origin", $"{branch}:{branch}"], cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> preserved = await this.PreserveOverwrittenFilesAsync(branch, includeCurrentBranchFiles: true, cancellationToken).ConfigureAwait(false);
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            await this.RestorePreservedCheckoutFilesAsync(preserved, cancellationToken).ConfigureAwait(false);
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
        GitCommandResult result = await this.RunOrThrowAsync(["remote"], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("origin", StringComparer.Ordinal);
    }

    /// <summary>Adds or updates the origin remote.</summary>
    public async Task SetRemoteAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith('-'))
        {
            throw new ConfigurationException("Provide a valid Git remote URL or path.");
        }

        bool exists = await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false);
        await this.RunOrThrowAsync(exists ? ["remote", "set-url", "origin", url] : ["remote", "add", "origin", url], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Determines whether there are changes staged for commit.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["diff", "--cached", "--quiet"], cancellationToken).ConfigureAwait(false);

        // 'git diff --quiet' exits 1 when there are differences, 0 when there are none.
        return result.ExitCode == 1;
    }

    /// <summary>Determines whether a specific path has changes staged for commit.</summary>
    /// <param name="relativePath">The repository-relative path to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> HasStagedChangesForFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["diff", "--cached", "--quiet", "--", relativePath], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode is not (0 or 1))
        {
            throw new GitCommandException($"diff --cached --quiet -- {relativePath}", result.ExitCode, result.StandardOutput, result.StandardError);
        }

        return result.ExitCode == 1;
    }

    /// <summary>Determines whether a specific path has unstaged changes.</summary>
    /// <param name="relativePath">The repository-relative path to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<bool> HasUnstagedChangesForFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunAsync(["diff", "--quiet", "--", relativePath], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode is not (0 or 1))
        {
            throw new GitCommandException($"diff --quiet -- {relativePath}", result.ExitCode, result.StandardOutput, result.StandardError);
        }

        return result.ExitCode == 1;
    }

    /// <summary>Stages the given path (relative to <see cref="WorkingDirectory"/>) for commit.</summary>
    /// <param name="relativePath">The repository-relative path to stage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AddAsync(string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["add", "--", relativePath], cancellationToken);

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

        if (result.ExitCode is not (0 or 1))
        {
            throw new GitCommandException($"ls-files --error-unmatch -- {relativePath}", result.ExitCode, result.StandardOutput, result.StandardError);
        }

        return result.Succeeded;
    }

    /// <summary>
    /// Commits the current work-tree contents of a tracked file to another branch,
    /// leaving the active branch, index, and work tree unchanged.
    /// </summary>
    /// <param name="relativePath">The tracked file path relative to <see cref="WorkingDirectory"/>.</param>
    /// <param name="branch">The target branch.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> if a commit was created; otherwise, <see langword="false"/> if the contents already matched.</returns>
    /// <exception cref="GitCommandException">The file is not tracked, the branch does not exist, or a Git operation fails.</exception>
    public async Task<bool> CopyFileToBranchAsync(string relativePath, string branch, CancellationToken cancellationToken = default)
    {
        if (!await this.IsTrackedAsync(relativePath, cancellationToken).ConfigureAwait(false))
        {
            throw new GitCommandException($"ls-files --error-unmatch -- {relativePath}", 1, string.Empty, $"File '{relativePath}' is not tracked.");
        }

        if (!await this.BranchExistsLocallyAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            if (!await this.RemoteBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false))
            {
                throw new GitCommandException($"copy file to {branch}", 1, string.Empty, $"Environment '{branch}' does not exist locally or on the remote.");
            }

            await this.RunOrThrowAsync(["fetch", "origin", $"refs/heads/{branch}:refs/heads/{branch}"], cancellationToken).ConfigureAwait(false);
        }

        string targetReference = $"refs/heads/{branch}";
        string targetCommit = (await this.RunOrThrowAsync(["rev-parse", "--verify", $"{targetReference}^{{commit}}"], cancellationToken).ConfigureAwait(false))
            .StandardOutput
            .Trim();
        string temporaryIndex = Path.Combine(this.GitDirectory.FullName, $"index-{Guid.NewGuid():N}");

        try
        {
            await this.RunWithIndexOrThrowAsync(["--literal-pathspecs", "read-tree", targetReference], temporaryIndex, cancellationToken).ConfigureAwait(false);
            await this.RunWithIndexOrThrowAsync(["--literal-pathspecs", "add", "--force", "--", relativePath], temporaryIndex, cancellationToken).ConfigureAwait(false);
            string newTree = (await this.RunWithIndexOrThrowAsync(["write-tree"], temporaryIndex, cancellationToken).ConfigureAwait(false))
                .StandardOutput
                .Trim();
            string oldTree = (await this.RunOrThrowAsync(["rev-parse", "--verify", $"{targetReference}^{{tree}}"], cancellationToken).ConfigureAwait(false))
                .StandardOutput
                .Trim();

            if (string.Equals(newTree, oldTree, StringComparison.Ordinal))
            {
                return false;
            }

            string currentBranch = await this.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false);
            string commitMessage = $"Copy file '{relativePath}' from environment '{currentBranch}'.";
            string newCommit = (await this.RunOrThrowAsync(
                ["commit-tree", newTree, "-p", targetCommit, "-m", commitMessage],
                cancellationToken).ConfigureAwait(false))
                .StandardOutput
                .Trim();

            await this.RunOrThrowAsync(["update-ref", targetReference, newCommit, targetCommit], cancellationToken).ConfigureAwait(false);

            return true;
        }
        finally
        {
            File.Delete(temporaryIndex);
            File.Delete($"{temporaryIndex}.lock");
        }
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

    /// <summary>Commits changes to a single file without including other staged files.</summary>
    /// <param name="message">The Git commit message.</param>
    /// <param name="relativePath">The file path relative to the work tree.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task CommitFileAsync(string message, string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["commit", "--only", "-m", message, "--", relativePath], cancellationToken);

    /// <summary>
    /// Stages modifications and deletions of all tracked files. Untracked files are left
    /// alone, since the work tree is the user's home directory.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task StageTrackedChangesAsync(CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["add", "--update"], cancellationToken);

    /// <summary>Stages additions, modifications, and deletions under the specified work-tree path.</summary>
    /// <param name="relativePath">The repository-relative directory or path to stage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task StageDirectoryChangesAsync(string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["add", "--all", "--", relativePath], cancellationToken);

    /// <summary>
    /// Lists pending changes to tracked files as <c>(status, path)</c> pairs, where status is
    /// <c>A</c>, <c>M</c> or <c>D</c>. Untracked files are not included.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<(string Status, string Path)>> ListPendingChangesAsync(CancellationToken cancellationToken = default)
    {
        GitCommandResult result = await this.RunOrThrowAsync(["status", "--porcelain", "--untracked-files=no", "--no-renames"], cancellationToken).ConfigureAwait(false);

        List<(string Status, string Path)> changes = [];

        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 4)
            {
                continue;
            }

            char status = line[0] != ' ' ? line[0] : line[1];
            changes.Add((status.ToString(), line[3..].Trim().Trim('"')));
        }

        return changes;
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

    /// <summary>Publishes a local environment branch without changing the active branch.</summary>
    public async Task PushBranchAsync(string branch, CancellationToken cancellationToken = default)
    {
        if (!await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GitCommandException("push", 1, "No 'origin' remote is configured. Run 'EnvManager remote set <url>' first.");
        }

        await this.RunOrThrowAsync(["push", "origin", $"refs/heads/{branch}:refs/heads/{branch}"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the corresponding branch from <c>origin</c> and fast-forwards when safe.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        string target = await this.FetchPullTargetAsync(cancellationToken).ConfigureAwait(false);
        await this.FastForwardPullAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches and previews the changes and local data affected by a pull.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The fetched commit and descriptions of incoming and local changes.</returns>
    public async Task<(string Target, string Incoming, string LocalChanges, string LocalCommits, IReadOnlyList<string> Obstructions)> PreviewPullAsync(CancellationToken cancellationToken = default)
    {
        string target = await this.FetchPullTargetAsync(cancellationToken).ConfigureAwait(false);
        GitCommandResult incoming = await this.RunOrThrowAsync(["diff", "--no-ext-diff", "--no-renames", "--name-status", "HEAD", target, "--"], cancellationToken).ConfigureAwait(false);
        GitCommandResult localChanges = await this.RunOrThrowAsync(["status", "--porcelain", "--untracked-files=no", "--no-renames"], cancellationToken).ConfigureAwait(false);
        GitCommandResult localCommits = await this.RunOrThrowAsync(["log", "--oneline", $"{target}..HEAD", "--"], cancellationToken).ConfigureAwait(false);
        GitCommandResult tracked = await this.RunOrThrowAsync(["ls-files", "-z"], cancellationToken).ConfigureAwait(false);
        HashSet<string> trackedPaths = new(tracked.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        IReadOnlyList<string> targetPaths = await this.ListTrackedFilesAsync(target, cancellationToken).ConfigureAwait(false);
        HashSet<string> obstructions = new(StringComparer.Ordinal);

        foreach (string path in targetPaths)
        {
            string fullPath = Path.Combine(this.WorkingDirectory.FullName, path);
            if (Directory.Exists(fullPath) || (File.Exists(fullPath) && !trackedPaths.Contains(path)))
            {
                obstructions.Add(path);
            }

            string? parent = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(parent))
            {
                if (File.Exists(Path.Combine(this.WorkingDirectory.FullName, parent)) && !trackedPaths.Contains(parent))
                {
                    obstructions.Add(parent);
                }

                parent = Path.GetDirectoryName(parent);
            }
        }

        return (target, incoming.StandardOutput, localChanges.StandardOutput, localCommits.StandardOutput, obstructions.OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>Fast-forwards to the fetched commit without discarding local changes.</summary>
    public Task FastForwardPullAsync(string target, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["-c", "core.bare=false", "merge", "--ff-only", "--no-edit", target], cancellationToken);

    private async Task<string> FetchPullTargetAsync(CancellationToken cancellationToken)
    {
        if (!await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new GitCommandException("pull", 1, "No 'origin' remote is configured. Clone from a remote repository before pulling.");
        }

        string branch = await this.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false);

        // Fetch only into FETCH_HEAD: a bare clone may map remote branches directly
        // onto local branches, which must not be updated before the fetch succeeds.
        await this.RunOrThrowAsync(["fetch", "--no-tags", "--refmap=", "origin", $"refs/heads/{branch}"], cancellationToken).ConfigureAwait(false);
        GitCommandResult result = await this.RunOrThrowAsync(["rev-parse", "--verify", "FETCH_HEAD^{commit}"], cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.Trim();
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
    private async Task<IReadOnlyList<string>> PreserveOverwrittenFilesAsync(string? destinationBranch, bool includeCurrentBranchFiles, CancellationToken cancellationToken)
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

        GitCommandResult head = await this.RunAsync(["rev-parse", "--verify", "--quiet", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (head.Succeeded)
        {
            filesToPreserve = filesToPreserve.Concat(await this.ListTrackedFilesAsync("HEAD", cancellationToken).ConfigureAwait(false));
        }
        else if (head.ExitCode != 1)
        {
            const string arguments = "rev-parse --verify --quiet HEAD";
            string command = $"git {string.Join(' ', this.RepositoryArguments)} {arguments}";
            throw new GitCommandException(arguments, command, head.ExitCode, head.StandardOutput, head.StandardError);
        }

        List<string> existingFiles = filesToPreserve
            .Distinct(StringComparer.Ordinal)
            .Where(relativePath => File.Exists(Path.Combine(this.WorkingDirectory.FullName, relativePath)))
            .ToList();

        if (existingFiles.Count == 0)
        {
            return [];
        }

        string backupDirectory = Path.Combine(
            this.GitDirectory.FullName,
            "overwritten-files",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(backupDirectory);
        this.backupsCreated.Add(backupDirectory);
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

        return existingFiles;
    }

    private async Task RestorePreservedCheckoutFilesAsync(IReadOnlyList<string> preserved, CancellationToken cancellationToken)
    {
        GitCommandResult index = await this.RunOrThrowAsync(["ls-files", "-z"], cancellationToken).ConfigureAwait(false);
        HashSet<string> tracked = new(index.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        List<string> missing = preserved.Where(path =>
        {
            string fullPath = Path.Combine(this.WorkingDirectory.FullName, path);
            return tracked.Contains(path)
                && !File.Exists(fullPath)
                && !Directory.Exists(fullPath)
                && new FileInfo(fullPath).LinkTarget is null;
        }).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        string temporaryDirectory = Path.Combine(this.GitDirectory.FullName, $"checkout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            foreach (string path in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await this.RunOrThrowAsync(
                    ["checkout-index", $"--prefix={temporaryDirectory}{Path.DirectorySeparatorChar}", "--", path],
                    cancellationToken).ConfigureAwait(false);
                string destination = Path.Combine(this.WorkingDirectory.FullName, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(Path.Combine(temporaryDirectory, path), destination, overwrite: false);
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    /// <summary>Lists every file tracked by <paramref name="branch"/>.</summary>
    /// <param name="branch">The branch whose tracked files to list.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task<IReadOnlyList<string>> ListTrackedFilesAsync(string branch, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunOrThrowAsync(["ls-tree", "-r", "--name-only", "-z", branch], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
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
            throw new GitCommandException(string.Join(' ', arguments), result.ExitCode, result.StandardOutput, result.StandardError);
        }
    }

    /// <summary>Runs git with the given repository-scoped arguments and throws <see cref="GitCommandException"/> on failure.</summary>
    private async Task<GitCommandResult> RunOrThrowAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            string operationArguments = string.Join(' ', arguments);
            string command = $"git {string.Join(' ', this.RepositoryArguments.Concat(arguments))}";

            throw new GitCommandException(operationArguments, command, result.ExitCode, result.StandardOutput, result.StandardError);
        }

        return result;
    }

    /// <summary>Runs a repository-scoped Git command using an isolated index.</summary>
    private Task<GitCommandResult> RunWithIndexAsync(
        IReadOnlyList<string> arguments,
        string indexFile,
        CancellationToken cancellationToken)
        => this.RunProcessAsync(
            this.WorkingDirectory,
            this.RepositoryArguments.Concat(arguments).ToList(),
            cancellationToken,
            indexFile);

    /// <summary>Runs a repository-scoped Git command using an isolated index and throws on failure.</summary>
    private async Task<GitCommandResult> RunWithIndexOrThrowAsync(
        IReadOnlyList<string> arguments,
        string indexFile,
        CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunWithIndexAsync(arguments, indexFile, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            string operationArguments = string.Join(' ', arguments);
            string command = $"git {string.Join(' ', this.RepositoryArguments.Concat(arguments))}";

            throw new GitCommandException(operationArguments, command, result.ExitCode, result.StandardOutput, result.StandardError);
        }

        return result;
    }

    /// <summary>Runs git with the given arguments in a specific directory and returns the captured result.</summary>
    private async Task<GitCommandResult> RunProcessAsync(
        DirectoryInfo directory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? indexFile = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        if (indexFile is not null)
        {
            startInfo.Environment["GIT_INDEX_FILE"] = indexFile;
        }

        using Process process = new()
        {
            StartInfo = startInfo,
        };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new GitCommandException($"git {string.Join(' ', arguments)}", exception);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
                catch (System.ComponentModel.Win32Exception exception)
                {
                    throw new IOException("Git was cancelled, but its process could not be terminated.", exception);
                }
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new GitCommandResult(
            process.ExitCode,
            (await standardOutput.ConfigureAwait(false)).TrimEnd(),
            (await standardError.ConfigureAwait(false)).TrimEnd());
    }
}
