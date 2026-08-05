[CmdletBinding()]
param(
    [string]$ClamAvRoot = 'C:\FFGuardianValidation\engines\Engine\ClamAV',
    [string]$DatabaseRoot = 'C:\FFGuardianValidation\database',
    [string]$OutputLock = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'clamav-database.lock.candidate.json')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$freshclam = Join-Path $ClamAvRoot 'freshclam.exe'
$clamscan = Join-Path $ClamAvRoot 'clamscan.exe'
if (-not (Test-Path $freshclam)) { throw "freshclam.exe mancante: $freshclam" }
if (-not (Test-Path $clamscan)) { throw "clamscan.exe mancante: $clamscan" }
New-Item -ItemType Directory -Path $DatabaseRoot -Force | Out-Null
$temp = Join-Path ([IO.Path]::GetTempPath()) ('FFGuardian-FreshClam-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $config = Join-Path $temp 'freshclam.conf'
    @(
        "DatabaseDirectory $DatabaseRoot",
        'DatabaseMirror database.clamav.net',
        'CompressLocalDatabase no',
        'LogVerbose yes',
        "UpdateLogFile $(Join-Path $temp 'freshclam.log')"
    ) | Set-Content $config -Encoding ASCII

    $process = Start-Process -FilePath $freshclam -ArgumentList @('--config-file', $config, '--foreground') -WorkingDirectory $ClamAvRoot -PassThru -Wait -NoNewWindow
    if ($process.ExitCode -ne 0) { throw "FreshClam fallito con codice $($process.ExitCode)." }

    $databaseFiles = @()
    foreach ($base in @('main','daily','bytecode')) {
        $candidate = Get-ChildItem $DatabaseRoot -File | Where-Object { $_.BaseName -eq $base -and $_.Extension -in @('.cvd','.cld') } | Select-Object -First 1
        if ($base -in @('main','daily') -and $null -eq $candidate) { throw "Database obbligatorio $base mancante." }
        if ($null -ne $candidate) {
            $databaseFiles += [ordered]@{
                name = $candidate.Name
                sha256 = (Get-FileHash $candidate.FullName -Algorithm SHA256).Hash
                size = $candidate.Length
                lastWriteUtc = $candidate.LastWriteTimeUtc.ToString('o')
            }
        }
    }

    $clean = Join-Path $temp 'clean sample.txt'
    $eicar = Join-Path $temp 'eicar-test.txt'
    Set-Content $clean 'FFGuardian controlled harmless database validation file.' -Encoding ASCII
    Set-Content $eicar 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*' -Encoding ASCII

    $cleanOutput = & $clamscan '--database' $DatabaseRoot '--no-summary' $clean 2>&1 | Out-String
    $cleanExit = $LASTEXITCODE
    if ($cleanExit -ne 0) { throw "File innocuo non pulito o database non caricabile. Exit=$cleanExit Output=$cleanOutput" }
    $eicarOutput = & $clamscan '--database' $DatabaseRoot '--no-summary' $eicar 2>&1 | Out-String
    $eicarExit = $LASTEXITCODE
    if ($eicarExit -ne 1 -or $eicarOutput -notmatch 'Eicar') { throw "EICAR controllato non rilevato correttamente. Exit=$eicarExit Output=$eicarOutput" }

    $versionOutput = & $clamscan '--database' $DatabaseRoot '--version' 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Impossibile leggere la versione firme.' }
    $lock = [ordered]@{
        schemaVersion = 1
        source = 'FreshClam official database.clamav.net on dedicated protected runner'
        createdAtUtc = [DateTime]::UtcNow.ToString('o')
        signatureVersion = $versionOutput.Trim()
        files = $databaseFiles
        cleanFilePassed = $true
        eicarDetected = $true
        approved = $false
        approvedBy = ''
        approvedAtUtc = ''
        notes = 'Candidate lock. Approval must occur through a protected pull request after report review.'
    }
    $lock | ConvertTo-Json -Depth 8 | Set-Content $OutputLock -Encoding UTF8
    Write-Host "Database ClamAV candidato verificato. Lock candidato: $OutputLock"
}
finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
