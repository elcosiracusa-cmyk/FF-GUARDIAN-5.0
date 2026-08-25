[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ValidationRoot = 'C:\FFGuardianValidation',
    [int]$ReportRetentionDays = 30
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Get-Process FFGuardian,yara,yara64,yarac,yarac64,clamscan,freshclam -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

$temp = Join-Path $ValidationRoot 'temp'
if (Test-Path $temp -and $PSCmdlet.ShouldProcess($temp, 'Remove temporary validation data')) {
    Get-ChildItem $temp -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Get-ChildItem $ValidationRoot -Directory -Filter 'artifact-*' -ErrorAction SilentlyContinue | ForEach-Object {
    if ($PSCmdlet.ShouldProcess($_.FullName, 'Remove extracted artifact')) {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$cutoff = (Get-Date).AddDays(-$ReportRetentionDays)
foreach ($folderName in @('reports','logs')) {
    $folder = Join-Path $ValidationRoot $folderName
    if (-not (Test-Path $folder)) { continue }
    Get-ChildItem $folder -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object LastWriteTime -lt $cutoff |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Host 'Pulizia completata. Motori approvati, database controllato ed evidenze recenti sono stati conservati.'
