param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

dotnet restore ./FFGuardian.App/FFGuardian.App.csproj
dotnet build ./FFGuardian.App/FFGuardian.App.csproj -c $Configuration
$publishDir = "$(pwd)\artifacts\ffguardian\$Runtime\publish"
dotnet publish ./FFGuardian.App/FFGuardian.App.csproj -c $Configuration -r $Runtime --self-contained true -o $publishDir
Write-Host "Publish finished. Artifacts in $publishDir"
