// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PackageManagerBase.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.PackageManagers;

/// <summary>
/// Base implementation of <see cref="IPackageManager"/> that runs an external package
/// manager executable and turns non-zero exit codes into <see cref="PackageManagerException"/>.
/// Concrete subclasses only need to describe the executable name and the
/// install/remove/update argument lists specific to that package manager's CLI.
/// </summary>
public abstract class PackageManagerBase : IPackageManager
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Property                                                                 │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public abstract string Name { get; }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Property                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Gets the name of the executable that implements this package manager.</summary>
    protected abstract string Executable { get; }

    /// <summary>Gets the arguments used to probe whether the executable is installed.</summary>
    protected virtual string[] VersionProbeArguments => ["--version"];

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <inheritdoc/>
    public virtual Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => ProcessRunner.IsAvailableAsync(this.Executable, this.VersionProbeArguments, cancellationToken);

    /// <inheritdoc/>
    public Task InstallAsync(string toolName, string? version, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(this.BuildInstallArguments(toolName, version), $"install '{toolName}'", cancellationToken);

    /// <inheritdoc/>
    public Task RemoveAsync(string toolName, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(this.BuildRemoveArguments(toolName), $"remove '{toolName}'", cancellationToken);

    /// <inheritdoc/>
    public Task UpdateAsync(string toolName, CancellationToken cancellationToken = default)
        => this.RunOrThrowAsync(this.BuildUpdateArguments(toolName), $"update '{toolName}'", cancellationToken);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ protected Method                                                                │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Builds the argument list used to install a tool.</summary>
    /// <param name="toolName">The name of the tool/package to install.</param>
    /// <param name="version">The specific version to install, or <see langword="null"/> for the latest.</param>
    protected abstract string[] BuildInstallArguments(string toolName, string? version);

    /// <summary>Builds the argument list used to remove a tool.</summary>
    /// <param name="toolName">The name of the tool/package to remove.</param>
    protected abstract string[] BuildRemoveArguments(string toolName);

    /// <summary>Builds the argument list used to update a tool.</summary>
    /// <param name="toolName">The name of the tool/package to update.</param>
    protected abstract string[] BuildUpdateArguments(string toolName);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Method                                                                  │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Runs the executable with the given arguments and throws on a non-zero exit code.</summary>
    private async Task RunOrThrowAsync(string[] arguments, string actionDescription, CancellationToken cancellationToken)
    {
        (int exitCode, string standardOutput, string standardError) = await ProcessRunner.RunAsync(this.Executable, arguments, cancellationToken).ConfigureAwait(false);

        if (exitCode != 0)
        {
            string details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;

            throw new PackageManagerException($"Failed to {actionDescription} using '{this.Name}' (exit code {exitCode}).{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details.Trim()}")}");
        }
    }
}
