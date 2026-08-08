[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ApplicationRoot,
    [Parameter(Mandatory)][string]$ReportPath,
    [switch]$RequireFreshClamUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [int]$TimeoutSeconds = 60,
        [int[]]$AllowedExitCodes = @(0)
    )
    if (-not (Test-Path $FilePath -PathType Leaf)) { throw "Eseguibile non trovato: $FilePath" }
    $stdout = [IO.Path]::GetTempFileName()
    $stderr = [IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory (Split-Path $FilePath) -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill($true) } catch { }
            throw "Timeout di $TimeoutSeconds secondi: $FilePath $($Arguments -join ' ')"
        }
        $out = Get-Content $stdout -Raw -ErrorAction SilentlyContinue
        $err = Get-Content $stderr -Raw -ErrorAction SilentlyContinue
        if ($AllowedExitCodes -notcontains $process.ExitCode) {
            throw "Exit code $($process.ExitCode) non consentito. STDOUT=$out STDERR=$err"
        }
        return [ordered]@{ exitCode=$process.ExitCode; stdout=$out; stderr=$err }
    }
    finally {
        Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue
    }
}

function Find-First([string[]]$Candidates) {
    foreach ($candidate in $Candidates) { if (Test-Path $candidate -PathType Leaf) { return (Resolve-Path $candidate).Path } }
    return $null
}

$root = (Resolve-Path $ApplicationRoot).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ('FFGuardian-PackagedEngineTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    applicationRoot = $root
    yara = [ordered]@{ included=$false; executable=$null; compiler=$null; version=$null; rulesValid=$false; selfTest=$false }
    clamav = [ordered]@{ included=$false; executable=$null; version=$null; database=$null; cleanTest=$false; eicarTest=$false }
    freshclam = [ordered]@{ included=$false; executable=$null; configuration=$null; version=$null; updateAttempted=$false; updateSucceeded=$false; exitCode=$null }
    engine10 = [ordered]@{ declaredIncluded=$false; assembly=$null; version=$null; selfTest=$false }
    success = $false
    errors = @()
}

try {
    $yaraRoot = Join-Path $root 'Engine\Yara'
    $yara = Find-First @((Join-Path $yaraRoot 'yara64.exe'), (Join-Path $yaraRoot 'yara.exe'))
    $yarac = Find-First @((Join-Path $yaraRoot 'yarac64.exe'), (Join-Path $yaraRoot 'yarac.exe'))
    if (-not $yara -or -not $yarac) { throw 'Payload YARA incompleto.' }
    $report.yara.included = $true; $report.yara.executable = $yara; $report.yara.compiler = $yarac
    $yaraVersion = Invoke-NativeChecked $yara @('--version') 30
    $report.yara.version = (($yaraVersion.stdout + "`n" + $yaraVersion.stderr).Trim() -split "`r?`n")[0]

    $ruleDirectory = Join-Path $yaraRoot 'Rules'
    $rules = @(Get-ChildItem $ruleDirectory -Recurse -File -ErrorAction SilentlyContinue | Where-Object Extension -in @('.yar','.yara'))
    if ($rules.Count -eq 0) { throw 'Nessuna regola YARA inclusa.' }
    foreach ($rule in $rules) {
        $compiled = Join-Path $temp ($rule.BaseName + '-' + [Guid]::NewGuid().ToString('N') + '.yarc')
        Invoke-NativeChecked $yarac @($rule.FullName, $compiled) 30 | Out-Null
        if (-not (Test-Path $compiled)) { throw "YARAC non ha prodotto il file compilato per $($rule.FullName)." }
    }
    $report.yara.rulesValid = $true
    $selfRule = Join-Path $temp 'ffguardian-self-test.yar'
    $selfFile = Join-Path $temp 'ffguardian-self-test.txt'
    @'
rule FFGuardian_Yara_Test
{
    strings:
        $test = "FFGUARDIAN_YARA_TEST_STRING"
    condition:
        $test
}
'@ | Set-Content $selfRule -Encoding ASCII
    'FFGUARDIAN_YARA_TEST_STRING' | Set-Content $selfFile -Encoding ASCII
    $yaraSelf = Invoke-NativeChecked $yara @($selfRule, $selfFile) 30
    if ($yaraSelf.stdout -notmatch 'FFGuardian_Yara_Test') { throw 'YARA non ha restituito il nome della regola di self-test.' }
    $report.yara.selfTest = $true

    $clamRoot = Join-Path $root 'Engine\ClamAV'
    $clamscan = Join-Path $clamRoot 'clamscan.exe'
    $freshclam = Join-Path $clamRoot 'freshclam.exe'
    if (-not (Test-Path $clamscan)) { throw 'clamscan.exe non incluso.' }
    $report.clamav.included = $true; $report.clamav.executable = $clamscan
    $clamVersion = Invoke-NativeChecked $clamscan @('--version') 30
    $report.clamav.version = (($clamVersion.stdout + "`n" + $clamVersion.stderr).Trim() -split "`r?`n")[0]

    $database = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'FFGuardian\ClamAV\Database'
    if (-not (Test-Path $database)) {
        $database = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FFGuardian\ClamAV\Database'
    }
    New-Item -ItemType Directory -Path $database -Force | Out-Null
    $report.clamav.database = $database

    $clean = Join-Path $temp 'clean.txt'
    $eicar = Join-Path $temp 'eicar.txt'
    'FFGuardian harmless validation fixture' | Set-Content $clean -Encoding ASCII
    'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*' | Set-Content $eicar -Encoding ASCII
    $cleanResult = Invoke-NativeChecked $clamscan @('--no-summary', "--database=$database", $clean) 90 @(0)
    $report.clamav.cleanTest = $cleanResult.stdout -notmatch 'FOUND'
    $eicarResult = Invoke-NativeChecked $clamscan @('--no-summary', "--database=$database", $eicar) 90 @(1)
    $report.clamav.eicarTest = $eicarResult.stdout -match 'Eicar.*FOUND'
    if (-not $report.clamav.cleanTest -or -not $report.clamav.eicarTest) { throw 'Self-test ClamAV non superato.' }

    if (-not (Test-Path $freshclam)) { throw 'freshclam.exe non incluso.' }
    $report.freshclam.included = $true; $report.freshclam.executable = $freshclam
    $config = Join-Path $temp 'freshclam.conf'
    @("DatabaseDirectory $database", 'DatabaseMirror database.clamav.net', 'Checks 1', 'Foreground yes') | Set-Content $config -Encoding ASCII
    $report.freshclam.configuration = $config
    # FreshClam validates its configuration even for --version on Windows. Always pass
    # the controlled temporary config so the packaged binary is tested independently
    # from any machine-wide or package-local freshclam.conf.
    $freshVersion = Invoke-NativeChecked $freshclam @("--config-file=$config", '--version') 30
    $report.freshclam.version = (($freshVersion.stdout + "`n" + $freshVersion.stderr).Trim() -split "`r?`n")[0]
    if ($RequireFreshClamUpdate) {
        $report.freshclam.updateAttempted = $true
        $freshResult = Invoke-NativeChecked $freshclam @("--config-file=$config", '--verbose') 600 @(0)
        $report.freshclam.exitCode = $freshResult.exitCode
        $report.freshclam.updateSucceeded = $true
    }

    $engine10Assembly = Find-First @((Join-Path $root 'Engine\Engine10\FFGuardian.App.dll'), (Join-Path $root 'FFGuardian.App.dll'))
    if ($engine10Assembly) {
        $report.engine10.declaredIncluded = $true
        $report.engine10.assembly = $engine10Assembly
        $report.engine10.version = [Diagnostics.FileVersionInfo]::GetVersionInfo($engine10Assembly).FileVersion
        try { [Reflection.AssemblyName]::GetAssemblyName($engine10Assembly) | Out-Null; $report.engine10.selfTest = $true } catch { throw "Assembly Engine10 non caricabile: $($_.Exception.Message)" }
    }

    $report.success = $report.yara.selfTest -and $report.clamav.cleanTest -and $report.clamav.eicarTest -and (-not $RequireFreshClamUpdate -or $report.freshclam.updateSucceeded)
}
catch {
    $report.errors += $_.Exception.Message
    $report.success = $false
}
finally {
    New-Item -ItemType Directory -Path (Split-Path $ReportPath) -Force | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content $ReportPath -Encoding UTF8
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $report.success) { throw "Verifica motori artifact fallita. Report: $ReportPath" }
