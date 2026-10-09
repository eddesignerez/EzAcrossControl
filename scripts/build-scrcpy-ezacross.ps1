param([string]$SourceDirectory, [string]$ServerBuildDirectory)
$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path "$PSScriptRoot\.." | Select-Object -ExpandProperty Path
$ScrcpySrcDir = if ($SourceDirectory) { $SourceDirectory } else { Join-Path $ProjectRoot "third_party\scrcpy-src" }
$PatchFile = Join-Path $ProjectRoot "patches\scrcpy\EZ_ACROSS_PATCH.patch"
$KeyboardPatch = Join-Path $ProjectRoot "patches\scrcpy\EXTERNAL_KEYBOARD_UHID.patch"
$BuildDir = Join-Path $ScrcpySrcDir "build-ezacross-release"
$ServerBuildDirectory = if ($ServerBuildDirectory) { $ServerBuildDirectory } else { Join-Path $ProjectRoot "work\scrcpy-server-build" }
$BinDir = Join-Path $ProjectRoot "third_party\scrcpy-ezacross\bin"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "1. AUDITORIA DO AMBIENTE" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Check MSYS2
$MsysBash = "C:\msys64\usr\bin\bash.exe"
if (-Not (Test-Path $MsysBash)) {
    Write-Host "MSYS2 (C:\msys64) NOT FOUND." -ForegroundColor Yellow
    Write-Host "Attempting to install via winget..."
    
    Write-Host "Please accept the UAC prompt if it appears." -ForegroundColor Yellow
    winget install --exact MSYS2.MSYS2 --accept-source-agreements --accept-package-agreements
    
    if (-Not (Test-Path $MsysBash)) {
        throw "Failed to install MSYS2. Please install manually."
    }
} else {
    Write-Host "MSYS2 FOUND." -ForegroundColor Green
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "2. DEPENDÊNCIAS SCRCPY (MSYS2 pacman)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$Deps = "mingw-w64-x86_64-sdl3 mingw-w64-x86_64-ffmpeg mingw-w64-x86_64-libusb mingw-w64-x86_64-make mingw-w64-x86_64-gcc mingw-w64-x86_64-pkgconf mingw-w64-x86_64-meson mingw-w64-x86_64-ninja"

# Execute pacman in MINGW64 context
$Env:MSYSTEM = "MINGW64"
$Env:CHERE_INVOKING = "1"
& $MsysBash -lc "pacman -S --noconfirm --needed $Deps"
if ($LASTEXITCODE -ne 0) { throw "Failed to install dependencies via pacman." }

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "3. CLONE E CHECKOUT (v4.1)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (-Not (Test-Path $ScrcpySrcDir)) {
    Write-Host "Cloning scrcpy to $ScrcpySrcDir..."
    git clone https://github.com/Genymobile/scrcpy.git $ScrcpySrcDir
}
Set-Location $ScrcpySrcDir
$tagCommit = git rev-parse 'v4.1^{commit}'
$headCommit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $headCommit -ne $tagCommit) { throw 'Use a checkout of the scrcpy v4.1 tag.' }
if (-not (Test-Path $PatchFile) -or -not (Test-Path $KeyboardPatch)) { throw 'Required scrcpy patch missing.' }
git apply --reverse --check $PatchFile 2>$null
$clientApplied = $LASTEXITCODE -eq 0
git apply --reverse --check $KeyboardPatch 2>$null
$keyboardApplied = $LASTEXITCODE -eq 0
if ($clientApplied -ne $keyboardApplied) { throw 'Both scrcpy patches must be applied together.' }
if (-not $clientApplied) {
    if (git status --porcelain) { throw 'Use a clean scrcpy checkout or a detached worktree; existing changes are preserved.' }
    git apply --check $PatchFile
    if ($LASTEXITCODE -ne 0) { throw 'Client patch validation failed.' }
    git apply --check $KeyboardPatch
    if ($LASTEXITCODE -ne 0) { throw 'Keyboard patch validation failed.' }
    git apply $PatchFile
    if ($LASTEXITCODE -ne 0) { throw 'Client patch failed.' }
    git apply $KeyboardPatch
    if ($LASTEXITCODE -ne 0) { throw 'Keyboard patch failed.' }
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "4. VALIDAR E APLICAR PATCH" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

Write-Host "Both scrcpy patches are applied." -ForegroundColor Green

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "5. COMPILAR SERVER COM TECLADO EXTERNO" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$ServerDest = Join-Path $ScrcpySrcDir 'scrcpy-server-ezacross'
& (Join-Path $PSScriptRoot 'build-scrcpy-server.ps1') -SourceDirectory $ScrcpySrcDir -BuildDirectory $ServerBuildDirectory -OutputPath $ServerDest
if (-not (Test-Path $ServerDest)) { throw 'Server build failed.' }

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "6. BUILD DO CLIENTE (Meson/Ninja)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$MsysSrcDir = $ScrcpySrcDir -replace '\\', '/' -replace 'c:', '/c'
$MsysServerDest = $ServerDest -replace '\\', '/' -replace 'c:', '/c'

$BuildCmd = @"
cd '$MsysSrcDir'
meson setup build-ezacross-release -Dprebuilt_server=scrcpy-server-ezacross -Dportable=true --buildtype release
ninja -C build-ezacross-release
"@

Set-Content -Path "build.sh" -Value $BuildCmd
& $MsysBash -lc "cd '$MsysSrcDir'; bash build.sh"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "7. OUTPUT V2" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (-Not (Test-Path $BinDir)) { New-Item -ItemType Directory -Force -Path $BinDir | Out-Null }

Copy-Item "$BuildDir\app\scrcpy.exe" -Destination $BinDir -Force
Copy-Item $ServerDest -Destination "$BinDir\scrcpy-server" -Force

$Dlls = @("SDL3.dll", "avcodec-62.dll", "avformat-62.dll", "avutil-60.dll", "swresample-6.dll", "swscale-9.dll", "libusb-1.0.dll", "libgcc_s_seh-1.dll", "libwinpthread-1.dll", "libstdc++-6.dll")
foreach ($Dll in $Dlls) {
    $DllPath = Join-Path "C:\msys64\mingw64\bin" $Dll
    if (Test-Path $DllPath) {
        Copy-Item $DllPath -Destination $BinDir -Force
    }
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "8. VERSION COMMAND TEST" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

& "$BinDir\scrcpy.exe" --version

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "Output written to: $BinDir" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
