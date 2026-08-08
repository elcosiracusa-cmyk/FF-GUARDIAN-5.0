[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FreshClamPath,
    [Parameter(Mandatory)][string]$SigToolPath,
    [Parameter(Mandatory)][string]$DatabaseDirectory,
    [Parameter(Mandatory)][string]$RuntimeDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

foreach ($exe in @($FreshClamPath, $SigToolPath)) {
    if (-not (Test-Path $exe -PathType Leaf)) { throw "Eseguibile ClamAV non trovato: $exe" }
}

New-Item -ItemType Directory -Path $DatabaseDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $RuntimeDirectory -Force | Out-Null

$db = (Resolve-Path $DatabaseDirectory).Path
$runtime = (Resolve-Path $RuntimeDirectory).Path
$configPath = Join-Path $runtime 'freshclam.conf'
$logPath = Join-Path $runtime 'freshclam.log'

@(
    ('DatabaseDirectory "{0}"' -f $db),
    'DatabaseMirror database.clamav.net',
    'Checks 1'
) | Set-Content $configPath -Encoding ASCII

$stdout = Join-Path $runtime 'freshclam.stdout.log'
$stderr = Join-Path $runtime 'freshclam.stderr.log'
$process = Start-Process -FilePath $FreshClamPath -ArgumentList @("--config-file=$configPath", '--verbose') -WorkingDirectory (Split-Path $FreshClamPath) -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $process.WaitForExit(900000)) {
    try { $process.Kill($true) } catch { }
    throw 'FreshClam timeout dopo 15 minuti.'
}

$outText = if (Test-Path $stdout) { Get-Content $stdout -Raw -ErrorAction SilentlyContinue } else { '' }
$errText = if (Test-Path $stderr) { Get-Content $stderr -Raw -ErrorAction SilentlyContinue } else { '' }
@($outText, $errText) | Set-Content $logPath -Encoding UTF8

if ($process.ExitCode -ne 0) {
    throw "FreshClam exit $($process.ExitCode). Log: $logPath"
}

$databaseReport = [ordered]@{}
foreach ($name in @('main.cvd','daily.cvd','bytecode.cvd')) {
    $path = Join-Path $db $name
    if (-not (Test-Path $path -PathType Leaf)) { throw "Database ClamAV mancante: $name" }
    $raw = & $SigToolPath --info $path 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $raw -notmatch 'Verification OK') {
        throw "Firma database ClamAV non valida: $name`n$raw"
    }
    $version = if ($raw -match 'Version:\s*(\d+)') { $Matches[1] } else { '' }
    $databaseReport[$name] = [ordered]@{
        fileName = $name
        sha256 = (Get-FileHash $path -Algorithm SHA256).Hash
        size = (Get-Item $path).Length
        version = $version
        signatureVerified = $true
    }
}

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    config = $configPath
    log = $logPath
    database = $databaseReport
    success = $true
}
$reportPath = Join-Path $runtime 'freshclam-evidence.json'
$report | ConvertTo-Json -Depth 6 | Set-Content $reportPath -Encoding UTF8
Get-Content $reportPath
