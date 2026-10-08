param(
    [string]$InnoCompiler = $env:INNO_COMPILER,
    [string]$MingwRoot = 'C:\msys64\mingw64',
    [string]$AdbDirectory = 'C:\Android\platform-tools'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bundle = Join-Path $root '.publish-staging/windows'
$output = Join-Path $root 'release-output'
if (Test-Path $bundle) { throw 'Use an empty Windows staging directory to avoid stale release files.' }
New-Item -ItemType Directory -Force $bundle,$output | Out-Null
& dotnet publish (Join-Path $root 'Windows-host/WindowsHost.csproj') -c Release -r win-x64 --self-contained true -p:Version=1.1.0 -p:DebugType=None -p:DebugSymbols=false -o $bundle --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed' }
$runtime = Join-Path $bundle 'scrcpy'
New-Item -ItemType Directory -Force $runtime | Out-Null
$native = Join-Path $root 'third_party/scrcpy-ezacross/bin'
Copy-Item -LiteralPath (Join-Path $native 'scrcpy.exe'),(Join-Path $native 'scrcpy-server') -Destination $runtime
foreach ($file in @('adb.exe','AdbWinApi.dll','AdbWinUsbApi.dll')) { Copy-Item -LiteralPath (Join-Path $AdbDirectory $file) -Destination $runtime }
$queue = [Collections.Generic.Queue[string]]::new()
$queue.Enqueue((Join-Path $runtime 'scrcpy.exe'))
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$objdump = Join-Path $MingwRoot 'bin/objdump.exe'
while ($queue.Count -gt 0) {
    $file = $queue.Dequeue()
    foreach ($line in (& $objdump -p $file)) {
        if ($line -notmatch 'DLL Name:\s*(.+)$') { continue }
        $name = $Matches[1].Trim()
        if (-not $visited.Add($name)) { continue }
        $source = Join-Path $MingwRoot "bin/$name"
        if (Test-Path $source) {
            Copy-Item -LiteralPath $source -Destination $runtime
            $queue.Enqueue((Join-Path $runtime $name))
        } elseif ($name -notmatch '^(api-ms-|ext-ms-)' -and -not (Test-Path (Join-Path $env:WINDIR "System32/$name"))) {
            throw "Missing native dependency: $name"
        }
    }
}
$licenses = Join-Path $bundle 'licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null
Copy-Item -LiteralPath (Join-Path $AdbDirectory 'NOTICE.txt') -Destination (Join-Path $licenses 'Android-platform-tools-NOTICE.txt')
Copy-Item -LiteralPath (Join-Path $root 'third_party/scrcpy-src/LICENSE') -Destination (Join-Path $licenses 'scrcpy-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $root 'Windows-host/Fonts/LICENSE.txt') -Destination (Join-Path $licenses 'Inter-OFL.txt')
Copy-Item -LiteralPath (Join-Path $root 'Windows-host/Fonts/Fraunces-OFL.txt') -Destination (Join-Path $licenses 'Fraunces-OFL.txt')
# Preserve the exact installed distribution's license notices, including transitive FFmpeg libraries.
Copy-Item -LiteralPath (Join-Path $MingwRoot 'share/licenses') -Destination (Join-Path $licenses 'MSYS2') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $bundle
Copy-Item -LiteralPath (Join-Path $root 'patches/scrcpy/EZ_ACROSS_PATCH.patch') -Destination (Join-Path $licenses 'EZ_ACROSS_PATCH.patch')
$portSource = Get-Content -Raw (Join-Path $root 'Windows-host/Config.cs')
if ($portSource -notmatch 'DefaultPort\s*=\s*(\d+)') { throw 'Default port missing' }
$port = $Matches[1]
if (-not (Test-Path $InnoCompiler)) { throw 'Set INNO_COMPILER to the official ISCC.exe path' }
& $InnoCompiler "/DBundle=$bundle" "/DOutput=$output" "/DPort=$port" (Join-Path $root 'packaging/windows-installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Compress-Archive -LiteralPath $bundle -DestinationPath (Join-Path $output 'EZAcrossControl-1.1.0-windows-x64-portable.zip') -CompressionLevel Optimal
Write-Host "Native dependency closure: $($visited.Count) imports verified"
