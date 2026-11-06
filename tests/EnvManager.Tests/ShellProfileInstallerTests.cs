// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellProfileInstallerTests.cs                                           │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-10-21                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Shells;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ShellProfileInstallerTests
{
    [Test]
    public async Task InstallBashMigratesExistingProfilesIntoNumberedScript()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string bashProfile = Path.Combine(home.FullName, ".bash_profile");
            File.WriteAllText(bashrc, "original bashrc");
            File.WriteAllText(bashProfile, "original bash profile");

            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("BASH");

            string installedContent = File.ReadAllText(bashrc);
            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh");

            string expectedContent = "export PATH=\"$HOME/.local/bin:$PATH\"\n\nfor file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";

            await Assert.That(installedContent).IsEqualTo(expectedContent);
            await Assert.That(File.ReadAllText(bashProfile)).IsEqualTo(expectedContent);
            await Assert.That(File.ReadAllText(migratedProfile)).Contains("original bashrc");
            await Assert.That(File.ReadAllText(migratedProfile)).Contains("original bash profile");
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc"))).IsFalse();
            await Assert.That(File.Exists(bashrc)).IsTrue();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallBashRollsBackEarlierProfileWritesWhenAProfileCannotBeReplaced()
    {
        DirectoryInfo home = CreateHome();
        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            File.WriteAllText(bashrc, "original bashrc");
            Directory.CreateDirectory(Path.Combine(home.FullName, ".bash_profile"));

            ShellProfileInstaller installer = new(home, isWindows: false);
            await Assert.That(() => installer.Install("bash")).Throws<IOException>();

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo("original bashrc");
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(home.FullName, ".bash_profile"))).IsTrue();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallBashUpgradesLegacyLoaderAndPreservesMigratedProfile()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh");
            File.WriteAllText(bashrc, "original profile");
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            File.WriteAllText(bashrc, "for file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n");

            installer.Install("bash");

            await Assert.That(File.ReadAllText(bashrc)).StartsWith("export PATH=\"$HOME/.local/bin:$PATH\"\n\n");
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");
            installer.Uninstall("bash");
            await Assert.That(File.Exists(bashrc)).IsFalse();
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task UninstallBashRestoresLegacyBackupFromLegacyLoader()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string backup = Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.WriteAllText(backup, "original profile");
            File.WriteAllText(bashrc, "for file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n");

            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Uninstall("bash");

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo("original profile");
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".bash_profile"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallBashIsIdempotentAndPreservesMigratedProfile()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh");
            File.WriteAllText(bashrc, "original profile");

            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            string installedContent = File.ReadAllText(bashrc);
            installer.Install("bash");

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo(installedContent);
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");
            await Assert.That(Directory.Exists(Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallProfileModifiedAfterInstallThrowsAndPreservesUserContent()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            File.WriteAllText(bashrc, "original profile");
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            File.WriteAllText(bashrc, "user edits");

            await Assert.That(() => installer.Install("bash")).Throws<ConfigurationException>();
            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo("user edits");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task UninstallBashRemovesLoadersAndKeepsMigratedProfile()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string original = "original bashrc";
            File.WriteAllText(bashrc, original);

            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            installer.Uninstall("bash");

            await Assert.That(File.Exists(bashrc)).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh"))).IsEqualTo(original);
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".bash_profile"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task UninstallBashRemovesProfilesCreatedByInstaller()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            installer.Uninstall("bash");

            await Assert.That(File.Exists(Path.Combine(home.FullName, ".bashrc"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".bash_profile"))).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task UninstallProfileChangedAfterInstallLeavesItUntouched()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            File.WriteAllText(bashrc, "user edits");

            await Assert.That(() => installer.Uninstall("bash")).Throws<ConfigurationException>();
            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo("user edits");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task NormalizeShellNameCmdOnUnixThrowsConfigurationException()
    {
        ShellProfileInstaller installer = new(new DirectoryInfo(Path.GetTempPath()), isWindows: false);

        await Assert.That(() => installer.NormalizeShellName("cmd")).Throws<ConfigurationException>();
    }

    [Test]
    public async Task NormalizeShellNameBashOnWindowsThrowsConfigurationException()
    {
        ShellProfileInstaller installer = new(new DirectoryInfo(Path.GetTempPath()), isWindows: true);

        await Assert.That(() => installer.NormalizeShellName("bash")).Throws<ConfigurationException>();
    }

    [Test]
    public async Task NormalizeShellNameUnknownShellThrowsConfigurationException()
    {
        ShellProfileInstaller installer = new(new DirectoryInfo(Path.GetTempPath()), isWindows: false);

        await Assert.That(() => installer.NormalizeShellName("unknown")).Throws<ConfigurationException>();
    }

    [Test]
    public async Task InstallZshWritesSpecifiedLoader()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("zsh");

            string content = File.ReadAllText(Path.Combine(home.FullName, ".zshrc"));

            string expectedContent = "export PATH=\"$HOME/.local/bin:$PATH\"\n\nfor file in \"$HOME/.EnvManager/zsh/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";

            await Assert.That(content).IsEqualTo(expectedContent);
            await Assert.That(File.ReadAllText(Path.Combine(home.FullName, ".zprofile"))).IsEqualTo(expectedContent);
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallFishWritesFishLoaderToConfigProfile()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("fish");

            string profile = Path.Combine(home.FullName, ".config", "fish", "config.fish");
            string content = File.ReadAllText(profile);

            string expectedContent = "set -gx PATH \"$HOME/.local/bin\" $PATH\n\nif test -d $HOME/.EnvManager/fish\n    for file in $HOME/.EnvManager/fish/*.fish\n        source \"$file\"\n    end\nend\n";

            await Assert.That(content).IsEqualTo(expectedContent);
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallUnixPowerShellUsesConfigProfilePath()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("powershell");

            string profile = Path.Combine(home.FullName, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");

            string expectedContent = "$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n\n$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";

            await Assert.That(File.ReadAllText(profile)).IsEqualTo(expectedContent);
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallWindowsPowerShellUsesDocumentsProfilePath()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: true);
            installer.Install("powershell");

            string profile = Path.Combine(home.FullName, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1");

            string expectedContent = "$env:PATH = \"$HOME/.local/bin$([IO.Path]::PathSeparator)$env:PATH\"\n\n$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";

            await Assert.That(File.ReadAllText(profile)).IsEqualTo(expectedContent);
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallCmdWritesSpecifiedLoader()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            ShellProfileInstaller installer = new(home, isWindows: true);
            installer.Install("cmd");

            string content = File.ReadAllText(Path.Combine(home.FullName, "_cmdrc"));

            string expectedContent = "set PATH=%USERPROFILE%\\.local\\bin;%PATH%\n\nfor %%f in (\"%USERPROFILE%\\.EnvManager\\cmd\\*\") do (\n    if exist \"%%f\" call \"%%f\"\n)\n";

            await Assert.That(content).IsEqualTo(expectedContent);
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    [Arguments("bash", false, ".bashrc", "00_Profile.sh")]
    [Arguments("zsh", false, ".zshrc", "00_Profile.sh")]
    [Arguments("fish", false, ".config/fish/config.fish", "00_Profile.fish")]
    [Arguments("powershell", false, ".config/powershell/Microsoft.PowerShell_profile.ps1", "00_Profile.ps1")]
    [Arguments("cmd", true, "_cmdrc", "00_Profile.cmd")]
    public async Task InstallMovesOriginalProfileToShellSpecificNumberedFile(string shell, bool isWindows, string profilePath, string migratedFileName)
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string originalProfile = Path.Combine(home.FullName, profilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(originalProfile)!);
            File.WriteAllText(originalProfile, "original profile");

            ShellProfileInstaller installer = new(home, isWindows);
            installer.Install(shell);

            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", shell, migratedFileName);
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");
            await Assert.That(File.Exists(Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", shell, profilePath))).IsFalse();
            await Assert.That(File.ReadAllText(originalProfile)).Contains(".EnvManager");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallDoesNotOverwriteExistingNumberedProfileDuringMigration()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", "bash", "00_Profile.sh");
            File.WriteAllText(bashrc, "original profile");
            Directory.CreateDirectory(Path.GetDirectoryName(migratedProfile)!);
            File.WriteAllText(migratedProfile, "existing script");

            ShellProfileInstaller installer = new(home, isWindows: false);
            await Assert.That(() => installer.Install("bash")).Throws<ConfigurationException>();

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo("original profile");
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("existing script");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    [Arguments("zsh", false, ".zshrc", "for file in \"$HOME/.EnvManager/zsh/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n")]
    [Arguments("fish", false, ".config/fish/config.fish", "if test -d $HOME/.EnvManager/fish\n    for file in $HOME/.EnvManager/fish/*.fish\n        source \"$file\"\n    end\nend\n")]
    [Arguments("powershell", false, ".config/powershell/Microsoft.PowerShell_profile.ps1", "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n")]
    [Arguments("powershell", true, "Documents/PowerShell/Microsoft.PowerShell_profile.ps1", "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n")]
    [Arguments("cmd", true, "_cmdrc", "for %%f in (\"%USERPROFILE%\\.EnvManager\\cmd\\*\") do (\n    if exist \"%%f\" call \"%%f\"\n)\n")]
    public async Task LegacyLoadersUpgradeAndUninstallWithoutLosingMigratedProfiles(string shell, bool isWindows, string relativePath, string legacyContent)
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string profile = Path.Combine(home.FullName, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
            File.WriteAllText(profile, "original profile");
            ShellProfileInstaller installer = new(home, isWindows);
            installer.Install(shell);
            string updatedContent = File.ReadAllText(profile);
            string migratedProfile = Path.Combine(home.FullName, ".EnvManager", shell, shell switch
            {
                "fish" => "00_Profile.fish",
                "powershell" => "00_Profile.ps1",
                "cmd" => "00_Profile.cmd",
                _ => "00_Profile.sh",
            });

            File.WriteAllText(profile, legacyContent);
            installer.Install(shell);
            await Assert.That(File.ReadAllText(profile)).IsEqualTo(updatedContent);
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");
            installer.Uninstall(shell);
            await Assert.That(File.Exists(profile)).IsFalse();
            await Assert.That(File.ReadAllText(migratedProfile)).IsEqualTo("original profile");

            installer.Install(shell);
            File.WriteAllText(profile, legacyContent);
            installer.Uninstall(shell);
            await Assert.That(File.Exists(profile)).IsFalse();
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    private static DirectoryInfo CreateHome()
    {
        DirectoryInfo home = new(Path.Combine(Path.GetTempPath(), $"EnvManager-ShellProfileTests-{Guid.NewGuid():N}"));
        home.Create();

        return home;
    }
}
