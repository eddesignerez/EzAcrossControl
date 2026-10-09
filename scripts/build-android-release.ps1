param([string]$JavaHome = $env:JAVA_HOME, [string]$AndroidHome = $env:ANDROID_HOME)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$private = Join-Path $root '.release-private'
New-Item -ItemType Directory -Force $private | Out-Null
$store = Join-Path $private 'android-release.jks'
$credential = Join-Path $private 'android-signing.json'
if (-not (Test-Path $credential)) {
    $random = [byte[]]::new(36)
    [Security.Cryptography.RandomNumberGenerator]::Fill($random)
    @{ Password = [Convert]::ToBase64String($random) } | ConvertTo-Json | Set-Content -LiteralPath $credential
    & icacls.exe $private /inheritance:r /grant:r "$($env:USERNAME):(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict signing key directory' }
}
$password = (Get-Content -Raw -LiteralPath $credential | ConvertFrom-Json).Password
$env:JAVA_HOME = $JavaHome
$env:ANDROID_HOME = $AndroidHome
$env:EZAC_RELEASE_STORE = $store
$env:EZAC_RELEASE_PASSWORD = $password
try {
    if (-not (Test-Path $store)) {
        & (Join-Path $JavaHome 'bin/keytool.exe') -genkeypair -keystore $store -storepass:env EZAC_RELEASE_PASSWORD -keypass:env EZAC_RELEASE_PASSWORD -alias ezacrosscontrol -keyalg RSA -keysize 4096 -validity 10000 -dname 'CN=EZ Across Control, O=ElementZero' 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not generate Android signing key' }
    }
    Push-Location (Join-Path $root 'Android-client')
    try { & .\gradlew.bat :app:assembleRelease --console=plain --no-configuration-cache; if ($LASTEXITCODE -ne 0) { throw 'Android release build failed' } }
    finally { Pop-Location }
    $output = Join-Path $root 'release-output'
    New-Item -ItemType Directory -Force $output | Out-Null
    $version = [regex]::Match((Get-Content -Raw (Join-Path $root 'Android-client/app/build.gradle.kts')), 'versionName\s*=\s*"([^"]+)"').Groups[1].Value
    if (-not $version) { throw 'Android version name missing' }
    Copy-Item -LiteralPath (Join-Path $root 'Android-client/app/build/outputs/apk/release/app-release.apk') -Destination (Join-Path $output "EZAcrossControl-$version-android.apk")
} finally {
    Remove-Item Env:EZAC_RELEASE_PASSWORD,Env:EZAC_RELEASE_STORE -ErrorAction SilentlyContinue
    $password = $null
}
