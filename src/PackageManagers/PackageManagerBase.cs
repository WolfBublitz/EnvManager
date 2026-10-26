// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: PackageManagerBase.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
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

    /// <inheritdoc/>
    public virtual bool RequiresElevation => false;

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
        bool useSudo = this.RequiresElevation && OperatingSystem.IsLinux() && GetEffectiveUserId() != 0;
        string executable = useSudo ? "sudo" : this.Executable;
        string[] commandArguments = useSudo ? [this.Executable, .. arguments] : arguments;
        (int exitCode, string standardOutput, string standardError) result;

        try
        {
            result = await ProcessRunner.RunAsync(executable, commandArguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception exception) when (useSudo && exception.NativeErrorCode == 2)
        {
            throw new PackageManagerException($"Cannot {actionDescription} using '{this.Name}': sudo was not found. Install sudo or run EnvManager as root.", exception);
        }

        (int exitCode, string standardOutput, string standardError) = result;

        if (exitCode != 0)
        {
            string details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;

            throw new PackageManagerException($"Failed to {actionDescription} using '{this.Name}' (exit code {exitCode}).{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details.Trim()}")}");
        }

    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}
