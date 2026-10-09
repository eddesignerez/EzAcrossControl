param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$BuildDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$JavaHome = $(if ($env:JAVA_HOME) { $env:JAVA_HOME } else { 'C:\Program Files\Android\Android Studio\jbr' }),
    [string]$AndroidHome = $(if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' })
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $SourceDirectory 'server\src\main'
$manager = Join-Path $source 'java\com\genymobile\scrcpy\control\UhidManager.java'
if (-not (Test-Path $manager) -or (Get-Content -Raw $manager) -notmatch 'id == 1 \? \(short\) 0x03 : BUS_VIRTUAL') {
    throw 'The external-keyboard UHID patch must be applied before building the server.'
}
if (Test-Path $BuildDirectory) { throw 'Use an empty server build directory to avoid stale classes.' }
$sdkTools = Join-Path $AndroidHome 'build-tools\36.0.0'
$androidJar = Join-Path $AndroidHome 'platforms\android-36\android.jar'
$generation = Join-Path $BuildDirectory 'gen'
$classes = Join-Path $BuildDirectory 'classes'
New-Item -ItemType Directory -Force -Path $generation,$classes,(Join-Path $generation 'com\genymobile\scrcpy') | Out-Null
[IO.File]::WriteAllText((Join-Path $generation 'com\genymobile\scrcpy\BuildConfig.java'),
    'package com.genymobile.scrcpy; public final class BuildConfig { public static final boolean DEBUG = false; public static final String VERSION_NAME = "4.1"; }',
    [Text.UTF8Encoding]::new($false))

$aidl = Join-Path $source 'aidl'
& (Join-Path $sdkTools 'aidl.exe') "-o$generation" "-I$aidl" (Join-Path $aidl 'android\content\IOnPrimaryClipChangedListener.aidl')
if ($LASTEXITCODE -ne 0) { throw 'Clipboard AIDL compilation failed.' }
& (Join-Path $sdkTools 'aidl.exe') "-o$generation" "-I$aidl" '-p' (Join-Path $AndroidHome 'platforms\android-36\framework.aidl') (Join-Path $aidl 'android\view\IDisplayWindowListener.aidl')
if ($LASTEXITCODE -ne 0) { throw 'Display AIDL compilation failed.' }

$sources = @(Get-ChildItem (Join-Path $source 'java') -Filter '*.java' -Recurse | ForEach-Object FullName)
$sources += @(Get-ChildItem $generation -Filter '*.java' -Recurse | ForEach-Object FullName)
$sourceList = Join-Path $BuildDirectory 'sources.txt'
$sources | ForEach-Object { '"' + $_.Replace('\','/') + '"' } | Set-Content $sourceList -Encoding utf8
& (Join-Path $JavaHome 'bin\javac.exe') '-encoding' 'UTF-8' '-bootclasspath' $androidJar '-cp' ((Join-Path $sdkTools 'core-lambda-stubs.jar') + ';' + $generation) '-d' $classes '-source' '1.8' '-target' '1.8' "@$sourceList"
if ($LASTEXITCODE -ne 0) { throw 'scrcpy server Java compilation failed.' }

$jar = Join-Path $BuildDirectory 'classes.jar'
& (Join-Path $JavaHome 'bin\jar.exe') 'cf' $jar '-C' $classes '.'
if ($LASTEXITCODE -ne 0) { throw 'scrcpy server JAR creation failed.' }
$dex = Join-Path $BuildDirectory 'server.zip'
& (Join-Path $JavaHome 'bin\java.exe') '-cp' (Join-Path $sdkTools 'lib\d8.jar') 'com.android.tools.r8.D8' '--lib' $androidJar '--min-api' '21' '--output' $dex $jar
if ($LASTEXITCODE -ne 0) { throw 'scrcpy server DEX compilation failed.' }
Copy-Item -LiteralPath $dex -Destination $OutputPath
Write-Host "scrcpy server built: $OutputPath"
