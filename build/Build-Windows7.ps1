# Extra Windows 7 build. Does not replace dist\LaserGRBL-Plotter-win.zip.
# Targets .NET Framework 4.0 so startup does not call the Windows 10 DPI APIs that crash a 4.8-targeted exe on Windows 7.
# The machine still needs .NET Framework 4.0 or any newer 4.x (4.8 is fine).
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

$pkgRoot = Join-Path $env:TEMP "net40-refs"
New-Item -ItemType Directory -Force -Path $pkgRoot | Out-Null
$nupkg = Join-Path $pkgRoot "refs.zip"
Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net40/1.0.3" -OutFile $nupkg
$pkg = Join-Path $pkgRoot "pkg"
if (Test-Path $pkg) { Remove-Item $pkg -Recurse -Force }
Expand-Archive -Path $nupkg -DestinationPath $pkg -Force
$targets = Join-Path $pkg "build\Microsoft.NETFramework.ReferenceAssemblies.net40.targets"
if (-not (Test-Path $targets)) { throw "Reference assemblies were not unpacked: $targets" }

Write-Host "Using $msbuild"
& $msbuild $csproj `
    /p:Configuration=$Configuration `
    /p:Platform=AnyCPU `
    /p:TargetFrameworkVersion=v4.0 `
    /p:OutputPath=bin\Release-win7\ `
    /p:IntermediateOutputPath=obj\Release-win7\ `
    /p:Win7Net40Targets="$targets" `
    /v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$built = Join-Path $root "LaserGRBL\bin\Release-win7"
$dest = Join-Path $root "dist\LaserGRBL-Plotter-win7"
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $built "*") $dest -Recurse -Force

$zip = Join-Path $root "dist\LaserGRBL-Plotter-win7.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $dest -DestinationPath $zip
Write-Host "Ready: $zip"
Write-Host "Unzip it on Windows 7 and run LaserGRBL.exe. The Windows 10/11 zip is unchanged."
