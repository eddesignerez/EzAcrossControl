# scripts/clean-scrcpy-build.ps1
$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path "$PSScriptRoot\.." | Select-Object -ExpandProperty Path
$ScrcpyBuildDir = Join-Path $ProjectRoot "third_party\scrcpy-src\build-ezacross"
$ScrcpyBinDir = Join-Path $ProjectRoot "third_party\scrcpy-ezacross"

Write-Host "Cleaning EZ Across scrcpy build artifacts..." -ForegroundColor Cyan

if (Test-Path $ScrcpyBuildDir) {
    Write-Host "Removing $ScrcpyBuildDir..."
    Remove-Item -Recurse -Force $ScrcpyBuildDir
} else {
    Write-Host "Build dir not found, skipping: $ScrcpyBuildDir"
}

if (Test-Path $ScrcpyBinDir) {
    Write-Host "Removing $ScrcpyBinDir..."
    Remove-Item -Recurse -Force $ScrcpyBinDir
} else {
    Write-Host "Bin dir not found, skipping: $ScrcpyBinDir"
}

Write-Host "Clean complete." -ForegroundColor Green
