using System.Collections.Generic;

internal sealed class PackageManagerFactory(SystemInfo systemInfo, Logger logger)
{
    public async IAsyncEnumerable<PackageManagerInfo> GetAvailablePackageManagersAsync()
    {
        if (systemInfo.OperatingSystem == OperatingSystemType.MacOS)
        {
            if (await HomebrewPackageManager.IsAvailableAsync().ConfigureAwait(false))
            {
                yield return new PackageManagerInfo
                {
                    Name = "Homebrew",
                    Description = "The Homebrew package manager for macOS",
                    Type = typeof(HomebrewPackageManager)
                };
            }
        }
        else if (systemInfo.OperatingSystem == OperatingSystemType.Windows)
        {
            if (await ScoopPackageManager.IsAvailableAsync().ConfigureAwait(false))
            {
                yield return new PackageManagerInfo
                {
                    Name = "Scoop",
                    Description = "The Scoop package manager for Windows",
                    Type = typeof(ScoopPackageManager)
                };
            }
        }
        else if (systemInfo.OperatingSystem == OperatingSystemType.Linux)
        {
            if (await AptPackageManager.IsAvailableAsync().ConfigureAwait(false))
            {
                yield return new PackageManagerInfo
                {
                    Name = "Apt",
                    Description = "The Apt package manager for Linux",
                    Type = typeof(AptPackageManager)
                };
            }
        }
    }
}