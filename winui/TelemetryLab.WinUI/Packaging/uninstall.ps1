# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\Telemetry Lab"),
    [switch]$Quiet,
    [switch]$StopRunning
)

$ErrorActionPreference = "Stop"

$installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
$startMenuShortcut = Join-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs") "Telemetry Lab.lnk"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "Telemetry Lab.lnk"
$appPathKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\TelemetryLab.WinUI.exe"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TelemetryLab.WinUI"

if ($StopRunning) {
    Get-Process -Name "TelemetryLab.WinUI" -ErrorAction SilentlyContinue | Stop-Process -Force
}

Remove-Item -LiteralPath $startMenuShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -Path $appPathKey -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installDirFull) {
    Remove-Item -LiteralPath $installDirFull -Recurse -Force
}

if (-not $Quiet) {
    Write-Host "Telemetry Lab was removed."
}
