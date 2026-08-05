[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('yara','clamav')][string]$Engine,
    [Parameter(Mandatory)][string]$LockPath,
    [Parameter(Mandatory)][string]$InstallRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $LockPath)) { throw "Lock file mancante: $LockPath" }
$lock = Get-Content -Path $LockPath -Raw | ConvertFrom-Json
$entry = $lock.$Engine
if (-not $entry) { throw "Sezione motore mancante: $Engine" }

$required = @('version','architecture','fileName','downloadUrl','sha256','source','license','approved','approvedBy','approvedAt')
foreach ($field in $required) {
    if ($null -eq $entry.$field -or [string]::IsNullOrWhiteSpace([string]$entry.$field)) {
        throw "Campo obbligatorio mancante per ${Engine}: $field"
    }
}
if ($entry.approved -ne $true) { throw "$Engine non approvato nel lock file." }
if ($entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "SHA-256 non valido per $Engine." }
$uri = [uri]$entry.downloadUrl
if ($uri.Scheme -ne 'https') { throw "Origine non HTTPS per $Engine." }

$engineDirectory = if ($Engine -eq 'yara') { Join-Path $InstallRoot 'Engine/Yara' } else { Join-Path $InstallRoot 'Engine/ClamAV' }
$executableNames = if ($Engine -eq 'yara') { @('yara64.exe','yara.exe') } else { @('clamscan.exe') }
$executable = Get-ChildItem -Path $engineDirectory -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $executableNames -contains $_.Name } | Select-Object -First 1
if (-not $executable) { throw "Eseguibile installato non trovato per $Engine in $engineDirectory" }

$versionOutput = & $executable.FullName --version 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "--version fallito per ${Engine}: $versionOutput" }
if ($versionOutput -notmatch [regex]::Escape([string]$entry.version)) {
    throw "Versione runtime non corrispondente per $Engine. Attesa $($entry.version), output: $versionOutput"
}

Write-Host "${Engine}: verificato $($entry.version) - $($executable.FullName)"
