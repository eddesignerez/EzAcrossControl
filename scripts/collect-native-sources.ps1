param([string]$MingwRoot = 'C:\msys64\mingw64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root '.publish-staging/windows/scrcpy'
$sourceDir = Join-Path $root 'work/native-sources'
New-Item -ItemType Directory -Force $sourceDir | Out-Null
$msys = Split-Path $MingwRoot -Parent
$dlls = Get-ChildItem -LiteralPath $runtime -Filter '*.dll'
$packages = @()
foreach ($dir in Get-ChildItem -LiteralPath (Join-Path $msys 'var/lib/pacman/local') -Directory) {
    $files = Get-Content -LiteralPath (Join-Path $dir.FullName 'files')
    $owned = @($dlls | Where-Object { $files -contains "mingw64/bin/$($_.Name)" } | ForEach-Object Name)
    if ($owned.Count -eq 0) { continue }
    $desc = Get-Content -Raw -LiteralPath (Join-Path $dir.FullName 'desc')
    $name = [regex]::Match($desc, '%NAME%\s+([^\r\n]+)').Groups[1].Value
    $base = [regex]::Match($desc, '%BASE%\s+([^\r\n]+)').Groups[1].Value
    $version = [regex]::Match($desc, '%VERSION%\s+([^\r\n]+)').Groups[1].Value
    $license = [regex]::Match($desc, '%LICENSE%\s+([^%]+)').Groups[1].Value.Trim()
    $filename = "$base-$version.src.tar.zst"
    $url = "https://repo.msys2.org/mingw/sources/$filename"
    $destination = Join-Path $sourceDir $filename
    if (-not (Test-Path $destination)) {
        try { Invoke-WebRequest -Uri $url -OutFile $destination }
        catch {
            # Older MSYS2 source packages use gzip.
            $filename = "$base-$version.src.tar.gz"
            $url = "https://repo.msys2.org/mingw/sources/$filename"
            $destination = Join-Path $sourceDir $filename
            Invoke-WebRequest -Uri $url -OutFile $destination
        }
    }
    $packages += [ordered]@{name=$name;version=$version;license=$license;libraries=$owned;source=$filename;upstreamSource=$url;sourceSha256=(Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant()}
    Write-Host "Source acquired: $name $version"
}
$scrcpyRoot = Join-Path $root 'third_party/scrcpy-src'
$commit = & git -C $scrcpyRoot rev-parse HEAD
& git -C $scrcpyRoot archive --format=tar -o (Join-Path $sourceDir 'scrcpy-upstream-source.tar') HEAD
if ($LASTEXITCODE -ne 0) { throw 'scrcpy source archive failed' }
# Include the complete actual modifications, rather than assuming an older patch describes this binary.
& git -C $scrcpyRoot diff --binary ("--output=" + (Join-Path $sourceDir 'scrcpy-distribution.patch')) HEAD
if ($LASTEXITCODE -ne 0) { throw 'scrcpy patch archive failed' }
Copy-Item -LiteralPath (Join-Path $root 'scripts/build-scrcpy-ezacross.ps1') -Destination $sourceDir
$server = Get-FileHash -LiteralPath (Join-Path $runtime 'scrcpy-server')
$manifest = [ordered]@{scrcpyCommit=$commit;scrcpyServerSha256=$server.Hash.ToLowerInvariant();packages=$packages}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $sourceDir 'native-dependencies.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $sourceDir 'native-dependencies.json') -Destination (Join-Path $root 'release-output/native-dependencies.json')
Compress-Archive -LiteralPath $sourceDir -DestinationPath (Join-Path $root 'release-output/EZAcrossControl-1.1.0-native-sources.zip') -CompressionLevel Fastest -Force
