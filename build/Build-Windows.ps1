# Build a Windows folder you can copy and run.
# Requires the .NET Framework 4.8 developer pack (installed with Visual Studio).
# The program itself runs on Windows 10 (1903+) and Windows 11, which already include .NET Framework 4.8.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "LaserGRBL\LaserGRBL.csproj"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe was not found. Install Visual Studio 2022 with the .NET desktop build tools."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild was not found. Install the .NET desktop development workload."
}

Write-Host "Using $msbuild"
& $msbuild $csproj /p:Configuration=$Configuration /p:TargetFrameworkVersion=v4.8 /p:Platform=AnyCPU /v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$built = Join-Path $root "LaserGRBL\bin\$Configuration"
$dest = Join-Path $root "dist\LaserGRBL-Plotter"
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $built "*") $dest -Recurse -Force

$zip = Join-Path $root "dist\LaserGRBL-Plotter-win.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $dest -DestinationPath $zip
Write-Host "Ready: $zip"
Write-Host "Unzip it and run LaserGRBL.exe. Open Grbl - Plotter... and turn plotter mode on."
