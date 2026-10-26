// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellProfileInstaller.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;

using EnvManager.Exceptions;

namespace EnvManager.Shells;

/// <summary>
/// Installs and removes the EnvManager loader in supported shell profile files,
/// preserving the original file contents in the user's EnvManager directory.
/// </summary>
internal sealed class ShellProfileInstaller
{
    private const string BashProfile = "for file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";
    private const string ZshProfile = "for file in \"$HOME/.EnvManager/zsh/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";
    private const string FishProfile = "if test -d $HOME/.EnvManager/fish\n    for file in $HOME/.EnvManager/fish/*.fish\n        source \"$file\"\n    end\nend\n";
    private const string PowerShellProfile = "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";
    private const string CommandPromptProfile = "for %%f in (\"%USERPROFILE%\\.EnvManager\\cmd\\*\") do (\n    if exist \"%%f\" call \"%%f\"\n)\n";

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

    /// <summary>Backs up and replaces the profile files for the requested shell.</summary>
    /// <param name="shellName">A supported shell name.</param>
    /// <returns>The home-relative paths of the installed profile files.</returns>
    public IReadOnlyList<string> Install(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfile profile = this.GetProfile(normalizedName);
        foreach (string relativePath in profile.ProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));

            if (!profileFile.Exists || string.Equals(File.ReadAllText(profileFile.FullName), profile.Content, StringComparison.Ordinal))
            {
                continue;
            }

            FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);

            if (backupFile.Exists)
            {
                throw new ConfigurationException($"Shell profile '{profileFile.FullName}' was modified after its original backup was created. Preserve or restore it before reinstalling shell integration.");
            }

            backupFile.Directory?.Create();
            File.Copy(profileFile.FullName, backupFile.FullName);
        }

        Directory.CreateDirectory(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName));

        foreach (string relativePath in profile.ProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            profileFile.Directory?.Create();
            File.WriteAllText(profileFile.FullName, profile.Content);
        }

        return profile.ProfilePaths;
    }

    /// <summary>Restores backed-up profile files or removes loader files created by EnvManager.</summary>
    /// <param name="shellName">A supported shell name.</param>
    public void Uninstall(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfile profile = this.GetProfile(normalizedName);
        List<(FileInfo ProfileFile, FileInfo BackupFile)> profiles = [];

        foreach (string relativePath in profile.ProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);

            if (profileFile.Exists && !string.Equals(File.ReadAllText(profileFile.FullName), profile.Content, StringComparison.Ordinal))
            {
                throw new ConfigurationException($"Shell profile '{profileFile.FullName}' has changed since EnvManager installed its loader. It was left untouched.");
            }

            profiles.Add((profileFile, backupFile));
        }

        foreach ((FileInfo profileFile, FileInfo backupFile) in profiles)
        {
            if (backupFile.Exists)
            {
                profileFile.Directory?.Create();
                File.Copy(backupFile.FullName, profileFile.FullName, overwrite: true);
                backupFile.Delete();
            }
            else if (profileFile.Exists)
            {
                profileFile.Delete();
            }
        }
    }

    private ShellProfile GetProfile(string shellName)
    {
        string[] profilePaths = shellName switch
        {
            "bash" => [".bashrc", ".bash_profile"],
            "zsh" => [".zshrc", ".zprofile"],
            "fish" => [Path.Combine(".config", "fish", "config.fish")],
            "powershell" when this.isWindows => [Path.Combine("Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1")],
            "powershell" => [Path.Combine(".config", "powershell", "Microsoft.PowerShell_profile.ps1")],
            "cmd" => ["_cmdrc"],
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        string content = shellName switch
        {
            "bash" => BashProfile,
            "zsh" => ZshProfile,
            "fish" => FishProfile,
            "powershell" => PowerShellProfile,
            "cmd" => CommandPromptProfile,
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        return new ShellProfile(profilePaths, content);
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

    private sealed record ShellProfile(string[] ProfilePaths, string Content);
}
