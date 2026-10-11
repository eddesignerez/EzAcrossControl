# EZ Across Control - Communication Protocol

EZ Across Control uses JSON over LAN WebSockets. The default, configurable port is `8765`; ports 3000, 4000, 5000 and 5173 are reserved. A read-only `GET /readiness?device=<Build.MODEL>` query uses the same Host IP and port and never starts control.

## Version 2 and compatibility

The current protocol version is `2`. Both endpoints must use v2; a v1 client or Host is rejected with an upgrade message. Upgrade Windows Host and Android companion together.

v2 authenticates one approved Android installation. It is not transport encryption: the WebSocket remains `ws://` on the trusted LAN and no claim of confidentiality is made.

## Envelope

```json
{"Type":"MESSAGE_TYPE","ProtocolVersion":2,"Sequence":1,"Timestamp":1720000000000,"Payload":{}}
```

`Type` defines `Payload`; `Sequence` and `Timestamp` are session metadata. Field casing is PascalCase on Host messages; the Android client accepts both PascalCase and camelCase for compatibility inside v2.

## Pairing handshake

1. The Host accepts a provisional socket and sends `PAIR_CHALLENGE` with a fresh random `Nonce`.
2. Android replies with `HELLO`: its stable installation UUID, Android-Keystore public key, ECDSA signature over `Nonce|InstallationId`, device name, requested ADB mode, and a six-digit verification code derived from that public key.
3. For an unknown key (including an approved device with a rotated key), Windows shows the device and code. The user must approve only if the same code is visible on Android. Windows retains the public key only; the Android private key never leaves Android Keystore.
4. For a known key, the Host verifies the challenge signature and sends `WELCOME`. Only then does it replace a previous authenticated companion and start any ADB discovery. Reconnect never captures input automatically.

```json
{"Type":"PAIR_CHALLENGE","ProtocolVersion":2,"Payload":{"Nonce":"base64-random-32-bytes"}}
```

```json
{"Type":"HELLO","ProtocolVersion":2,"Payload":{"DeviceName":"Android Tablet X","ConnectionMode":"Auto","InstallationId":"uuid","PublicKey":"base64-x509-spki","Signature":"base64-ecdsa","PairingCode":"123456"}}
```

`ConnectionMode` is `Auto`, `Wi-Fi`, or `USB`; it selects the Host's ADB/scrcpy input transport. The companion WebSocket is always LAN. A rejected pairing closes with policy violation and must not displace the currently authenticated companion.

```json
{"Type":"WELCOME","ProtocolVersion":2,"Payload":{}}
```

## Session messages

All post-handshake messages require protocol v2 and the active authenticated socket.

```json
{"Type":"PING","ProtocolVersion":2,"Payload":{"Timestamp":1720000000000}}
{"Type":"PONG","ProtocolVersion":2,"Payload":{"Timestamp":1720000000000}}
{"Type":"SESSION_STATUS","ProtocolVersion":2,"Payload":{"ControlTransport":"Wi-Fi","EngineState":"Ready","ControlEnabled":true}}
{"Type":"CONTROL_STOP","ProtocolVersion":2,"Payload":{}}
```

`ControlTransport` is `USB`, `Wi-Fi`, or `null`. `CONTROL_STOP` releases control and blocks capture until a successful authenticated reconnect; it does not alter Android Accessibility or debugging settings.

## Input and handoff

Input messages are sent only during the active session: `INPUT_MOUSE_MOVE`, `INPUT_MOUSE_BUTTON`, `INPUT_MOUSE_WHEEL`, `INPUT_TEXT_COMMIT`, `INPUT_KEY_DOWN`, and `INPUT_KEY_UP`. Normalized mouse coordinates remain `[0,1]` across the Host virtual screen.

```json
{"Type":"INPUT_HANDOFF_BEGIN","ProtocolVersion":2,"Payload":{"Edge":"Right","SessionId":7,"EntryNormalizedY":0.25,"ClientTxTimestamp":0}}
```

`EntryNormalizedY` means the point along the crossed edge: vertical coordinate for Left/Right, horizontal coordinate for Top/Bottom. The Android pointer enters at the corresponding proportional position. `RETURN_TO_WINDOWS` includes the same `SessionId`; the Host keeps existing drag safety and edge rearm behavior. `INPUT_HANDOFF_END` and `INPUT_HANDOFF_CANCEL` terminate the active handoff.

## Bounds and resilience

Host WebSocket messages are assembled across fragments and rejected when binary or larger than 64 KiB. Each authenticated session owns bounded outbound queues (64 priority and 256 standard messages); a stale session cannot send into a replacement session. Android reconnects after loss/sleep with capped exponential delays (1, 2, 4, 8, 16, 30 seconds, up to eight tries), then requires a manual retry.
