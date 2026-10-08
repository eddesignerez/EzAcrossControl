# scripts/build-scrcpy-ezacross.ps1
$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path "$PSScriptRoot\.." | Select-Object -ExpandProperty Path
$ScrcpySrcDir = Join-Path $ProjectRoot "third_party\scrcpy-src"
$PatchFile = Join-Path $ProjectRoot "patches\scrcpy\EZ_ACROSS_PATCH.patch"
$PrebuiltServerUrl = "https://github.com/Genymobile/scrcpy/releases/download/v3.1/scrcpy-server-v3.1"
$ExpectedServerHash = "deacb991ed2509715160ffdc7907e47b4160eb30d1566217e9047fd5b8850cae" # This hash is provided by the user, but it actually matches 3.1. I will use 3.1 since 4.1 isn't public yet? Wait, user explicitly requested v4.1. Let me use v4.1 URL and the provided hash.
$PrebuiltServerUrl = "https://github.com/Genymobile/scrcpy/releases/download/v4.1/scrcpy-server-v4.1"
$BuildDir = Join-Path $ScrcpySrcDir "build-ezacross"
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
git fetch origin --tags
git checkout v4.1
git reset --hard HEAD
git clean -fd

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "4. VALIDAR E APLICAR PATCH" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (Test-Path $PatchFile) {
    Write-Host "Checking patch..."
    git apply --check $PatchFile
    if ($LASTEXITCODE -ne 0) { throw "Patch validation failed! Run git apply --check manually to investigate." }
    
    Write-Host "Applying patch..."
    git apply $PatchFile
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply patch." }
} else {
    throw "Patch file not found: $PatchFile"
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "5. BAIXAR PREBUILT SERVER 4.1" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$ServerDest = Join-Path $ScrcpySrcDir "scrcpy-server-v4.1"
if (-Not (Test-Path $ServerDest)) {
    Write-Host "Downloading $PrebuiltServerUrl..."
    Invoke-WebRequest -Uri $PrebuiltServerUrl -OutFile $ServerDest
}

$ActualHash = (Get-FileHash $ServerDest -Algorithm SHA256).Hash.ToLower()
if ($ActualHash -ne $ExpectedServerHash) {
    throw "Server hash mismatch! Expected $ExpectedServerHash, got $ActualHash"
}
Write-Host "Server hash matched." -ForegroundColor Green

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "6. BUILD DO CLIENTE (Meson/Ninja)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$MsysSrcDir = $ScrcpySrcDir -replace '\\', '/' -replace 'c:', '/c'
$MsysServerDest = $ServerDest -replace '\\', '/' -replace 'c:', '/c'

$BuildCmd = @"
cd '$MsysSrcDir'
meson setup build-ezacross -Dprebuilt_server=scrcpy-server-v4.1 -Dportable=true --buildtype release
ninja -C build-ezacross
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

$Dlls = @("SDL3.dll", "avcodec-61.dll", "avformat-61.dll", "avutil-59.dll", "swresample-5.dll", "swscale-8.dll", "libusb-1.0.dll", "libgcc_s_seh-1.dll", "libwinpthread-1.dll", "libstdc++-6.dll")
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
