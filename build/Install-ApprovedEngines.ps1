[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$DestinationRoot = 'C:\FFGuardianValidation\engines',
    [ValidateSet('all','yara','clamav')][string]$Engine = 'all'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Approval([string]$Name) {
    $path = Join-Path $RepositoryRoot "security\approvals\$Name-approval.json"
    if (-not (Test-Path $path)) { throw "Approvazione mancante: $path" }
    $approval = Get-Content $path -Raw | ConvertFrom-Json
    if ($approval.approved -ne $true) { throw "$Name non approvato dal processo protetto." }
    if ([string]::IsNullOrWhiteSpace([string]$approval.approvedBy) -or [string]::IsNullOrWhiteSpace([string]$approval.approvedAtUtc)) {
        throw "$Name: identità o data di approvazione mancanti."
    }
    if ([string]$approval.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "$Name: SHA-256 approvato mancante." }
    if ([long]$approval.size -le 0) { throw "$Name: dimensione approvata mancante." }
    return $approval
}

New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
$lockPath = Join-Path $RepositoryRoot 'engines.lock.json'
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json

foreach ($name in @('yara','clamav')) {
    if ($Engine -ne 'all' -and $Engine -ne $name) { continue }
    $approval = Read-Approval $name
    $config = $lock.$name
    if ([string]$approval.version -ne [string]$config.version) { throw "$name: versione approval/lock diversa." }
    if ([string]$approval.officialUrl -ne [string]$config.downloadUrl) { throw "$name: URL approval/lock diverso." }
    if (-not ([string]$approval.sha256).Equals([string]$config.sha256, [StringComparison]::OrdinalIgnoreCase)) { throw "$name: hash approval/lock diverso." }
    if ([long]$approval.size -ne [long]$config.size) { throw "$name: dimensione approval/lock diversa." }
}

& (Join-Path $PSScriptRoot 'Install-SecurityEngines.ps1') -LockFile $lockPath -DestinationRoot $DestinationRoot -Engine $Engine
if ($LASTEXITCODE -ne 0) { throw 'Installazione motori approvati fallita.' }

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    destinationRoot = $DestinationRoot
    engine = $Engine
    yaraApproval = if ($Engine -in @('all','yara')) { Read-Approval 'yara' } else { $null }
    clamavApproval = if ($Engine -in @('all','clamav')) { Read-Approval 'clamav' } else { $null }
}
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $DestinationRoot 'approved-engines-installation.json') -Encoding UTF8
