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
/// EnvManager intentionally shells out to the system-installed <c>git</c> binary
/// instead of depending on a managed git implementation, so that the user's existing
/// authentication (SSH keys, credential helpers, etc.) is reused transparently.
/// </remarks>
public sealed class GitClient
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Initializes a new instance of the <see cref="GitClient"/> class.
    /// </summary>
    /// <param name="workingDirectory">The working tree that git commands are run against.</param>
    public GitClient(DirectoryInfo workingDirectory)
    {
        this.WorkingDirectory = workingDirectory;
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the working tree directory that git commands are executed against.</summary>
    public DirectoryInfo WorkingDirectory { get; }

    /// <summary>Gets a value indicating whether <see cref="WorkingDirectory"/> is a git repository.</summary>
    public bool IsRepository => Directory.Exists(Path.Combine(this.WorkingDirectory.FullName, ".git"));

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Clones a repository into <see cref="WorkingDirectory"/> checking out the given
    /// branch. If the branch does not yet exist on the remote, the clone still succeeds
    /// and the caller is expected to create the branch locally afterwards.
    /// </summary>
    /// <param name="repositoryUrl">The URL of the remote repository.</param>
    /// <param name="branch">The branch to check out after cloning, when it exists.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task CloneAsync(Uri repositoryUrl, string branch, CancellationToken cancellationToken = default)
    {
        this.WorkingDirectory.Parent?.Create();

        GitCommandResult branchAwareResult = await this.RunInDirectoryAsync(
            this.WorkingDirectory.Parent ?? this.WorkingDirectory,
            [
                "clone",
                "--branch", branch,
                "--origin", "origin",
                repositoryUrl.ToString(),
                this.WorkingDirectory.FullName,
            ],
            cancellationToken).ConfigureAwait(false);

        if (branchAwareResult.Succeeded)
        {
            return;
        }

        // The requested branch may simply not exist yet on the remote (e.g. this is the
        // first environment ever pushed to this repository). Fall back to cloning the
        // repository's default branch so the local checkout still succeeds; the caller
        // is responsible for creating the requested environment branch afterwards.
        GitCommandResult fallbackResult = await this.RunInDirectoryAsync(
            this.WorkingDirectory.Parent ?? this.WorkingDirectory,
            ["clone", "--origin", "origin", repositoryUrl.ToString(), this.WorkingDirectory.FullName],
            cancellationToken).ConfigureAwait(false);

        if (!fallbackResult.Succeeded)
        {
            throw new GitCommandException($"clone {repositoryUrl}", fallbackResult.ExitCode, fallbackResult.StandardError);
        }
    }

    /// <summary>
    /// Initializes a brand-new, local-only git repository (no remote) in
    /// <see cref="WorkingDirectory"/> with a single orphan branch named
    /// <paramref name="branch"/>.
    /// </summary>
    /// <param name="branch">The name of the initial environment branch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task InitAsync(string branch, CancellationToken cancellationToken = default)
    {
        this.WorkingDirectory.Create();

        await this.RunOrThrowAsync(["init", "--initial-branch", branch, "."], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks out the given environment branch, creating it (either as an orphan branch
    /// or tracking a matching remote branch) if it does not already exist locally.
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
            await this.RunOrThrowAsync(["checkout", branch], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await this.RemoteBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false))
        {
            await this.RunOrThrowAsync(["checkout", "-t", $"origin/{branch}"], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!allowCreate)
        {
            throw new GitCommandException($"checkout {branch}", 1, $"Environment '{branch}' does not exist locally or on the remote.");
        }

        // Create a fresh orphan branch so that each environment's history is
        // independent, as required by the branch-per-environment storage model. The
        // orphan checkout carries over the previous branch's working-tree files as
        // untracked content, so they are unstaged and then removed from disk as well
        // to leave a clean slate for the new environment.
        await this.RunOrThrowAsync(["checkout", "--orphan", branch], cancellationToken).ConfigureAwait(false);
        await this.RunOrThrowAsync(["rm", "-rf", "--cached", "--ignore-unmatch", "."], cancellationToken).ConfigureAwait(false);
        await this.RunOrThrowAsync(["clean", "-fd"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the name of the currently checked-out branch.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<string> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
    {
        // 'symbolic-ref' resolves the branch name even on an "unborn" branch (a freshly
        // orphaned/initialized branch with no commits yet), unlike 'rev-parse
        // --abbrev-ref HEAD', which fails in that state.
        GitCommandResult result = await this.RunOrThrowAsync(["symbolic-ref", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);

        return result.StandardOutput.Trim();
    }

    /// <summary>Lists all environments (branches) known locally and, if configured, on the remote.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        if (await this.HasRemoteAsync(cancellationToken).ConfigureAwait(false))
        {
            await this.RunOrThrowAsync(["fetch", "origin"], cancellationToken).ConfigureAwait(false);
        }

        GitCommandResult result = await this.RunOrThrowAsync(
            ["for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes"],
            cancellationToken).ConfigureAwait(false);

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(reference => reference.StartsWith("origin/", StringComparison.Ordinal) ? reference["origin/".Length..] : reference)
            .Where(reference => reference is not ("HEAD" or "origin"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Stages the given path (relative to <see cref="WorkingDirectory"/>) for commit.</summary>
    /// <param name="relativePath">The repository-relative path to stage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AddAsync(string relativePath, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(["add", relativePath], cancellationToken);

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

        await this.RunOrThrowAsync(["pull", "--ff-only"], cancellationToken).ConfigureAwait(false);
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

        await this.RunAsync(["fetch", "origin"], cancellationToken).ConfigureAwait(false);

        GitCommandResult result = await this.RunAsync(["show-ref", "--verify", "--quiet", $"refs/remotes/origin/{branch}"], cancellationToken).ConfigureAwait(false);

        return result.Succeeded;
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

    /// <summary>
    /// Runs an arbitrary git command against <see cref="WorkingDirectory"/> and returns
    /// its captured result without throwing, even for a non-zero exit code.
    /// </summary>
    /// <param name="arguments">The arguments to pass to git.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        => this.RunInDirectoryAsync(this.WorkingDirectory, arguments, cancellationToken);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Runs git with the given arguments and throws <see cref="GitCommandException"/> on failure.</summary>
    private async Task<GitCommandResult> RunOrThrowAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunInDirectoryAsync(this.WorkingDirectory, arguments, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new GitCommandException(string.Join(' ', arguments), result.ExitCode, result.StandardError);
        }

        return result;
    }

    /// <summary>Runs git with the given arguments in a specific directory and returns the captured result.</summary>
    private async Task<GitCommandResult> RunInDirectoryAsync(DirectoryInfo directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
