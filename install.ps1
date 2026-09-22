<#
.SYNOPSIS
    Installs the EnvManager CLI.

.DESCRIPTION
    Downloads the EnvManager executable for Windows, places it in a per-user
    installation directory, and ensures that directory is on the user's PATH.

.EXAMPLE
    irm "https://raw.githubusercontent.com/WolfBublitz/EnvManager/refs/heads/master/install.ps1" -UseBasicParsing | iex
#>

[CmdletBinding()]
param(
    [string]$Version = "latest",
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA "Programs\EnvManager")
)

$ErrorActionPreference = "Stop"

$Repository = "WolfBublitz/EnvManager"
$BinaryName = "EnvManager.exe"
$AssetName = "EnvManager-win-x64.exe"

if ($env:PROCESSOR_ARCHITECTURE -ne "AMD64" -and $env:PROCESSOR_ARCHITEW6432 -ne "AMD64") {
    Write-Error "Unsupported architecture: $env:PROCESSOR_ARCHITECTURE. Only 64-bit Windows (x64) is supported."
}

if ($Version -eq "latest") {
    $DownloadUrl = "https://github.com/$Repository/releases/latest/download/$AssetName"
}
else {
    $DownloadUrl = "https://github.com/$Repository/releases/download/$Version/$AssetName"
}

Write-Host "==> Downloading $AssetName ($Version)..." -ForegroundColor Green

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
$DestinationPath = Join-Path $InstallDirectory $BinaryName

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $DestinationPath -UseBasicParsing
}
catch {
    Write-Error "Failed to download EnvManager from $DownloadUrl. $_"
}

Write-Host "==> Installed EnvManager to $DestinationPath" -ForegroundColor Green

# ┌────────────────────────────────────────────────────────────┐
# │ Ensure the install directory is on the user's PATH          │
# └────────────────────────────────────────────────────────────┘

$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
$PathEntries = $UserPath -split ";" | Where-Object { $_ -ne "" }

if ($PathEntries -notcontains $InstallDirectory) {
    $NewUserPath = ($PathEntries + $InstallDirectory) -join ";"
    [Environment]::SetEnvironmentVariable("Path", $NewUserPath, "User")
    $env:Path = "$env:Path;$InstallDirectory"
    Write-Host "==> Added $InstallDirectory to your user PATH. Restart your terminal for it to take effect." -ForegroundColor Yellow
}

Write-Host "==> Run 'EnvManager --help' to get started." -ForegroundColor Green
