param(
    [string]$SourceDirectory,
    [string]$MingwRoot = 'C:/msys64/mingw64',
    [string]$ResultsDirectory = 'release-output/test-results/native-mouse-capture'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $SourceDirectory) { $SourceDirectory = Join-Path $projectRoot 'third_party/scrcpy-src' }
$compiler = Join-Path $MingwRoot 'bin/gcc.exe'
$configuration = Join-Path $SourceDirectory 'build-ezacross/app'
if (-not (Test-Path -LiteralPath $compiler) -or -not (Test-Path -LiteralPath (Join-Path $configuration 'config.h'))) {
    throw 'Existing native compiler and scrcpy build configuration are required; no dependencies are installed.'
}
if (-not [IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory = Join-Path $projectRoot $ResultsDirectory }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$executable = Join-Path $ResultsDirectory 'capture-regression.exe'
$compilerArguments = @('-DSDL_DECLSPEC=', "-I$SourceDirectory/app/src", "-I$configuration", "-I$MingwRoot/include", (Join-Path $projectRoot 'Tests/NativeMouseCapture/capture_regression.c'), "$SourceDirectory/app/src/mouse_capture.c", '-o', $executable)
$previousPath = $env:Path
try {
    $env:Path = "$MingwRoot/bin;$previousPath"
    & $compiler @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Native capture test compilation failed.' }
    & $executable | Tee-Object -FilePath (Join-Path $ResultsDirectory 'results.jsonl')
    if ($LASTEXITCODE -ne 0) { throw 'Native capture regression failed.' }
} finally { $env:Path = $previousPath }
