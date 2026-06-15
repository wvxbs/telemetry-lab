# Telemetry Lab Windows App

This document explains how to use, install, package, and publish the native WinUI 3 version of Telemetry Lab.

## Why Use The Windows App

Use the Windows app when you want the most native workflow:

- open HWiNFO CSV logs from the local machine;
- use live reload against a CSV that is still being written;
- inspect game-focused telemetry with quick cards for FPS, lows, GPU/CPU power, temperatures, RAM, VRAM, and utilization;
- use a Windows 11-style interface instead of the browser dashboard;
- optionally install Telemetry Lab like a normal per-user Windows app.

Use the Streamlit/Docker app when you want the web dashboard, browser uploads/downloads, or a containerized workflow.

## Download Options

GitHub Actions publishes two Windows artifacts from the **Build Windows app** workflow.

### Single-file launcher

Artifact:

```text
TelemetryLab-WinUI3-windows-x64-exe
```

This contains one file:

```text
TelemetryLab-WinUI3-windows-x64.exe
```

This is the easiest way to try the app. Run it directly. The launcher extracts the WinUI app into:

```text
%LOCALAPPDATA%\Telemetry Lab\WinUI\app
```

Then it starts:

```text
TelemetryLab.WinUI.exe
```

This is not a traditional installer. It is a low-friction launcher/cache wrapper around the full WinUI app.

### Portable folder

Artifact:

```text
TelemetryLab-WinUI3-windows-x64
```

This contains the full self-contained WinUI folder. Extract it and run:

```text
TelemetryLab.WinUI.exe
```

Use this when you want to inspect the files, keep the app portable, or run the included PowerShell install scripts yourself.

## Running Without Installing

For quick use, download the single-file launcher and run it.

For a fully portable run, download the portable folder artifact, extract it anywhere, and run:

```powershell
.\TelemetryLab.WinUI.exe
```

The app reads CSV files chosen through the interface or typed paths available to the app process. It does not require fixed container paths.

## Installing

Install only if you want Windows integration:

- Start Menu shortcut;
- optional desktop shortcut;
- Windows installed apps entry;
- App Paths registration, so `TelemetryLab.WinUI.exe` can be resolved by Explorer/Win+R.

Open the native app and go to:

```text
Instalação / Installation
```

Use:

- **Install** to copy the app to `%LOCALAPPDATA%\Programs\Telemetry Lab`;
- **Update** to refresh that installed copy from the current package;
- **Repair** to recreate shortcuts/registration;
- **Uninstall** to remove the per-user installation.

Terminal alternative from the extracted portable folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CreateDesktopShortcut
```

Uninstall manually:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## Live Reload

Live reload watches a readable CSV path and reloads when file size or modification time changes.

Use it for HWiNFO logs that are still being written.

Expected workflow:

1. Start HWiNFO logging to CSV.
2. Open or paste the CSV path in Telemetry Lab.
3. Enable **Leitura dinâmica / Live reload**.
4. Watch the state text under the toggle.

The state text tells you whether the app is:

- off;
- waiting for a valid path;
- watching a specific file;
- reloading after a detected change;
- blocked because the file is missing;
- failed while reading.

## Gaming View

The **Jogos / Gaming** tab is the fast-read dashboard for game sessions.

It prioritizes:

- current FPS;
- average FPS;
- 1% low;
- 0.1% low;
- GPU power;
- CPU/GPU temperatures;
- GPU/CPU utilization;
- RAM and VRAM usage;
- memory/SPD hub temperatures when available.

Voltage sensors are intentionally filtered out of this page because they are usually less useful for quick game-performance diagnosis.

GPU power is ranked to prefer the more representative sensors first, such as:

```text
GPU Consumo de energia [W]
GPU Power [W]
GPU Total Power [W]
TGP / TBP
```

Lower-level rails and subcomponent sensors, such as `Fonte PP`, `NVVDD`, and individual line sensors, remain available in detailed tables but are not preferred for the main GPU power card.

VRAM is ranked to prefer dedicated video memory counters first, such as:

```text
Memória dedicada GPU D3D [MB]
Memória GPU alocada [MB]
Dedicated GPU Memory [MB]
GPU Memory Allocated [MB]
```

Percentage sensors such as `Uso de memória GPU [%]` are only fallbacks for the VRAM card. Available/free memory and dynamic/shared GPU memory are not preferred because they do not represent dedicated VRAM currently consumed by the game as clearly.

## Signatures And SmartScreen

Windows executables downloaded from the internet can be blocked by SmartScreen if they are unsigned or signed by an unknown/untrusted certificate.

Feature-branch artifacts may be unsigned so contributors can test builds without owning a signing certificate. These builds can still be blocked by Windows.

Release-quality artifacts require Authenticode signing:

- pushes to `main`;
- version tags such as `v0.1.0`;
- GitHub Packages.

Maintainers must configure these repository or environment secrets:

```text
WINDOWS_CODESIGN_PFX_BASE64
WINDOWS_CODESIGN_PFX_PASSWORD
```

Optional repository/environment variable:

```text
CODESIGN_TIMESTAMP_URL
```

If signing is required and those secrets are missing, the workflow fails instead of uploading a misleading unsigned executable.

Each Windows artifact includes a SHA256 checksum file:

```text
TelemetryLab-WinUI3-windows-x64.sha256.txt
```

## Creating The PFX Secret

Use a valid Authenticode code-signing certificate. Export it as a `.pfx`, then encode it as Base64.

PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\certificate.pfx")) |
  Set-Content -LiteralPath "certificate.pfx.base64.txt" -Encoding ASCII
```

Add the contents of `certificate.pfx.base64.txt` as:

```text
WINDOWS_CODESIGN_PFX_BASE64
```

Add the PFX password as:

```text
WINDOWS_CODESIGN_PFX_PASSWORD
```

Do not commit the `.pfx`, its password, or the Base64 text file.

## CI Workflows

Windows app artifacts:

```text
.github/workflows/winui3-build.yml
```

Behavior:

- `feature/winui3-shell`: builds development artifacts; signing is optional;
- `main`: builds release-quality artifacts; signing is required;
- `v*` tags: builds release zip/exe and uploads them to GitHub Releases; signing is required;
- manual `workflow_dispatch`: follows the branch/tag context.

GitHub Packages:

```text
.github/workflows/winui-package.yml
```

Behavior:

- runs on `main` or manual dispatch;
- publishes a NuGet package as a distribution carrier;
- includes the single-file launcher, portable zip, and SHA256 file under `tools/win-x64`;
- signing is required.

Docker image publishing is separate and is not replaced by the WinUI workflows.

## Local Build

Build from a normal Windows path, not a WSL UNC path, because WinUI/MSIX tooling can be fragile with UNC paths.

```powershell
dotnet build .\winui\TelemetryLab.WinUI\TelemetryLab.WinUI.csproj -c Release
```

Publish the portable WinUI app:

```powershell
dotnet publish .\winui\TelemetryLab.WinUI\TelemetryLab.WinUI.csproj `
  -c Release `
  -r win-x64 `
  -p:SelfContained=true `
  -p:WindowsPackageType=None `
  -o .\artifacts\TelemetryLab-WinUI3
```

Copy install helpers:

```powershell
Copy-Item .\winui\TelemetryLab.WinUI\Packaging\install.ps1, `
  .\winui\TelemetryLab.WinUI\Packaging\uninstall.ps1 `
  -Destination .\artifacts\TelemetryLab-WinUI3 `
  -Force
```

Create the payload zip:

```powershell
Compress-Archive `
  -Path .\artifacts\TelemetryLab-WinUI3\* `
  -DestinationPath .\artifacts\TelemetryLab.WinUI-payload.zip `
  -CompressionLevel Optimal `
  -Force
```

Publish the single-file launcher:

```powershell
dotnet publish .\winui\TelemetryLab.Launcher\TelemetryLab.Launcher.csproj `
  -c Release `
  -r win-x64 `
  -p:SelfContained=true `
  -p:PublishSingleFile=true `
  -p:PayloadZip="$(Resolve-Path .\artifacts\TelemetryLab.WinUI-payload.zip)" `
  -o .\artifacts\launcher
```

The launcher output is:

```text
artifacts/launcher/TelemetryLab.exe
```

## Troubleshooting

If Windows blocks a downloaded executable:

- check whether it is a feature-branch unsigned build;
- prefer a signed `main`, tag, Package, or Release artifact;
- verify the SHA256 checksum;
- make sure the artifact was not modified after download.

If a CI run fails at signing:

- for feature branches, check the `Resolve signing policy` step and make sure `REQUIRE_CODESIGN=false`;
- for `main`, tags, and Packages, configure `WINDOWS_CODESIGN_PFX_BASE64` and `WINDOWS_CODESIGN_PFX_PASSWORD`;
- confirm the PFX is valid for code signing;
- confirm the password matches the PFX.

If the native app cannot open:

- try the portable zip instead of the launcher;
- check `%LOCALAPPDATA%\Telemetry Lab\launcher-error.log`;
- delete `%LOCALAPPDATA%\Telemetry Lab\WinUI\app` and run the launcher again;
- download a fresh artifact from a successful workflow run.
