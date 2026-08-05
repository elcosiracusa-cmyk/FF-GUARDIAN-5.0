[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [Alias('LockPath')]
    [string]$LockFile = (Join-Path $PSScriptRoot '..\engines.lock.json'),

    [Parameter(Mandatory = $false)]
    [string]$DestinationRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [ValidateSet('all', 'yara', 'clamav')]
    [string]$Engine = 'all'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-HttpsUrl([string]$Url, [string]$Name) {
    $uri = [Uri]$Url
    if ($uri.Scheme -ne 'https') { throw "$Name deve utilizzare HTTPS." }
    if ([string]::IsNullOrWhiteSpace($uri.Host)) { throw "$Name non contiene un host valido." }
}

function Assert-Sha256([string]$Hash, [string]$Name) {
    if ($Hash -notmatch '^[A-Fa-f0-9]{64}$') {
        throw "SHA-256 verificato mancante o non valido per $Name. Aggiornare engines.lock.json tramite PR dedicata."
    }
}

function Expand-ZipSafely([string]$Archive, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $destinationFull = [IO.Path]::GetFullPath($Destination)
    [IO.Directory]::CreateDirectory($destinationFull) | Out-Null
    $prefix = $destinationFull.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrEmpty($entry.FullName)) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $destinationFull $entry.FullName))
            if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Archivio non sicuro: path traversal rilevato in '$($entry.FullName)'."
            }
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) {
                [IO.Directory]::CreateDirectory($target) | Out-Null
                continue
            }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            $input = $entry.Open()
            try {
                $output = [IO.File]::Open($target, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try { $input.CopyTo($output) } finally { $output.Dispose() }
            } finally { $input.Dispose() }
        }
    } finally { $zip.Dispose() }
}

function Install-Engine($Config, [string]$Name) {
    if ($Config.approved -ne $true) { throw "$Name non approvato nel lock file." }
    if ([string]::IsNullOrWhiteSpace([string]$Config.approvedBy) -or [string]::IsNullOrWhiteSpace([string]$Config.approvedAt)) {
        throw "$Name: metadati di approvazione incompleti."
    }
    Assert-HttpsUrl $Config.downloadUrl "$Name downloadUrl"
    Assert-Sha256 $Config.sha256 $Name
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("FFGuardian-Engine-" + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($tempRoot) | Out-Null
    try {
        $archive = Join-Path $tempRoot $Config.fileName
        Invoke-WebRequest -Uri $Config.downloadUrl -OutFile $archive -UseBasicParsing
        $actualSize = (Get-Item $archive).Length
        if ([long]$Config.size -le 0) { throw "$Name: dimensione approvata mancante." }
        if ([long]$Config.size -ne $actualSize) {
            throw "$Name: dimensione inattesa. Attesa $($Config.size), ottenuta $actualSize."
        }
        $actualHash = (Get-FileHash -Path $archive -Algorithm SHA256).Hash
        if (-not $actualHash.Equals([string]$Config.sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Name: SHA-256 non corrispondente."
        }
        $expanded = Join-Path $tempRoot 'expanded'
        Expand-ZipSafely $archive $expanded
        $destination = [IO.Path]::GetFullPath((Join-Path $DestinationRoot ([string]$Config.destination)))
        if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
        [IO.Directory]::CreateDirectory($destination) | Out-Null
        Get-ChildItem $expanded -Recurse -File | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($expanded, $_.FullName)
            $target = Join-Path $destination $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item $_.FullName $target -Force
        }
        Write-Host "$Name verificato e installato in $destination"
    }
    finally {
        if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

if (-not (Test-Path $LockFile)) { throw "Lock file non trovato: $LockFile" }
$lock = Get-Content $LockFile -Raw | ConvertFrom-Json
if ($Engine -in @('all','yara')) { Install-Engine $lock.yara 'YARA' }
if ($Engine -in @('all','clamav')) { Install-Engine $lock.clamav 'ClamAV' }
