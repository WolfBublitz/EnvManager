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
/// migrating original profile contents into the user's EnvManager shell directory.
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

    /// <summary>Migrates existing profile contents and installs loader files for the requested shell.</summary>
    /// <param name="shellName">A supported shell name.</param>
    /// <returns>The home-relative paths of installed loader files and any migrated profile.</returns>
    public IReadOnlyList<string> Install(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfile profile = this.GetProfile(normalizedName);
        List<(FileInfo ProfileFile, bool Existed, byte[] OriginalContent)> profiles = [];
        List<(FileInfo ProfileFile, string TemporaryFile)> stagedFiles = [];
        List<string> migrationContents = [];
        List<byte[]> migrationContentBytes = [];
        FileInfo migratedProfileFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"00_Profile{profile.MigrationExtension}"));
        FileInfo envManagerProfileFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"01_EnvManager{profile.MigrationExtension}"));
        string envManagerProfileContent = GetEnvManagerProfileContent(normalizedName);

        foreach (string relativePath in profile.ProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            bool existed = profileFile.Exists;
            byte[] originalContent = existed ? File.ReadAllBytes(profileFile.FullName) : [];
            string originalText = existed ? File.ReadAllText(profileFile.FullName) : string.Empty;

            if (existed && !IsInstalledProfile(normalizedName, profile, originalText))
            {
                FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);
                if (backupFile.Exists)
                {
                    throw new ConfigurationException($"Shell profile '{profileFile.FullName}' was modified after its original backup was created. Preserve or restore it before reinstalling shell integration.");
                }

                migrationContents.Add(originalText);
                migrationContentBytes.Add(originalContent);
            }

            profiles.Add((profileFile, existed, originalContent));
        }

        if (migrationContents.Count > 0 && migratedProfileFile.Exists)
        {
            throw new ConfigurationException($"Cannot migrate the existing shell profile because '{migratedProfileFile.FullName}' already exists.");
        }

        if (envManagerProfileFile.Exists
            && !string.Equals(File.ReadAllText(envManagerProfileFile.FullName), envManagerProfileContent, StringComparison.Ordinal))
        {
            throw new ConfigurationException($"EnvManager profile '{envManagerProfileFile.FullName}' was modified. Preserve or restore it before reinstalling shell integration.");
        }

        string? migrationTemporaryFile = null;
        bool migratedProfileCreated = false;

        try
        {
            foreach ((FileInfo profileFile, _, _) in profiles)
            {
                profileFile.Directory?.Create();
                string profileDirectory = profileFile.DirectoryName
                    ?? throw new IOException($"Could not determine the profile directory for '{profileFile.FullName}'.");
                string temporaryFile = Path.Combine(profileDirectory, $".{profileFile.Name}.{Guid.NewGuid():N}.tmp");
                stagedFiles.Add((profileFile, temporaryFile));
                File.WriteAllText(temporaryFile, profile.Content);
            }

            Directory.CreateDirectory(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName));
            if (!envManagerProfileFile.Exists)
            {
                string envManagerDirectory = envManagerProfileFile.DirectoryName
                    ?? throw new IOException($"Could not determine the profile directory for '{envManagerProfileFile.FullName}'.");
                Directory.CreateDirectory(envManagerDirectory);
                string temporaryFile = Path.Combine(envManagerDirectory, $".{envManagerProfileFile.Name}.{Guid.NewGuid():N}.tmp");
                profiles.Add((envManagerProfileFile, false, []));
                stagedFiles.Add((envManagerProfileFile, temporaryFile));
                File.WriteAllText(temporaryFile, envManagerProfileContent);
            }

            if (migrationContents.Count > 0)
            {
                migratedProfileFile.Directory?.Create();
                string migrationDirectory = migratedProfileFile.DirectoryName
                    ?? throw new IOException($"Could not determine the profile directory for '{migratedProfileFile.FullName}'.");
                migrationTemporaryFile = Path.Combine(migrationDirectory, $".{migratedProfileFile.Name}.{Guid.NewGuid():N}.tmp");
                if (migrationContentBytes.Count == 1)
                {
                    File.WriteAllBytes(migrationTemporaryFile, migrationContentBytes[0]);
                }
                else
                {
                    File.WriteAllText(migrationTemporaryFile, string.Join(Environment.NewLine, migrationContents));
                }

                File.Move(migrationTemporaryFile, migratedProfileFile.FullName);
                migratedProfileCreated = true;
            }

            foreach ((FileInfo profileFile, string temporaryFile) in stagedFiles)
            {
                File.Move(temporaryFile, profileFile.FullName, overwrite: true);
            }

            List<string> installedPaths = [.. profile.ProfilePaths];
            installedPaths.Add(Path.Combine(".EnvManager", normalizedName, envManagerProfileFile.Name)
                .Replace(Path.DirectorySeparatorChar, '/'));
            if (migrationContents.Count > 0 || File.Exists(migratedProfileFile.FullName))
            {
                installedPaths.Add(Path.Combine(".EnvManager", normalizedName, migratedProfileFile.Name)
                    .Replace(Path.DirectorySeparatorChar, '/'));
            }

            return installedPaths;
        }
        catch (Exception exception)
        {
            List<Exception> rollbackFailures = [];
            foreach ((FileInfo profileFile, bool existed, byte[] originalContent) in profiles)
            {
                try
                {
                    if (existed)
                    {
                        File.WriteAllBytes(profileFile.FullName, originalContent);
                    }
                    else if (profileFile.Exists)
                    {
                        profileFile.Delete();
                    }
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(rollbackException);
                }
            }

            if (migratedProfileCreated)
            {
                try
                {
                    migratedProfileFile.Delete();
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(rollbackException);
                }
            }

            if (rollbackFailures.Count > 0)
            {
                rollbackFailures.Insert(0, exception);
                throw new IOException("Shell profile installation failed and rollback was incomplete.", new AggregateException(rollbackFailures));
            }

            throw;
        }
        finally
        {
            foreach ((_, string temporaryFile) in stagedFiles)
            {
                File.Delete(temporaryFile);
            }

            if (migrationTemporaryFile is not null)
            {
                File.Delete(migrationTemporaryFile);
            }
        }
    }

    /// <summary>Restores backed-up profile files or removes loader files created by EnvManager.</summary>
    /// <param name="shellName">A supported shell name.</param>
    public void Uninstall(string shellName)
    {
        string normalizedName = this.NormalizeShellName(shellName);
        ShellProfile profile = this.GetProfile(normalizedName);
        List<(FileInfo ProfileFile, FileInfo BackupFile)> profiles = [];
        FileInfo envManagerProfileFile = new(Path.Combine(this.homeDirectory.FullName, ".EnvManager", normalizedName, $"01_EnvManager{profile.MigrationExtension}"));
        if (envManagerProfileFile.Exists
            && !string.Equals(File.ReadAllText(envManagerProfileFile.FullName), GetEnvManagerProfileContent(normalizedName), StringComparison.Ordinal))
        {
            throw new ConfigurationException($"EnvManager profile '{envManagerProfileFile.FullName}' has changed since EnvManager installed it. It was left untouched.");
        }

        foreach (string relativePath in profile.ProfilePaths)
        {
            FileInfo profileFile = new(Path.Combine(this.homeDirectory.FullName, relativePath));
            FileInfo backupFile = this.GetBackupFile(normalizedName, relativePath);

            if (profileFile.Exists && !IsInstalledProfile(normalizedName, profile, File.ReadAllText(profileFile.FullName)))
            {
                throw new ConfigurationException($"Shell profile '{profileFile.FullName}' has changed since EnvManager installed its loader. It was left untouched.");
            }

            profiles.Add((profileFile, backupFile));
        }

        if (envManagerProfileFile.Exists)
        {
            envManagerProfileFile.Delete();
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

    private static bool IsInstalledProfile(string shellName, ShellProfile profile, string content)
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

        return string.Equals(content, profile.Content, StringComparison.Ordinal)
            || string.Equals(content, legacyContent, StringComparison.Ordinal)
            || string.Equals(content, previousContent, StringComparison.Ordinal);
    }

    private static string GetEnvManagerProfileContent(string shellName)
        => shellName switch
        {
            "bash" or "zsh" => "export PATH=\"$HOME/.local/bin:$PATH\"\n",
            "fish" => "set -gx PATH \"$HOME/.local/bin\" $PATH\n",
            "powershell" => "$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n",
            "cmd" => "set PATH=%USERPROFILE%\\.local\\bin;%PATH%\n",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

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

        string migrationExtension = shellName switch
        {
            "bash" or "zsh" => ".sh",
            "fish" => ".fish",
            "powershell" => ".ps1",
            "cmd" => ".cmd",
            _ => throw new ConfigurationException($"Unsupported shell '{shellName}'."),
        };

        return new ShellProfile(profilePaths, content, migrationExtension);
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

    private sealed record ShellProfile(string[] ProfilePaths, string Content, string MigrationExtension);
}
