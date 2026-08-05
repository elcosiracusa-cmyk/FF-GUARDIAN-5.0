[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ManifestPath,
    [Parameter(Mandatory = $true)] [string]$SignaturePath,
    [Parameter(Mandatory = $true)] [string]$PublicKeyPath,
    [Parameter(Mandatory = $false)] [string]$PrivateKeyPem = $env:FFGUARDIAN_RELEASE_PRIVATE_KEY_PEM
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PrivateKeyPem)) {
    throw 'Chiave privata Release non disponibile nell’ambiente protetto.'
}
if (-not (Test-Path $ManifestPath)) { throw "Manifesto non trovato: $ManifestPath" }

$manifestBytes = [IO.File]::ReadAllBytes((Resolve-Path $ManifestPath).Path)
$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($PrivateKeyPem)
    $signature = $rsa.SignData(
        $manifestBytes,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pss)
    [IO.File]::WriteAllBytes($SignaturePath, $signature)

    $publicPem = $rsa.ExportSubjectPublicKeyInfoPem()
    [IO.File]::WriteAllText($PublicKeyPath, $publicPem, [Text.UTF8Encoding]::new($false))

    $publicRsa = [Security.Cryptography.RSA]::Create()
    try {
        $publicRsa.ImportFromPem($publicPem)
        $verified = $publicRsa.VerifyData(
            $manifestBytes,
            $signature,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pss)
        if (-not $verified) { throw 'Verifica immediata della firma Release fallita.' }
    }
    finally { $publicRsa.Dispose() }

    Write-Host 'Manifesto firmato e firma verificata. Nessun materiale privato è stato scritto nel repository.'
}
finally {
    $rsa.Dispose()
    $PrivateKeyPem = $null
}
