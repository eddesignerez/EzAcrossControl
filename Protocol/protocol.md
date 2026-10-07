# EZ Across Control - Communication Protocol

All communication between the Windows Host and the Android Client occurs over WebSockets using JSON format.

## Protocol Versioning
The current protocol version is `1`. Both the `HELLO` and `WELCOME` messages exchange this version.
Clients or hosts should cleanly disconnect if they do not support the received version.

## Message Envelope
Every message must be wrapped in a standard envelope containing routing, sequence, and timing information:

```json
{
  "Type": "MESSAGE_TYPE",
  "ProtocolVersion": 1,
  "Sequence": 12345,
  "Timestamp": 1720000000000,
  "Payload": { ... }
}
```
- **Type**: Defines the schema of the `Payload`.
- **ProtocolVersion**: Always `1` for this version.
- **Sequence**: A monotonically increasing integer starting from 1 for each session.
- **Timestamp**: Standard Unix time in milliseconds.
- **Payload**: An object specific to the `Type`.

## Supported Messages (Phase 1 & 2)

### Handshake

#### HELLO
Sent by the Client upon connection to introduce itself to the server.
```json
{
  "Type": "HELLO",
  "ProtocolVersion": 1,
  "Sequence": 1,
  "Timestamp": 1695034800000,
  "Payload": {
    "DeviceName": "Android Tablet X"
  }
}
```

#### WELCOME
Sent by the Server in response to a `HELLO` message.
```json
{
  "Type": "WELCOME",
  "ProtocolVersion": 1,
  "Sequence": 1,
  "Timestamp": 1695034800000,
  "Payload": {}
}
```

#### PING / PONG
Used for latency measurement.
```json
{
  "Type": "PING", // or PONG
  "ProtocolVersion": 1,
  "Sequence": 2,
  "Timestamp": 1695034800000,
  "Payload": {
    "OriginalTimestamp": 1695034800000
  }
}
```

### Input Injection (Phase 2.3+)

Input injection uses normalized coordinates `[0.0, 1.0]` for mouse positions. The normalization is relative to the *entire virtual screen bounding box* of the Host.

#### INPUT_MOUSE_MOVE
```json
{
  "Type": "INPUT_MOUSE_MOVE",
  "ProtocolVersion": 1,
  "Sequence": 3,
  "Timestamp": 1695034800050,
  "Payload": {
    "X": 1920,
    "Y": 1080,
    "DeltaX": 5,
    "DeltaY": -2,
    "NormalizedX": 0.5,
    "NormalizedY": 0.5,
    "IsInjected": false
  }
}
```

#### INPUT_MOUSE_BUTTON
```json
{
  "Type": "INPUT_MOUSE_BUTTON",
  "ProtocolVersion": 1,
  "Sequence": 4,
  "Timestamp": 1695034800100,
  "Payload": {
    "Button": "Left",     // Left, Right, Middle, X1, X2
    "Action": "Down",     // Down, Up
    "X": 1920,
    "Y": 1080,
    "IsInjected": false
  }
}
```

#### INPUT_MOUSE_WHEEL
```json
{
  "Type": "INPUT_MOUSE_WHEEL",
  "ProtocolVersion": 1,
  "Sequence": 5,
  "Timestamp": 1695034800200,
  "Payload": {
    "Axis": "Vertical",   // Vertical, Horizontal
    "Delta": 120,
    "X": 1920,
    "Y": 1080
  }
}
```

#### INPUT_KEY_DOWN / INPUT_KEY_UP
```json
{
  "Type": "INPUT_KEY_DOWN", // or INPUT_KEY_UP
  "ProtocolVersion": 1,
  "Sequence": 6,
  "Timestamp": 1695034800300,
  "Payload": {
    "VirtualKeyCode": 65, // Example: 'A'
    "ScanCode": 30,
    "IsExtended": false,
    "IsInjected": false,
    "Modifiers": {
      "Ctrl": false,
      "Shift": true,
      "Alt": false,
      "Win": false
    }
  }
}
```

### Handoff Management

#### INPUT_HANDOFF_BEGIN
Sent when the cursor intends to transfer to the client.
```json
{
  "Type": "INPUT_HANDOFF_BEGIN",
  "ProtocolVersion": 1,
  "Sequence": 7,
  "Timestamp": 1695034800400,
  "Payload": {
    "Edge": "Right",
    "EntryNormalizedY": 0.5
  }
}
```

#### INPUT_HANDOFF_CANCEL
Sent if the transition was aborted.
```json
{
  "Type": "INPUT_HANDOFF_CANCEL",
  "ProtocolVersion": 1,
  "Sequence": 8,
  "Timestamp": 1695034800500,
  "Payload": {}
}
```

#### INPUT_HANDOFF_END
Sent when the client returns control to the Host.
```json
{
  "Type": "INPUT_HANDOFF_END",
  "ProtocolVersion": 1,
  "Sequence": 9,
  "Timestamp": 1695034800600,
  "Payload": {}
}
```
