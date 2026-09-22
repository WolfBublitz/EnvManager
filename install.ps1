<#
.SYNOPSIS
Installs the EnvManager CLI.

.DESCRIPTION
Downloads the EnvManager executable for the current Windows architecture, places it
in a per-user installation directory, and adds that directory to the user PATH.

.PARAMETER Version
The release version to install. Defaults to the latest release.

.PARAMETER InstallDir
The directory where EnvManager.exe is installed. Defaults to
%LOCALAPPDATA%\Programs\EnvManager.

.EXAMPLE
& ([scriptblock]::Create((Invoke-RestMethod
    "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1"))) -Version 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Version = "latest",

    [Parameter()]
    [Alias("InstallDirectory")]
    [ValidateNotNullOrEmpty()]
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\EnvManager")
)

$ErrorActionPreference = "Stop"

$repository = "WolfBublitz/EnvManager"
$binaryName = "EnvManager.exe"
$architecture = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { throw "Unsupported architecture: 32-bit Windows is not supported." }
$assetName = "EnvManager-windows-$architecture.exe"

if ($Version -eq "latest") {
    $downloadUrl = "https://github.com/$repository/releases/latest/download/$assetName"
}
else {
    $downloadUrl = "https://github.com/$repository/releases/download/$Version/$assetName"
}

Write-Verbose "Downloading $assetName from $downloadUrl"
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$destinationPath = Join-Path $InstallDir $binaryName
$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) "$binaryName.$([guid]::NewGuid()).tmp"

try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $temporaryPath -UseBasicParsing
    Move-Item -Path $temporaryPath -Destination $destinationPath -Force
}
catch {
    throw "Failed to download EnvManager from $downloadUrl. $($_.Exception.Message)"
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

Write-Host "==> Installed EnvManager to $destinationPath" -ForegroundColor Green

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$pathEntries = @($userPath -split ";" | Where-Object { $_ })
$pathComparison = [StringComparer]::OrdinalIgnoreCase

if (-not ($pathEntries | Where-Object { $pathComparison.Equals($_, $InstallDir) })) {
    $newUserPath = ($pathEntries + $InstallDir) -join ";"
    [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
    $env:Path = "$env:Path;$InstallDir"
    Write-Host "==> Added $InstallDir to your user PATH. Restart your terminal to load it." -ForegroundColor Yellow
}

Write-Host "==> Run 'EnvManager --help' to get started." -ForegroundColor Green
