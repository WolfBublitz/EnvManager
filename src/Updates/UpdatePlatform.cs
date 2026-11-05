// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: UpdatePlatform.cs                                                       │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Runtime.InteropServices;

using EnvManager.Exceptions;

namespace EnvManager.Updates;

internal static class UpdatePlatform
{
    public static string GetAssetName()
        => GetAssetName(OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture);

    internal static string GetAssetName(bool isWindows, bool isLinux, bool isMacOS, Architecture architecture)
    {
        string architectureName = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new ConfigurationException($"Self-update is not supported on the {architecture} architecture."),
        };

        if (isWindows && architectureName == "x64")
        {
            return "EnvManager-windows-x64.exe";
        }

        if (isLinux)
        {
            return $"EnvManager-linux-{architectureName}";
        }

        if (isMacOS)
        {
            return $"EnvManager-macos-{architectureName}";
        }

        if (isWindows)
        {
            throw new ConfigurationException($"Self-update is not supported on Windows {architectureName}.");
        }

        throw new ConfigurationException("Self-update is not supported on this operating system.");
    }

    public static string GetRunningExecutablePath()
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)
            || !IsSupportedExecutableName(System.IO.Path.GetFileName(executablePath), GetAssetName(), OperatingSystem.IsWindows()))
        {
            throw new ConfigurationException("Self-update requires running the installed EnvManager executable directly.");
        }

        return System.IO.Path.GetFullPath(executablePath);
    }

    internal static bool IsSupportedExecutableName(string executableName, string releaseAssetName, bool isWindows)
        => string.Equals(executableName, isWindows ? "EnvManager.exe" : "EnvManager", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executableName, releaseAssetName, StringComparison.OrdinalIgnoreCase);
}
