[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('yara','clamav')][string]$Engine,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][uri]$Url,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Url.Scheme -ne 'https') { throw 'Sono accettati esclusivamente URL HTTPS.' }
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('FFGuardian-EngineApproval-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$archive = Join-Path $tempRoot ([IO.Path]::GetFileName($Url.AbsolutePath))
$extract = Join-Path $tempRoot 'extract'

function Expand-ZipSafely([string]$ZipPath, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $root = [IO.Path]::GetFullPath($Destination) + [IO.Path]::DirectorySeparatorChar
    $zip = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $entry.FullName))
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Path traversal rilevato nel pacchetto: $($entry.FullName)"
            }
        }
    } finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $Destination)
}

try {
    Invoke-WebRequest -Uri $Url -OutFile $archive -UseBasicParsing
    $actualHash = (Get-FileHash -Path $archive -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -ne $ExpectedSha256.ToUpperInvariant()) {
        throw "SHA-256 non corrispondente. Atteso $ExpectedSha256, ottenuto $actualHash"
    }
    Expand-ZipSafely -ZipPath $archive -Destination $extract

    $candidateNames = if ($Engine -eq 'yara') { @('yara64.exe','yara.exe') } else { @('clamscan.exe') }
    $executable = Get-ChildItem -Path $extract -Recurse -File | Where-Object { $candidateNames -contains $_.Name } | Select-Object -First 1
    if (-not $executable) { throw "Eseguibile richiesto non trovato per $Engine." }

    $versionOutput = & $executable.FullName --version 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "--version fallito per $Engine con codice $LASTEXITCODE. $versionOutput" }
    if ($versionOutput -notmatch [regex]::Escape($Version)) { throw "Versione runtime diversa da $Version. Output: $versionOutput" }

    $report = [ordered]@{
        engine = $Engine
        version = $Version
        url = $Url.AbsoluteUri
        fileName = [IO.Path]::GetFileName($archive)
        size = (Get-Item $archive).Length
        sha256 = $actualHash
        runtimeVersion = $versionOutput.Trim()
        executable = $executable.FullName
        acquiredAt = [DateTimeOffset]::UtcNow.ToString('O')
        approved = $false
        note = "Report tecnico generato. L'approvazione richiede revisione manuale e PR dedicata."
    }
    $reportDirectory = Split-Path -Parent $ReportPath
    if ($reportDirectory) { New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null }
    $report | ConvertTo-Json -Depth 5 | Set-Content -Path $ReportPath -Encoding UTF8
    Write-Host "Verifica tecnica completata. Report: $ReportPath"
}
finally {
    Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
