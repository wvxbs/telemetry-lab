# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\Telemetry Lab"),
    [switch]$CreateDesktopShortcut
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceExe = Join-Path $scriptDir "TelemetryLab.WinUI.exe"

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "TelemetryLab.WinUI.exe was not found next to install.ps1. Extract the app zip before installing."
}

$installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
$sourceDirFull = [System.IO.Path]::GetFullPath($scriptDir)
$installExe = Join-Path $installDirFull "TelemetryLab.WinUI.exe"

if ($sourceDirFull -ne $installDirFull) {
    New-Item -ItemType Directory -Force -Path $installDirFull | Out-Null

    Get-ChildItem -LiteralPath $scriptDir -Force | ForEach-Object {
        $destination = Join-Path $installDirFull $_.Name
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $installExe)) {
    throw "Installed executable was not found at $installExe."
}

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuShortcut = Join-Path $startMenuDir "Telemetry Lab.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $installExe
$shortcut.WorkingDirectory = $installDirFull
$shortcut.IconLocation = "$installExe,0"
$shortcut.Description = "Telemetry Lab"
$shortcut.Save()

if ($CreateDesktopShortcut) {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "Telemetry Lab.lnk"
    $desktopLink = $shell.CreateShortcut($desktopShortcut)
    $desktopLink.TargetPath = $installExe
    $desktopLink.WorkingDirectory = $installDirFull
    $desktopLink.IconLocation = "$installExe,0"
    $desktopLink.Description = "Telemetry Lab"
    $desktopLink.Save()
}

$appPathKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\TelemetryLab.WinUI.exe"
New-Item -Path $appPathKey -Force | Out-Null
Set-Item -Path $appPathKey -Value $installExe
Set-ItemProperty -Path $appPathKey -Name "Path" -Value $installDirFull

$uninstallScript = Join-Path $installDirFull "uninstall.ps1"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TelemetryLab.WinUI"
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Telemetry Lab"
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $installExe
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "0.1"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Gabriel Ferreira"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDirFull
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Type DWord -Value 1
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Type DWord -Value 1
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
Set-ItemProperty -Path $uninstallKey -Name "QuietUninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`" -Quiet"

Write-Host "Telemetry Lab installed at $installDirFull"
Write-Host "Start Menu shortcut: $startMenuShortcut"
Write-Host "You can also run it from Win+R with: TelemetryLab.WinUI.exe"
