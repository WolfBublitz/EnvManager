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
    public async Task InstallBashBacksUpAndReplacesExistingProfiles()
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
            string backupDirectory = Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", "bash");

            string expectedContent = "for file in \"$HOME/.EnvManager/bash/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";

            await Assert.That(installedContent).IsEqualTo(expectedContent);
            await Assert.That(File.ReadAllText(bashProfile)).IsEqualTo(expectedContent);
            await Assert.That(File.ReadAllText(Path.Combine(backupDirectory, ".bashrc"))).IsEqualTo("original bashrc");
            await Assert.That(File.ReadAllText(Path.Combine(backupDirectory, ".bash_profile"))).IsEqualTo("original bash profile");
        }
        finally
        {
            Directory.Delete(home.FullName, recursive: true);
        }
    }

    [Test]
    public async Task InstallBashIsIdempotentAndPreservesOriginalBackup()
    {
        DirectoryInfo home = CreateHome();

        try
        {
            string bashrc = Path.Combine(home.FullName, ".bashrc");
            string backup = Path.Combine(home.FullName, ".EnvManager", "backups", "shell-profiles", "bash", ".bashrc");
            File.WriteAllText(bashrc, "original profile");

            ShellProfileInstaller installer = new(home, isWindows: false);
            installer.Install("bash");
            string installedContent = File.ReadAllText(bashrc);
            installer.Install("bash");

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo(installedContent);
            await Assert.That(File.ReadAllText(backup)).IsEqualTo("original profile");
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
    public async Task UninstallBashRestoresOriginalProfiles()
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

            await Assert.That(File.ReadAllText(bashrc)).IsEqualTo(original);
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

            string expectedContent = "for file in \"$HOME/.EnvManager/zsh/\"*.sh; do\n    [ -f \"$file\" ] && source \"$file\"\ndone\n";

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

            string expectedContent = "if test -d $HOME/.EnvManager/fish\n    for file in $HOME/.EnvManager/fish/*.fish\n        source \"$file\"\n    end\nend\n";

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

            string expectedContent = "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";

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

            string expectedContent = "$path = \"$HOME/.EnvManager/powershell\"\n\nif (Test-Path $path) {\n    Get-ChildItem $path -Filter *.ps1 -File | ForEach-Object { . $_.FullName }\n}\n";

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

            string expectedContent = "for %%f in (\"%USERPROFILE%\\.EnvManager\\cmd\\*\") do (\n    if exist \"%%f\" call \"%%f\"\n)\n";

            await Assert.That(content).IsEqualTo(expectedContent);
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
