# Network Configuration

## Default Settings
- **Default Port**: 8787 (Legacy port 8765 is no longer used by default due to conflicts)
- **Transport**: WebSockets over LAN
- **Routing**: Strictly Local Area Network. No Cloud.

## Restrictions
The following ports are **RESERVED** for other local projects and must **NOT** be used:
- `3000`
- `4000`
- `5000`
- `5173`

## Configuration Location
The port is not hardcoded in the views. It is centralized in:
- **Windows**: `Windows-host/Config.cs`
- **Android**: `Android-client/app/src/main/java/.../Config.kt` (or similar Constants file)
