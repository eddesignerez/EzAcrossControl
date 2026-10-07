# V2.2: Dual Transport (USB + Network)

## Overview
EZ Across Control V2 utilizes a custom scrcpy 4.1 backend. In Phase V2.2, we introduced a Dual Transport system allowing the user to select their connection mode: Auto, USB, or Network.

## Connection Modes
- **Auto**: Prefer USB if available, otherwise fallback to Network (TCP/IP).
- **USB**: Force USB connection (`--serial <usb-serial>`). Ignore Network even if it's the only available transport.
- **Network**: Force Network connection (`--serial <ip:port>`). Ignore USB even if available.

## Device Identification Consolidation
Since `adb devices -l` lists USB and TCP instances as separate rows but they represent the same physical device, we identify unique devices by grouping them by their `model:` attribute. This allows the system to seamlessly switch or select transports for the same physical Android device.

## Implementation Details
1. **Model**: `AndroidDevice.cs` manages identity linking.
2. **Manager**: `AndroidDeviceManager.cs` orchestrates discovery (`adb devices -l`), groups outputs by model, and provides `ConnectTcpAsync`/`DisconnectTcpAsync` wrappers.
3. **Engine Injection**: `ScrcpyProcessManager.cs`'s `StartAsync` now accepts the `ConnectionMode` and `AndroidDevice` to inject the correct `--serial` argument when launching the `scrcpy.exe` process.

## USB Parity Rules
The network transport must match the functional capabilities of the USB connection, specifically regarding UHID mouse mapping, Japanese composition, and Edge handoff behaviors. Physical validation will determine if Direct LAN optimization is required to solve jitter issues over Wi-Fi.
