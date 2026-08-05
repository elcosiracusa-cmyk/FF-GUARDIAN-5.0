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
        throw "${Name}: identità o data di approvazione mancanti."
    }

    $sha256 = if ($approval.PSObject.Properties.Name -contains 'assetSha256') { [string]$approval.assetSha256 } else { [string]$approval.sha256 }
    $size = if ($approval.PSObject.Properties.Name -contains 'assetSize') { [long]$approval.assetSize } else { [long]$approval.size }
    if ($sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "${Name}: SHA-256 approvato mancante." }
    if ($size -le 0) { throw "${Name}: dimensione approvata mancante." }
    return $approval
}

New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
$lockPath = Join-Path $RepositoryRoot 'engines.lock.json'
if (-not (Test-Path $lockPath)) { throw "Lock file non trovato: $lockPath" }
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json

foreach ($name in @('yara','clamav')) {
    if ($Engine -ne 'all' -and $Engine -ne $name) { continue }
    $approval = Read-Approval $name
    $config = $lock.$name
    if ($null -eq $config) { throw "${name}: configurazione assente in engines.lock.json." }

    $approvalUrl = if ($approval.PSObject.Properties.Name -contains 'assetUrl') { [string]$approval.assetUrl } else { [string]$approval.officialUrl }
    $approvalSha = if ($approval.PSObject.Properties.Name -contains 'assetSha256') { [string]$approval.assetSha256 } else { [string]$approval.sha256 }
    $approvalSize = if ($approval.PSObject.Properties.Name -contains 'assetSize') { [long]$approval.assetSize } else { [long]$approval.size }
    $configUrl = if ($config.PSObject.Properties.Name -contains 'assetUrl') { [string]$config.assetUrl } else { [string]$config.downloadUrl }
    $configSha = if ($config.PSObject.Properties.Name -contains 'assetSha256') { [string]$config.assetSha256 } else { [string]$config.sha256 }
    $configSize = if ($config.PSObject.Properties.Name -contains 'assetSize') { [long]$config.assetSize } else { [long]$config.size }

    if ([string]$approval.version -ne [string]$config.version) { throw "${name}: versione approval/lock diversa." }
    if ($approvalUrl -ne $configUrl) { throw "${name}: URL approval/lock diverso." }
    if (-not $approvalSha.Equals($configSha, [StringComparison]::OrdinalIgnoreCase)) { throw "${name}: hash approval/lock diverso." }
    if ($approvalSize -ne $configSize) { throw "${name}: dimensione approval/lock diversa." }
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
