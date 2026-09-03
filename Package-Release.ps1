#
# Builds both the server mod (TheLocksmith.csproj) and the client plugin
# (TheLocksmith.Client\TheLocksmith.Client.csproj), then assembles a single
# distributable zip that mirrors the SPT 4.0.x install layout:
#
#   SPT_Runtime\user\mods\TheLocksmith\...
#   BepInEx\plugins\TheLocksmith.Client\...
#
# Users just extract the zip and drag the two top-level folders (SPT_Runtime,
# BepInEx) into their SPT install root, merging with the existing folders.
#
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "Building server mod..."
dotnet build "$root\TheLocksmith.csproj" -c Debug
if ($LASTEXITCODE -ne 0) { throw "Server mod build failed" }

Write-Host "Building client mod..."
dotnet build "$root\TheLocksmith.Client\TheLocksmith.Client.csproj" -c Debug
if ($LASTEXITCODE -ne 0) { throw "Client mod build failed" }

$distDir = Join-Path $root "dist"
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }

$serverDistDir = Join-Path $distDir "SPT_Runtime\user\mods\TheLocksmith"
$clientDistDir = Join-Path $distDir "BepInEx\plugins\TheLocksmith.Client"
New-Item -ItemType Directory -Path $serverDistDir -Force | Out-Null
New-Item -ItemType Directory -Path $clientDistDir -Force | Out-Null

Copy-Item "$root\bin\Debug\TheLocksmith.dll" $serverDistDir -Force
Copy-Item "$root\assets" (Join-Path $serverDistDir "assets") -Recurse -Force
Copy-Item "$root\config" (Join-Path $serverDistDir "config") -Recurse -Force

Copy-Item "$root\TheLocksmith.Client\bin\Debug\TheLocksmith.Client.dll" $clientDistDir -Force

$zipPath = Join-Path $root "TheLocksmith-Full-$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Package created: $zipPath"
