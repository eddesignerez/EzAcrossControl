$ErrorActionPreference = "Stop"

$sdkPath = "$env:LOCALAPPDATA\Android\Sdk"
$platforms = Get-ChildItem "$sdkPath\platforms\android-*" | Sort-Object Name -Descending | Select-Object -First 1
$buildTools = Get-ChildItem "$sdkPath\build-tools" | Sort-Object Name -Descending | Select-Object -First 1

$androidJar = "$($platforms.FullName)\android.jar"
$d8 = "$($buildTools.FullName)\d8.bat"

if (-not (Test-Path $androidJar)) { throw "android.jar not found at $androidJar" }
if (-not (Test-Path $d8)) { throw "d8 not found at $d8" }

$srcDir = "src"
$outDir = "build"

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "Compiling Java files..."
$javaFiles = Get-ChildItem -Path $srcDir -Recurse -Filter "*.java" | Select-Object -ExpandProperty FullName
if (-not $javaFiles) { throw "No java files found" }

javac -source 8 -target 8 -bootclasspath $androidJar -d $outDir $javaFiles

Write-Host "Running d8..."
$classFiles = Get-ChildItem -Path $outDir -Recurse -Filter "*.class" | Select-Object -ExpandProperty FullName
& $d8 --output $outDir --lib $androidJar $classFiles

Write-Host "Packaging server.jar..."
if (Test-Path "server.jar") { Remove-Item "server.jar" }
# Ensure we package just classes.dex at the root of the archive
Set-Location $outDir
jar cvf ..\server.jar classes.dex
Set-Location ..

Write-Host "Done! server.jar is ready."
