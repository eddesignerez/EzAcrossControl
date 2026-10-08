# Third-party components

The Windows distribution includes .NET 8 (MIT), Android Platform Tools (notices distributed with the tools), and an adapted scrcpy runtime (Apache-2.0). scrcpy and its dependencies remain separate executables/libraries; their license terms apply to those components.

- scrcpy upstream source: https://github.com/Genymobile/scrcpy/tree/v4.1
- EZ Across Control modifications: `patches/scrcpy/EZ_ACROSS_PATCH.patch`
- Rebuild procedure: `Docs/v2/build-scrcpy-windows.md`
- MSYS2 runtime libraries, including FFmpeg, SDL3, libusb and their transitive dependencies: https://packages.msys2.org/
- Exact MSYS2 source packages: https://github.com/msys2/MINGW-packages
- .NET source and notices: https://github.com/dotnet/runtime and https://github.com/dotnet/wpf
- Android Platform Tools: https://developer.android.com/tools/releases/platform-tools
- Inter and Fraunces fonts: SIL Open Font License; notices included in the distribution.

The Windows installer includes a `licenses` directory with the upstream notices for the supplied native runtime. The corresponding upstream versions and any changes are listed in the release's dependency manifest and source archive. Android dependencies use AndroidX, Kotlin and OkHttp; their upstream licenses apply independently.

No third-party signing key or authentication credential is included.
