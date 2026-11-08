// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: DowngradeCommand.cs                                                      │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-08                                                           │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Updates;
using Semver;
using Spectre.Console;

namespace EnvManager.CommandLine;

public sealed class DowngradeCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                            │
    // └────────────────────────────────────────────────────────────────────────────────┘
    public DowngradeCommand()
        : base("downgrade", "Selects and installs an earlier version of EnvManager.")
    {
        Option<string?> versionOption = new("--version")
        {
            Description = "Install this available earlier version without prompting.",
        };
        this.Add(versionOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            using HttpClient httpClient = new();
            GitHubReleaseClient releaseClient = new(httpClient);
            IReadOnlyList<UpdateRelease> availableReleases = Array.Empty<UpdateRelease>();

            await ConsoleReporter.RunWithStatusAsync(
                $"Checking for EnvManager downgrades (current version {Program.ApplicationVersion})",
                async () =>
                {
                    availableReleases = await releaseClient.GetAvailableDowngradesAsync(
                        Program.ApplicationVersion,
                        UpdatePlatform.GetAssetName(),
                        cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);

            string? requestedVersion = parseResult.GetValue(versionOption);
            if (availableReleases.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(requestedVersion))
                {
                    throw new ConfigurationException(
                        $"Version '{requestedVersion}' is not an available downgrade.");
                }

                ConsoleReporter.Success(
                    $"No earlier EnvManager versions are available than {Program.ApplicationVersion}.");
                return;
            }

            bool interactive =
                AnsiConsole.Profile.Capabilities.Interactive
                && !Console.IsInputRedirected;

            UpdateRelease selectedRelease = SelectRelease(
                availableReleases,
                requestedVersion,
                interactive,
                releases => AnsiConsole.Prompt(
                    new SelectionPrompt<UpdateRelease>()
                        .Title("Select an earlier EnvManager version to install")
                        .PageSize(10)
                        .UseConverter(release =>
                            $"{release.Version} — {Markup.Escape(release.Name)}")
                        .AddChoices(releases)));

            string executablePath = UpdatePlatform.GetRunningExecutablePath();
            ExecutableUpdater updater = new(httpClient);

            await ConsoleReporter.RunWithStatusAsync(
                $"Downloading EnvManager {selectedRelease.Version}",
                () => updater.DownloadAndReplaceAsync(
                    selectedRelease,
                    executablePath,
                    Environment.ProcessId,
                    cancellationToken)).ConfigureAwait(false);

            if (OperatingSystem.IsWindows())
            {
                ConsoleReporter.Success(
                    $"EnvManager {selectedRelease.Version} will be installed after this process exits. Restart EnvManager to use it.");
            }
            else
            {
                ConsoleReporter.Success(
                    $"Downgraded EnvManager to version {selectedRelease.Version}.");
            }
        }));
    }

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ internal Method                                                               │
    // └────────────────────────────────────────────────────────────────────────────────┘
    internal static UpdateRelease SelectRelease(
        IReadOnlyList<UpdateRelease> availableReleases,
        string? requestedVersion,
        bool interactive,
        Func<IReadOnlyList<UpdateRelease>, UpdateRelease> prompt)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            if (!GitHubReleaseClient.TryParseTag(
                    requestedVersion,
                    out SemVersion? parsedVersion))
            {
                throw new ConfigurationException(
                    $"'{requestedVersion}' is not a valid semantic version.");
            }

            return availableReleases.FirstOrDefault(
                release => release.Version.ComparePrecedenceTo(parsedVersion) == 0)
                ?? throw new ConfigurationException(
                    $"Version '{requestedVersion}' is not an available downgrade.");
        }

        return availableReleases.Count switch
        {
            0 => throw new ConfigurationException(
                "No compatible downgrades are available."),

            1 => availableReleases[0],

            _ when !interactive => throw new ConfigurationException(
                "Multiple EnvManager downgrades are available. Specify one with 'EnvManager downgrade --version <version>'."),

            _ => prompt(availableReleases),
        };
    }
}
