[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$DestinationRoot,
    [string]$LockFile = (Join-Path $PSScriptRoot '..\clamav-database.lock.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $LockFile -PathType Leaf)) { throw "Lock database ClamAV non trovato: $LockFile" }
if (-not (Test-Path $SourceDirectory -PathType Container)) { throw "Artifact database ClamAV non trovato: $SourceDirectory" }

$lock = Get-Content $LockFile -Raw | ConvertFrom-Json
if ($lock.approved -ne $true) { throw 'Database ClamAV non approvato dal processo protetto.' }
if ([string]::IsNullOrWhiteSpace([string]$lock.approvedBy) -or [string]::IsNullOrWhiteSpace([string]$lock.approvedAt)) {
    throw 'Metadati di approvazione database ClamAV incompleti.'
}
if ([long]$lock.sourceRunId -le 0 -or [string]::IsNullOrWhiteSpace([string]$lock.artifactName)) {
    throw 'Provenienza artifact database ClamAV incompleta.'
}

$expected = @(
    [ordered]@{ name='main.cvd'; sha256=[string]$lock.mainCvdSha256; size=[long]$lock.mainCvdSize },
    [ordered]@{ name='daily.cvd'; sha256=[string]$lock.dailyCvdSha256; size=[long]$lock.dailyCvdSize },
    [ordered]@{ name='bytecode.cvd'; sha256=[string]$lock.bytecodeCvdSha256; size=[long]$lock.bytecodeCvdSize }
)

foreach ($item in $expected) {
    if ($item.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "SHA-256 approvato non valido per $($item.name)." }
    if ($item.size -le 0) { throw "Dimensione approvata non valida per $($item.name)." }
}

$destination = Join-Path $DestinationRoot 'Engine\ClamAV\database'
New-Item -ItemType Directory -Path $destination -Force | Out-Null

foreach ($item in $expected) {
    $source = Join-Path $SourceDirectory $item.name
    if (-not (Test-Path $source -PathType Leaf)) { throw "File database approvato mancante: $($item.name)" }
    $actualSize = (Get-Item $source).Length
    if ($actualSize -ne $item.size) {
        throw "Dimensione database non corrispondente per $($item.name). Attesa $($item.size), ottenuta $actualSize."
    }
    $actualHash = (Get-FileHash $source -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($item.sha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 database non corrispondente per $($item.name)."
    }
    Copy-Item $source (Join-Path $destination $item.name) -Force
}

$manifest = [ordered]@{
    installedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    approvedBy = [string]$lock.approvedBy
    approvedAt = [string]$lock.approvedAt
    sourceRunId = [long]$lock.sourceRunId
    sourceCommit = [string]$lock.sourceCommit
    artifactName = [string]$lock.artifactName
    signatureVersion = [string]$lock.signatureVersion
    files = $expected
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $destination 'ffguardian-database-manifest.json') -Encoding UTF8
Write-Host "Database ClamAV approvato verificato e installato in $destination"
