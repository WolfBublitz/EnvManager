// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ExecutableUpdater.cs                                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;

namespace EnvManager.Updates;

internal sealed class ExecutableUpdater
{
    private readonly HttpClient httpClient;

    public ExecutableUpdater(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task DownloadAndReplaceAsync(
        UpdateRelease release,
        string executablePath,
        int processId,
        CancellationToken cancellationToken)
    {
        string fullExecutablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullExecutablePath))
        {
            throw new ConfigurationException($"The EnvManager executable was not found at '{fullExecutablePath}'.");
        }

        string directory = Path.GetDirectoryName(fullExecutablePath)
            ?? throw new ConfigurationException($"Could not determine the installation directory for '{fullExecutablePath}'.");
        string stagedPath = Path.Combine(directory, $".EnvManager-update-{Guid.NewGuid():N}{Path.GetExtension(fullExecutablePath)}");
        bool replacementScheduled = false;

        try
        {
            using HttpResponseMessage response = await this.httpClient.GetAsync(
                release.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (FileStream destination = new(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                await content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            await using (FileStream downloadedFile = new(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = await SHA256.HashDataAsync(downloadedFile, cancellationToken).ConfigureAwait(false);
                string actualDigest = Convert.ToHexStringLower(hash);
                if (!string.Equals(actualDigest, release.Sha256, StringComparison.Ordinal))
                {
                    throw new ConfigurationException(
                        $"The downloaded update failed SHA-256 verification. Expected {release.Sha256}, received {actualDigest}.");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), $"EnvManager-update-{Guid.NewGuid():N}.ps1");
                try
                {
                    await File.WriteAllTextAsync(scriptPath, CreateWindowsReplacementScript(), cancellationToken).ConfigureAwait(false);
                    using Process helper = new()
                    {
                        StartInfo = CreateWindowsHelperStartInfo(scriptPath, processId, stagedPath, fullExecutablePath),
                    };
                    if (!helper.Start())
                    {
                        throw new ConfigurationException("Could not start the helper process to replace EnvManager after it exits.");
                    }

                    replacementScheduled = true;
                }
                finally
                {
                    if (!replacementScheduled && File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                    }
                }
            }
            else
            {
                UnixFileMode mode = File.GetUnixFileMode(fullExecutablePath);
                File.SetUnixFileMode(stagedPath, mode);
                File.Move(stagedPath, fullExecutablePath, overwrite: true);
            }
        }
        finally
        {
            if (!replacementScheduled && File.Exists(stagedPath))
            {
                File.Delete(stagedPath);
            }
        }
    }

    internal static string CreateWindowsReplacementScript()
        => """
           param(
               [Parameter(Mandatory = $true)][int]$ProcessId,
               [Parameter(Mandatory = $true)][string]$SourcePath,
               [Parameter(Mandatory = $true)][string]$DestinationPath,
               [Parameter(Mandatory = $true)][string]$ScriptPath
           )
           $ErrorActionPreference = 'Stop'
           if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
               Wait-Process -Id $ProcessId
           }
           Move-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
           Remove-Item -LiteralPath $ScriptPath -Force
           """;

    private static ProcessStartInfo CreateWindowsHelperStartInfo(
        string scriptPath,
        int processId,
        string stagedPath,
        string executablePath)
    {
        ProcessStartInfo startInfo = new("powershell.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ProcessId");
        startInfo.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-SourcePath");
        startInfo.ArgumentList.Add(stagedPath);
        startInfo.ArgumentList.Add("-DestinationPath");
        startInfo.ArgumentList.Add(executablePath);
        startInfo.ArgumentList.Add("-ScriptPath");
        startInfo.ArgumentList.Add(scriptPath);
        return startInfo;
    }
}
