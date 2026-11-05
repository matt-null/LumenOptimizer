<#
  Builds the portable app (and the installer, if Inno Setup is installed).
  Run from PowerShell in this folder:   .\build.ps1
  Output:  dist\portable\LumenOptimizer.exe
           dist\LumenOptimizer-portable.zip
           dist\LumenOptimizer-Setup.exe   (only if Inno Setup 6 is installed)
#>
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$out  = Join-Path $dist "portable"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

dotnet publish "$root\src\LumenOptimizer.UI\LumenOptimizer.UI.csproj" `
    -c $Configuration -r win-x64 -p:Platform=x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$zip = Join-Path $dist "LumenOptimizer-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out\*" -DestinationPath $zip
Write-Host "Portable build: $out"
Write-Host "Portable zip:   $zip"

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc "$root\installer\LumenOptimizer.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (exit $LASTEXITCODE)" }
    Write-Host "Installer:      $dist\LumenOptimizer-Setup.exe"
} else {
    Write-Host "Inno Setup 6 not found - skipped the installer. Get it free from https://jrsoftware.org/isdl.php"
}
