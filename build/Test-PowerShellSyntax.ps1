[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -Path (Join-Path $RepositoryRoot 'build') -Filter '*.ps1' -File -Recurse | Sort-Object FullName
if ($files.Count -eq 0) { throw 'Nessuno script PowerShell trovato in build/.' }

$allErrors = [System.Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
    foreach ($error in $errors) {
        $allErrors.Add([pscustomobject]@{
            File = $file.FullName
            Line = $error.Extent.StartLineNumber
            Column = $error.Extent.StartColumnNumber
            ErrorId = $error.ErrorId
            Message = $error.Message
            Text = $error.Extent.Text
        })
    }
}

if ($allErrors.Count -gt 0) {
    $allErrors | Format-Table -AutoSize | Out-String | Write-Error
    throw "PowerShell syntax check fallito: $($allErrors.Count) errore/i."
}

Write-Host "PowerShell syntax check superato: $($files.Count) script analizzati."
