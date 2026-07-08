using System;
using System.IO;
using System.Runtime.InteropServices;

internal enum OperatingSystemType
{
    Linux,
    Windows,
    MacOS,
    Unknown
}

internal enum PackageManagerType
{
    Apt,
    Dnf,
    Pacman,
    Brew,
    Winget,
    Choco,
    Scoop,
    Unknown
}

internal record SystemInfo
{
    private readonly Lazy<OperatingSystemType> lazyOsType;

    private readonly Lazy<PackageManagerType> lazyPackageManager;

    public SystemInfo()
    {
        lazyOsType = new Lazy<OperatingSystemType>(GetOsType);
        lazyPackageManager = new Lazy<PackageManagerType>(DetectPackageManager);
    }

    public OperatingSystemType OperatingSystem => lazyOsType.Value;

    public PackageManagerType PackageManager => lazyPackageManager.Value;

    private OperatingSystemType GetOsType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OperatingSystemType.Linux;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OperatingSystemType.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OperatingSystemType.MacOS;

        return OperatingSystemType.Unknown;
    }

    private PackageManagerType DetectPackageManager()
    {
        return OperatingSystem switch
        {
            OperatingSystemType.Linux => DetectLinuxPackageManager(),
            OperatingSystemType.MacOS => DetectMacPackageManager(),
            OperatingSystemType.Windows => DetectWindowsPackageManager(),
            _ => PackageManagerType.Unknown
        };
    }

    private PackageManagerType DetectLinuxPackageManager()
    {
        if (File.Exists("/usr/bin/apt") || File.Exists("/bin/apt"))
            return PackageManagerType.Apt;

        if (File.Exists("/usr/bin/dnf"))
            return PackageManagerType.Dnf;

        if (File.Exists("/usr/bin/pacman"))
            return PackageManagerType.Pacman;

        return PackageManagerType.Unknown;
    }

    private PackageManagerType DetectMacPackageManager()
    {
        return File.Exists("/opt/homebrew/bin/brew") ||
               File.Exists("/usr/local/bin/brew")
            ? PackageManagerType.Brew
            : PackageManagerType.Unknown;
    }

    private PackageManagerType DetectWindowsPackageManager()
    {
        if (CommandExists("winget"))
            return PackageManagerType.Winget;

        if (CommandExists("choco"))
            return PackageManagerType.Choco;

        if (CommandExists("scoop"))
            return PackageManagerType.Scoop;

        return PackageManagerType.Unknown;
    }

    private bool CommandExists(string cmd)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            var full = Path.Combine(p, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"{cmd}.exe"
                : cmd);

            if (File.Exists(full))
                return true;
        }

        return false;
    }
}