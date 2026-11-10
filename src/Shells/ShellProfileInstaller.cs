// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellProfileInstaller.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using EnvManager.Exceptions;

namespace EnvManager.Shells;

/// <summary>
/// Installs and removes the EnvManager loader in supported shell profiles,
/// preserving original shell profile contents as addon files in the user's EnvManager shell directory.
/// </summary>
internal sealed class ShellProfileInstaller
{
    private const string LegacyBashProfile = "for file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";
    private const string BashProfile = LegacyBashProfile;
    private const string PreviousBashProfile = "export PATH=\"$HOME/.local/bin:$PATH\"\n\n" + LegacyBashProfile;
    private const string LegacyZshProfile = "for file in \"$HOME/.EnvManager/zsh/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";
    private const string ZshProfile = LegacyZshProfile;
    private const string PreviousZshProfile = "export PATH=\"$HOME/.local/bin:$PATH\"\n\n" + LegacyZshProfile;
    private const string LegacyFishProfile = "if test -d $HOME/.EnvManager/fish\n    for file in $HOME/.EnvManager/fish/*.fish\n        source \"$file\"\n    end\nend\n";
    private const string FishProfile = LegacyFishProfile;
    private const string PreviousFishProfile = "set -gx PATH \"$HOME/.local/bin\" $PATH\n\n" + LegacyFishProfile;
    private const string LegacyPowerShellProfile = "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";
    private const string PowerShellProfile = "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | Sort-Object Name | ForEach-Object { . $_.FullName }\n}\n";
    private const string PreviousPowerShellProfile = "$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n\n" + LegacyPowerShellProfile;
    private const string LegacyCommandPromptProfile = "for %%f in (\"%USERPROFILE%\\.EnvManager\\cmd\\*\") do (\n    if exist \"%%f\" call \"%%f\"\n)\n";
    private const string CommandPromptProfile = LegacyCommandPromptProfile;
    private const string PreviousCommandPromptProfile = "set PATH=%USERPROFILE%\\.local\\bin;%PATH%\n\n" + LegacyCommandPromptProfile;

    private readonly DirectoryInfo homeDirectory;
    private readonly bool isWindows;

    /// <summary>Initializes an installer for the supplied home directory and platform.</summary>
    /// <param name="homeDirectory">The user home directory to modify.</param>
    /// <param name="isWindows">Whether the profiles are being installed on Windows.</param>
    public ShellProfileInstaller(DirectoryInfo homeDirectory, bool isWindows)
    {
        this.homeDirectory = homeDirectory;
        this.isWindows = isWindows;
    }

    /// <summary>Normalizes a shell name and verifies that it is supported on this platform.</summary>
    /// <param name="shellName">The shell name supplied by the user.</param>
    /// <exception cref="ConfigurationException">The name or platform is unsupported.</exception>
    public string NormalizeShellName(string shellName)
    {
        string normalizedName = shellName.Trim().ToLowerInvariant();

        if (normalizedName is not ("bash" or "zsh" or "fish" or "powershell" or "cmd"))
        {
            throw new ConfigurationException($"Unsupported shell '{shellName}'. Supported shells: bash, zsh, fish, powershell, cmd.");
        }

        if (this.isWindows && normalizedName is "bash" or "zsh" or "fish")
        {
            throw new ConfigurationException($"Shell '{normalizedName}' is not supported on Windows.");
        }

        if (!this.isWindows && normalizedName is "cmd")
        {
            throw new ConfigurationException("Shell 'cmd' is only supported on Windows.");
        }

        return normalizedName;
    }

    /// <summary>Preserves existing shell profiles as addon files and installs shell profile loaders.</summary>
    /// <param name="shellName">A supported shell name.</param>
    /// <returns>The home-relative paths of installed shell profiles and addon files.</returns>
    public IReadOnlyList<string> Install(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfileDefinition shellProfile = this.GetShellProfileDefinition(normalizedName);
        List<(FileInfo ShellProfileFile, bool Existed, byte[] OriginalContent)> shellProfileFiles = [];
        List<(FileInfo File, string TemporaryFile)> stagedFiles = [];
        List<string> addonContents = [];
        List<byte[]> addonContentBytes = [];
        FileInfo migratedAddonFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"00_Profile{shellProfile.AddonExtension}"));
        FileInfo pathAddonFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"01_EnvManager{shellProfile.AddonExtension}"));
        bool hasProfileAlias = this.HasEnvManagerAlias(normalizedName, shellProfile, includePathAddon: false);
        string pathAddonContent = GetPathAddonContent(normalizedName, includeAlias: !hasProfileAlias);

        foreach (string relativePath in shellProfile.ShellProfilePaths)
        {
            FileInfo shellProfileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            bool existed = shellProfileFile.Exists;
            byte[] originalContent = existed ? File.ReadAllBytes(shellProfileFile.FullName) : [];
            string originalText = existed ? File.ReadAllText(shellProfileFile.FullName) : string.Empty;

            if (existed && !IsInstalledShellProfile(normalizedName, shellProfile, originalText))
            {
                FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);
                if (backupFile.Exists)
                {
                    throw new ConfigurationException($"Shell profile '{shellProfileFile.FullName}' was modified after its original backup was created. Preserve or restore it before reinstalling shell integration.");
                }

                addonContents.Add(originalText);
                addonContentBytes.Add(originalContent);
            }

            shellProfileFiles.Add((shellProfileFile, existed, originalContent));
        }

        if (addonContents.Count > 0 && migratedAddonFile.Exists)
        {
            throw new ConfigurationException($"Cannot preserve the existing shell profile because addon file '{migratedAddonFile.FullName}' already exists.");
        }

        if (pathAddonFile.Exists
            && !string.Equals(File.ReadAllText(pathAddonFile.FullName), pathAddonContent, StringComparison.Ordinal))
        {
            string pathOnlyContent = GetPathAddonContent(normalizedName, includeAlias: false);
            string aliasContent = GetPathAddonContent(normalizedName, includeAlias: true);
            string existingPathAddonContent = File.ReadAllText(pathAddonFile.FullName);
            if (!string.Equals(existingPathAddonContent, pathOnlyContent, StringComparison.Ordinal)
                && !string.Equals(existingPathAddonContent, aliasContent, StringComparison.Ordinal))
            {
                throw new ConfigurationException($"EnvManager addon file '{pathAddonFile.FullName}' was modified. Preserve or restore it before reinstalling shell integration.");
            }
        }

        string? addonTemporaryFile = null;
        bool migratedAddonCreated = false;

        try
        {
            foreach ((FileInfo shellProfileFile, _, _) in shellProfileFiles)
            {
                shellProfileFile.Directory?.Create();
                string profileDirectory = shellProfileFile.DirectoryName
                    ?? throw new IOException($"Could not determine the shell profile directory for '{shellProfileFile.FullName}'.");
                string temporaryFile = Path.Combine(profileDirectory, $".{shellProfileFile.Name}.{Guid.NewGuid():N}.tmp");
                stagedFiles.Add((shellProfileFile, temporaryFile));
                File.WriteAllText(temporaryFile, shellProfile.ShellProfileLoaderContent);
            }

            Directory.CreateDirectory(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName));
            if (!pathAddonFile.Exists
                || !string.Equals(File.ReadAllText(pathAddonFile.FullName), pathAddonContent, StringComparison.Ordinal))
            {
                string envManagerDirectory = pathAddonFile.DirectoryName
                    ?? throw new IOException($"Could not determine the addon directory for '{pathAddonFile.FullName}'.");
                Directory.CreateDirectory(envManagerDirectory);
                string temporaryFile = Path.Combine(envManagerDirectory, $".{pathAddonFile.Name}.{Guid.NewGuid():N}.tmp");
                shellProfileFiles.Add((pathAddonFile, pathAddonFile.Exists, pathAddonFile.Exists ? File.ReadAllBytes(pathAddonFile.FullName) : []));
                stagedFiles.Add((pathAddonFile, temporaryFile));
                File.WriteAllText(temporaryFile, pathAddonContent);
            }

            if (addonContents.Count > 0)
            {
                migratedAddonFile.Directory?.Create();
                string addonDirectory = migratedAddonFile.DirectoryName
                    ?? throw new IOException($"Could not determine the addon directory for '{migratedAddonFile.FullName}'.");
                addonTemporaryFile = Path.Combine(addonDirectory, $".{migratedAddonFile.Name}.{Guid.NewGuid():N}.tmp");
                if (addonContentBytes.Count == 1)
                {
                    File.WriteAllBytes(addonTemporaryFile, addonContentBytes[0]);
                }
                else
                {
                    File.WriteAllText(addonTemporaryFile, string.Join(Environment.NewLine, addonContents));
                }

                File.Move(addonTemporaryFile, migratedAddonFile.FullName);
                migratedAddonCreated = true;
            }

            foreach ((FileInfo file, string temporaryFile) in stagedFiles)
            {
                File.Move(temporaryFile, file.FullName, overwrite: true);
            }

            List<string> installedPaths = [.. shellProfile.ShellProfilePaths];
            installedPaths.Add(Path.Combine(".EnvManager", normalizedName, pathAddonFile.Name)
                .Replace(Path.DirectorySeparatorChar, '/'));
            if (addonContents.Count > 0 || File.Exists(migratedAddonFile.FullName))
            {
                installedPaths.Add(Path.Combine(".EnvManager", normalizedName, migratedAddonFile.Name)
                    .Replace(Path.DirectorySeparatorChar, '/'));
            }

            return installedPaths;
        }
        catch (Exception exception)
        {
            List<Exception> rollbackFailures = [];
            foreach ((FileInfo shellProfileFile, bool existed, byte[] originalContent) in shellProfileFiles)
            {
                try
                {
                    if (existed)
                    {
                        File.WriteAllBytes(shellProfileFile.FullName, originalContent);
                    }
                    else if (shellProfileFile.Exists)
                    {
                        shellProfileFile.Delete();
                    }
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(rollbackException);
                }
            }

            if (migratedAddonCreated)
            {
                try
                {
                    migratedAddonFile.Delete();
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(rollbackException);
                }
            }

            if (rollbackFailures.Count > 0)
            {
                rollbackFailures.Insert(0, exception);
                throw new IOException("Shell profile and addon installation failed and rollback was incomplete.", new AggregateException(rollbackFailures));
            }

            throw;
        }
        finally
        {
            foreach ((_, string temporaryFile) in stagedFiles)
            {
                File.Delete(temporaryFile);
            }

            if (addonTemporaryFile is not null)
            {
                File.Delete(addonTemporaryFile);
            }
        }
    }

    /// <summary>Restores shell profile backups and removes addon files created by EnvManager.</summary>
    /// <param name="shellName">A supported shell name.</param>
    public void Uninstall(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfileDefinition shellProfile = this.GetShellProfileDefinition(normalizedName);
        List<(FileInfo ShellProfileFile, FileInfo BackupFile)> shellProfileFiles = [];
        FileInfo pathAddonFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"01_EnvManager{shellProfile.AddonExtension}"));
        if (pathAddonFile.Exists
            && !IsExpectedPathAddonContent(normalizedName, File.ReadAllText(pathAddonFile.FullName)))
        {
            throw new ConfigurationException($"EnvManager addon file '{pathAddonFile.FullName}' has changed since EnvManager installed it. It was left untouched.");
        }

        foreach (string relativePath in shellProfile.ShellProfilePaths)
        {
            FileInfo shellProfileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);

            if (shellProfileFile.Exists && !IsInstalledShellProfile(normalizedName, shellProfile, File.ReadAllText(shellProfileFile.FullName)))
            {
                throw new ConfigurationException($"Shell profile '{shellProfileFile.FullName}' has changed since EnvManager installed its loader. It was left untouched.");
            }

            shellProfileFiles.Add((shellProfileFile, backupFile));
        }

        if (pathAddonFile.Exists)
        {
            pathAddonFile.Delete();
        }

        foreach ((FileInfo shellProfileFile, FileInfo backupFile) in shellProfileFiles)
        {
            if (backupFile.Exists)
            {
                shellProfileFile.Directory?.Create();
                File.Copy(backupFile.FullName, shellProfileFile.FullName, overwrite: true);
                backupFile.Delete();
            }
            else if (shellProfileFile.Exists)
            {
                shellProfileFile.Delete();
            }
        }
    }

    private static bool IsInstalledShellProfile(string shellName, ShellProfileDefinition shellProfile, string content)
    {
        string legacyContent = shellName switch
        {
            "bash" => LegacyBashProfile,
            "zsh" => LegacyZshProfile,
            "fish" => LegacyFishProfile,
            "powershell" => LegacyPowerShellProfile,
            "cmd" => LegacyCommandPromptProfile,
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };
        string previousContent = shellName switch
        {
            "bash" => PreviousBashProfile,
            "zsh" => PreviousZshProfile,
            "fish" => PreviousFishProfile,
            "powershell" => PreviousPowerShellProfile,
            "cmd" => PreviousCommandPromptProfile,
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        return string.Equals(content, shellProfile.ShellProfileLoaderContent, StringComparison.Ordinal)
            || string.Equals(content, legacyContent, StringComparison.Ordinal)
            || string.Equals(content, previousContent, StringComparison.Ordinal);
    }

    private static string GetPathAddonContent(string shellName)
        => GetPathAddonContent(shellName, includeAlias: true);

    private static string GetPathAddonContent(string shellName, bool includeAlias)
        => shellName switch
        {
            "bash" or "zsh" => $"export PATH=\"$HOME/.local/bin:$PATH\"\n{(includeAlias ? "alias em='EnvManager'\n" : string.Empty)}",
            "fish" => $"set -gx PATH \"$HOME/.local/bin\" $PATH\n{(includeAlias ? "alias em EnvManager\n" : string.Empty)}",
            "powershell" => $"$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n{(includeAlias ? "Set-Alias -Name em -Value EnvManager\n" : string.Empty)}",
            "cmd" => $"set PATH=%USERPROFILE%\\.local\\bin;%PATH%\n{(includeAlias ? "doskey em=EnvManager $*\n" : string.Empty)}",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

    /// <summary>Adds the EnvManager alias to a configured shell when it is missing.</summary>
    /// <param name="shellName">A supported shell name.</param>
    /// <returns>The home-relative addon path when the addon changed; otherwise, <see langword="null"/>.</returns>
    public string? EnsureEnvManagerAlias(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfileDefinition shellProfile = this.GetShellProfileDefinition(normalizedName);

        if (this.HasEnvManagerAlias(normalizedName, shellProfile, includePathAddon: true))
        {
            return null;
        }

        FileInfo pathAddonFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"01_EnvManager{shellProfile.AddonExtension}"));
        string expectedPathOnlyContent = GetPathAddonContent(normalizedName, includeAlias: false);
        string aliasContent = GetAliasContent(normalizedName);
        string migratedContent;

        if (!pathAddonFile.Exists)
        {
            migratedContent = GetPathAddonContent(normalizedName, includeAlias: true);
        }
        else
        {
            string existingContent = File.ReadAllText(pathAddonFile.FullName);
            if (!string.Equals(existingContent, expectedPathOnlyContent, StringComparison.Ordinal))
            {
                throw new ConfigurationException($"EnvManager addon file '{pathAddonFile.FullName}' was modified. Preserve or restore it before migrating the shell alias.");
            }

            migratedContent = existingContent + aliasContent;
        }

        string addonDirectory = pathAddonFile.DirectoryName
            ?? throw new IOException($"Could not determine the addon directory for '{pathAddonFile.FullName}'.");
        Directory.CreateDirectory(addonDirectory);
        string temporaryFile = Path.Combine(addonDirectory, $".{pathAddonFile.Name}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryFile, migratedContent);
            File.Move(temporaryFile, pathAddonFile.FullName, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryFile);
        }

        return Path.Combine(".EnvManager", normalizedName, pathAddonFile.Name)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private bool HasEnvManagerAlias(string shellName, ShellProfileDefinition shellProfile, bool includePathAddon)
    {
        foreach (string relativePath in shellProfile.ShellProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            if (profileFile.Exists && HasEnvManagerAlias(shellName, File.ReadAllText(profileFile.FullName)))
            {
                return true;
            }
        }

        DirectoryInfo shellDirectory = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", shellName));
        if (!shellDirectory.Exists)
        {
            return false;
        }

        foreach (string filePath in Directory.EnumerateFiles(shellDirectory.FullName, $"*{shellProfile.AddonExtension}"))
        {
            FileInfo addonFile = new(filePath);
            if (!includePathAddon && string.Equals(addonFile.Name, $"01_EnvManager{shellProfile.AddonExtension}", StringComparison.Ordinal))
            {
                continue;
            }

            if (HasEnvManagerAlias(shellName, File.ReadAllText(addonFile.FullName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEnvManagerAlias(string shellName, string content)
    {
        string pattern = shellName switch
        {
            "bash" or "zsh" => @"^\s*alias\s+em\s*=",
            "fish" => @"^\s*alias\s+em(?:\s|$)",
            "powershell" => @"^\s*(?:Set-Alias|New-Alias)\b(?:\s+-Name\s+em\b|\s+em\b)",
            "cmd" => @"^\s*doskey\s+em\s*=",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };
        RegexOptions options = RegexOptions.Multiline | (shellName is "powershell" or "cmd" ? RegexOptions.IgnoreCase : RegexOptions.None);

        return Regex.IsMatch(content, pattern, options);
    }

    private static bool IsExpectedPathAddonContent(string shellName, string content)
        => string.Equals(content, GetPathAddonContent(shellName, includeAlias: false), StringComparison.Ordinal)
            || string.Equals(content, GetPathAddonContent(shellName, includeAlias: true), StringComparison.Ordinal);

    private static string GetAliasContent(string shellName)
        => shellName switch
        {
            "bash" or "zsh" => "alias em='EnvManager'\n",
            "fish" => "alias em EnvManager\n",
            "powershell" => "Set-Alias -Name em -Value EnvManager\n",
            "cmd" => "doskey em=EnvManager $*\n",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

    private ShellProfileDefinition GetShellProfileDefinition(string shellName)
    {
        string[] shellProfilePaths = shellName switch
        {
            "bash" => [".bashrc", ".bash_profile"],
            "zsh" => [".zshrc", ".zprofile"],
            "fish" => [Path.Combine(".config", "fish", "config.fish")],
            "powershell" when this.isWindows => [Path.Combine("Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1")],
            "powershell" => [Path.Combine(".config", "powershell", "Microsoft.PowerShell_profile.ps1")],
            "cmd" => ["_cmdrc"],
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        string shellProfileLoaderContent = shellName switch
        {
            "bash" => BashProfile,
            "zsh" => ZshProfile,
            "fish" => FishProfile,
            "powershell" => PowerShellProfile,
            "cmd" => CommandPromptProfile,
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        string addonExtension = shellName switch
        {
            "bash" or "zsh" => ".sh",
            "fish" => ".fish",
            "powershell" => ".ps1",
            "cmd" => ".cmd",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        return new ShellProfileDefinition(shellProfilePaths, shellProfileLoaderContent, addonExtension);
    }

    private FileInfo GetBackupFile(string shellName, string relativePath)
    {
        return new FileInfo(Path.Combine(
            this.homeDirectory.FullName,
            ".EnvManager",
            "backups",
            "shell-profiles",
            shellName,
            relativePath));
    }

    private sealed record ShellProfileDefinition(string[] ShellProfilePaths, string ShellProfileLoaderContent, string AddonExtension);
}
