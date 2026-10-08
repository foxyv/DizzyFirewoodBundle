# Packages Dizzy Firewood Bundle for GitHub Releases.
# Usage: .\scripts\package-release.ps1 [-Version 0.10.0]

param(
    [string]$Version = "0.10.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$dll = "src\Dizzy.FirewoodBundle\bin\Release\Dizzy.FirewoodBundle.dll"
# Always rebuild so an old DLL is never zipped.
Write-Host "Building Dizzy.FirewoodBundle Release..."
dotnet build src\Dizzy.FirewoodBundle\Dizzy.FirewoodBundle.csproj -c Release -p:DeployOnBuild=false
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

if (-not (Test-Path $dll)) {
    throw "Missing $dll - build failed."
}

$staging = "dist\Dizzy.FirewoodBundle-$Version"
$pluginDir = "$staging\Dizzy.FirewoodBundle"
$zipPath = "dist\Dizzy.FirewoodBundle-$Version.zip"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll $pluginDir -Force
Copy-Item "LICENSE" $pluginDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path $pluginDir -DestinationPath $zipPath -Force
Write-Host "Packed $zipPath"
