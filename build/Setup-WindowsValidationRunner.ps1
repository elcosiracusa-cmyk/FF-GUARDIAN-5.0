[CmdletBinding()]
param(
    [string]$Root = 'C:\FFGuardianValidation',
    [int]$MinimumMemoryGb = 8,
    [int]$MinimumLogicalProcessors = 4,
    [int]$MinimumFreeSpaceGb = 80
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Eseguire lo script da PowerShell elevata sul runner dedicato.'
    }
}

Assert-Administrator
if (-not [Environment]::Is64BitOperatingSystem) { throw 'È richiesto Windows x64.' }
$os = Get-CimInstance Win32_OperatingSystem
if ([version]$os.Version -lt [version]'10.0.22000') { throw 'È richiesto Windows 11 o Windows Server equivalente aggiornato.' }
$computer = Get-CimInstance Win32_ComputerSystem
$memoryGb = [math]::Floor($computer.TotalPhysicalMemory / 1GB)
if ($memoryGb -lt $MinimumMemoryGb) { throw "RAM insufficiente: ${memoryGb} GB." }
if ($computer.NumberOfLogicalProcessors -lt $MinimumLogicalProcessors) { throw 'Numero di processori logici insufficiente.' }
$drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot($Root).TrimEnd(':\'))
$freeGb = [math]::Floor($drive.Free / 1GB)
if ($freeGb -lt $MinimumFreeSpaceGb) { throw "Spazio libero insufficiente: ${freeGb} GB." }

$paths = @(
    $Root,
    (Join-Path $Root 'engines'),
    (Join-Path $Root 'database'),
    (Join-Path $Root 'artifacts'),
    (Join-Path $Root 'reports'),
    (Join-Path $Root 'logs'),
    (Join-Path $Root 'temp')
)
foreach ($path in $paths) { New-Item -ItemType Directory -Path $path -Force | Out-Null }

$defender = Get-MpComputerStatus
if (-not $defender.AntivirusEnabled) { throw 'Microsoft Defender deve restare attivo.' }
$profiles = Get-NetFirewallProfile
if ($profiles | Where-Object { -not $_.Enabled }) { throw 'Tutti i profili Windows Firewall devono restare attivi.' }

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    machine = $env:COMPUTERNAME
    os = $os.Caption
    osVersion = $os.Version
    architecture = $env:PROCESSOR_ARCHITECTURE
    memoryGb = $memoryGb
    logicalProcessors = $computer.NumberOfLogicalProcessors
    freeSpaceGb = $freeGb
    defenderEnabled = $defender.AntivirusEnabled
    firewallProfiles = @($profiles | Select-Object Name, Enabled)
    validationRoot = $Root
    snapshotRequired = $true
}
$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $Root 'reports\runner-prerequisites.json') -Encoding UTF8
Write-Host "Runner dedicato verificato. Creare o confermare lo snapshot pulito prima dei test."
