[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactZip,
    [string]$ValidationRoot = 'C:\FFGuardianValidation',
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$RequireSignedUiChecklist
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$reports = Join-Path $ValidationRoot 'reports'
$logs = Join-Path $ValidationRoot 'logs'
$extract = Join-Path $ValidationRoot ('artifact-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reports,$logs,$extract -Force | Out-Null

function Invoke-Checked([string]$Name, [scriptblock]$Action) {
    $started = Get-Date
    try {
        & $Action
        [ordered]@{ name=$Name; success=$true; startedAt=$started.ToUniversalTime().ToString('o'); durationMs=[int]((Get-Date)-$started).TotalMilliseconds; error=$null }
    }
    catch {
        [ordered]@{ name=$Name; success=$false; startedAt=$started.ToUniversalTime().ToString('o'); durationMs=[int]((Get-Date)-$started).TotalMilliseconds; error=$_.Exception.ToString() }
    }
}

$results = @()
try {
    if (-not (Test-Path $ArtifactZip)) { throw "Artifact non trovato: $ArtifactZip" }
    $artifactHash = (Get-FileHash $ArtifactZip -Algorithm SHA256).Hash
    Expand-Archive -Path $ArtifactZip -DestinationPath $extract -Force
    $exe = Join-Path $extract 'FFGuardian.exe'
    if (-not (Test-Path $exe)) { throw 'FFGuardian.exe mancante nell artifact.' }

    $results += Invoke-Checked 'artifact-smoke-test' {
        $report = Join-Path $reports 'artifact-smoke.json'
        $p = Start-Process $exe -ArgumentList @('--smoke-test','--report',$report) -WorkingDirectory $ValidationRoot -PassThru -Wait
        if ($p.ExitCode -ne 0 -or -not (Test-Path $report)) { throw "Smoke test fallito. Exit=$($p.ExitCode)" }
        $json = Get-Content $report -Raw | ConvertFrom-Json
        if (-not $json.Success) { throw "Smoke report negativo: $($json.Error)" }
    }

    $results += Invoke-Checked 'runtime-core-tests' {
        dotnet run --project (Join-Path $RepositoryRoot 'FFGuardian.Security.Core.Tests\FFGuardian.Security.Core.Tests.csproj') --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Core tests falliti.' }
    }

    $results += Invoke-Checked 'engine-approval-gate' {
        & (Join-Path $PSScriptRoot 'Install-ApprovedEngines.ps1') -RepositoryRoot $RepositoryRoot -DestinationRoot $extract -Engine all
    }

    $results += Invoke-Checked 'runtime-yara' {
        $p = Start-Process $exe -ArgumentList @('--smoke-test','--report',(Join-Path $reports 'runtime-yara-smoke.json')) -WorkingDirectory $extract -PassThru -Wait
        if ($p.ExitCode -ne 0) { throw "YARA runtime smoke fallito. Exit=$($p.ExitCode)" }
    }

    $results += Invoke-Checked 'runtime-clamav' {
        $lock = Join-Path $RepositoryRoot 'clamav-database.lock.json'
        if (-not (Test-Path $lock)) { throw 'clamav-database.lock.json mancante.' }
        $db = Get-Content $lock -Raw | ConvertFrom-Json
        if ($db.approved -ne $true) { throw 'Database ClamAV non approvato.' }
        $p = Start-Process $exe -ArgumentList @('--smoke-test','--report',(Join-Path $reports 'runtime-clamav-smoke.json')) -WorkingDirectory $extract -PassThru -Wait
        if ($p.ExitCode -ne 0) { throw "ClamAV runtime smoke fallito. Exit=$($p.ExitCode)" }
    }

    if ($RequireSignedUiChecklist) {
        $results += Invoke-Checked 'ui-manual-validation' {
            $checklistPath = Join-Path $RepositoryRoot 'security\validation\ui-manual-checklist.json'
            $checklist = Get-Content $checklistPath -Raw | ConvertFrom-Json
            if ($checklist.signed -ne $true -or [string]::IsNullOrWhiteSpace([string]$checklist.reviewer)) { throw 'Checklist UI non firmata.' }
            foreach ($item in $checklist.checks.PSObject.Properties) { if ($item.Value -ne $true) { throw "Controllo UI non superato: $($item.Name)" } }
            if (-not ([string]$checklist.artifactSha256).Equals($artifactHash,[StringComparison]::OrdinalIgnoreCase)) { throw 'Checklist UI riferita a un artifact diverso.' }
        }
    }

    $remaining = Get-Process FFGuardian,yara,yara64,clamscan,freshclam -ErrorAction SilentlyContinue
    if ($remaining) {
        $remaining | Stop-Process -Force -ErrorAction SilentlyContinue
        $results += [ordered]@{ name='residual-process-check'; success=$false; startedAt=[DateTime]::UtcNow.ToString('o'); durationMs=0; error='Processi residui trovati e terminati.' }
    } else {
        $results += [ordered]@{ name='residual-process-check'; success=$true; startedAt=[DateTime]::UtcNow.ToString('o'); durationMs=0; error=$null }
    }

    $success = -not ($results | Where-Object { -not $_.success })
    $summary = [ordered]@{
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        artifact = [IO.Path]::GetFullPath($ArtifactZip)
        artifactSha256 = $artifactHash
        machine = $env:COMPUTERNAME
        success = $success
        results = $results
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reports 'validation-summary.json') -Encoding UTF8
    if (-not $success) { throw 'Validation suite non superata. Consultare validation-summary.json.' }
}
finally {
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue }
}
