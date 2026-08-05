[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory = $true)]
    [string]$FFGuardianRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$target = [IO.Path]::GetFullPath($FFGuardianRoot)
[IO.Directory]::CreateDirectory($target) | Out-Null

$installer = Join-Path $PSScriptRoot 'Install-ApprovedEngines.ps1'
if (-not (Test-Path $installer)) {
    throw "Script installazione motori non trovato: $installer"
}

& $installer -RepositoryRoot $RepositoryRoot -DestinationRoot $target -Engine all
if ($LASTEXITCODE -ne 0) {
    throw 'Installazione dei motori approvati nella cartella FFGuardian fallita.'
}

$yaraRoot = Join-Path $target 'Engine\Yara'
$clamRoot = Join-Path $target 'Engine\ClamAV'

$yara = @(
    (Join-Path $yaraRoot 'yara64.exe'),
    (Join-Path $yaraRoot 'yara.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

$yarac = @(
    (Join-Path $yaraRoot 'yarac64.exe'),
    (Join-Path $yaraRoot 'yarac.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

$required = @(
    $yara,
    $yarac,
    (Join-Path $clamRoot 'clamscan.exe'),
    (Join-Path $clamRoot 'freshclam.exe')
)

foreach ($file in $required) {
    if ([string]::IsNullOrWhiteSpace([string]$file) -or -not (Test-Path $file)) {
        throw "Payload motori incompleto nella cartella FFGuardian: $file"
    }
}

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    ffGuardianRoot = $target
    yaraExecutable = $yara
    yaracExecutable = $yarac
    clamScanExecutable = (Join-Path $clamRoot 'clamscan.exe')
    freshClamExecutable = (Join-Path $clamRoot 'freshclam.exe')
}

$reportPath = Join-Path $target 'Engine\bundled-engines.json'
[IO.Directory]::CreateDirectory((Split-Path $reportPath)) | Out-Null
$report | ConvertTo-Json -Depth 4 | Set-Content $reportPath -Encoding UTF8
Write-Host "Motori approvati inclusi in: $target\Engine"
