# Network Configuration

## Default Settings
- **Default Port**: 8765. The previous development build used 8787; an existing Android preference can still contain that value. Enter the same port in both applications.
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
