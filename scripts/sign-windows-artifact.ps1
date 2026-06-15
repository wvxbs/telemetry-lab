param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDir,

    [string]$PfxPath = $env:CODESIGN_PFX_PATH,
    [string]$PfxBase64 = $env:CODESIGN_PFX_BASE64,
    [string]$PfxPassword = $env:CODESIGN_PFX_PASSWORD,
    [string]$TimestampUrl = $(if ($env:CODESIGN_TIMESTAMP_URL) { $env:CODESIGN_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }),
    [string[]]$AdditionalTargets = @(),
    [switch]$SkipTimestamp
)

$ErrorActionPreference = "Stop"

function Resolve-SignTool {
    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $windowsKits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path -LiteralPath $windowsKits) {
        $candidate = Get-ChildItem -LiteralPath $windowsKits -Recurse -Filter signtool.exe |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK signing tools."
}

function Get-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if (-not $resolved) {
        throw "Required file was not found: $Path"
    }

    return $resolved.Path
}

function Write-TempPfx {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Base64
    )

    $cleanBase64 = (($Base64 -split "\r?\n") |
        Where-Object { $_ -and $_ -notmatch "^-+BEGIN" -and $_ -notmatch "^-+END" }) -join ""

    $tempPfx = Join-Path ([System.IO.Path]::GetTempPath()) ("telemetry-lab-codesign-{0}.pfx" -f ([Guid]::NewGuid()))
    [System.IO.File]::WriteAllBytes($tempPfx, [Convert]::FromBase64String($cleanBase64))
    return $tempPfx
}

function Test-EmbeddedSignature {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Target,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedThumbprint,

        [Parameter(Mandatory = $true)]
        [bool]$RequireTimestamp
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Target
    if (-not $signature.SignerCertificate) {
        throw "Missing signature: $Target"
    }

    if ($signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint) {
        throw "Unexpected signing certificate: $Target"
    }

    if ($signature.Status -in @("NotSigned", "HashMismatch")) {
        throw "Invalid signature in $Target. Status: $($signature.Status)"
    }

    if ($RequireTimestamp -and -not $signature.TimeStamperCertificate) {
        throw "Signature has no timestamp: $Target"
    }

    Write-Host "Embedded signature matches expected signer for $Target. Status: $($signature.Status)"
}

$artifactPath = Resolve-Path -LiteralPath $ArtifactDir -ErrorAction SilentlyContinue
if (-not $artifactPath) {
    throw "Artifact directory was not found: $ArtifactDir"
}

$temporaryPfx = $null
try {
    if (-not $PfxPath) {
        if (-not $PfxBase64) {
            Write-Host "No code signing certificate configured. Skipping Authenticode signing."
            return
        }

        $temporaryPfx = Write-TempPfx -Base64 $PfxBase64
        $PfxPath = $temporaryPfx
    }

    if (-not $PfxPassword) {
        Write-Host "No code signing password configured. Skipping Authenticode signing."
        return
    }

    $pfxFile = Get-RequiredFile -Path $PfxPath
    $signTool = Resolve-SignTool
    $expectedSigner = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $pfxFile,
        $PfxPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
    )
    $targets = @(
        (Join-Path $artifactPath.Path "TelemetryLab.WinUI.exe")
    )
    foreach ($target in $AdditionalTargets) {
        if ($target) {
            $targets += $target
        }
    }

    foreach ($target in $targets) {
        $null = Get-RequiredFile -Path $target
    }

    Write-Host "Signing Windows executables in $($artifactPath.Path)"
    foreach ($target in $targets) {
        $signArgs = @(
            "sign",
            "/f", $pfxFile,
            "/p", $PfxPassword,
            "/fd", "SHA256"
        )

        if (-not $SkipTimestamp) {
            $signArgs += @("/td", "SHA256", "/tr", $TimestampUrl)
        }

        $signArgs += $target
        & $signTool @signArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to sign: $target"
        }
    }

    foreach ($target in $targets) {
        & $signTool verify /pa /v $target
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "signtool verify did not trust the certificate chain. Validating embedded signature."
        }

        Test-EmbeddedSignature -Target $target -ExpectedThumbprint $expectedSigner.Thumbprint -RequireTimestamp (-not $SkipTimestamp)
    }

    Write-Host "Windows executables signed and verified as Authenticode binaries."
    $global:LASTEXITCODE = 0
}
finally {
    if ($temporaryPfx -and (Test-Path -LiteralPath $temporaryPfx)) {
        Remove-Item -LiteralPath $temporaryPfx -Force
    }
}
