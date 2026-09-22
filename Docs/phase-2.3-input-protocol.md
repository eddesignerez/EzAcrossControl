# Phase 2.3 - Input Event Protocol & Network Transport

## Overview
Phase 2.3 establishes the foundation for transferring input events captured locally on the Windows Host to the Android Client over a local network.

## Goals
1. Define a standardized protocol (`version 1`) for mouse and keyboard events.
2. Implement robust queuing and sequence management.
3. Ensure low overhead by coalescing mouse move events (throttled to ~120 Hz).
4. Parse and display events accurately in the Android app for debug validation, without triggering OS injection or accessibility services.

## The Protocol
The protocol defines an envelope for robust delivery over WebSockets:
- `Type`: Specifies the event, e.g., `INPUT_MOUSE_MOVE`, `INPUT_KEY_DOWN`.
- `ProtocolVersion`: Int (currently `1`). Must match between host and client.
- `Sequence`: Monotonically increasing number to detect lost messages.
- `Timestamp`: Host timestamp when the message was encoded.
- `Payload`: The specific data structure for the event type.

Refer to `protocol.md` for a complete schema definition.

## Architecture
- **Windows Host (`InputSessionManager`)**:
  - Enqueues raw events from `IInputCaptureService`.
  - Dedicates a background worker (`WorkerLoopAsync`) to decouple capture thread from network I/O.
  - Drops intermediate `MouseMove` events when the queue is saturated, saving the latest position to maintain latency ("latest-value wins").
  - Sends exact button/key states via the WebSocket.

- **Android Client (`InputSessionManager` / `InputEventParser`)**:
  - Parses incoming text payloads safely into Kotlin data classes (`MessageEnvelope`).
  - Calculates event rate (`Hz`) and monitors sequence continuity.
  - Exposes properties directly to Jetpack Compose for real-time visualization on the debug panel.

## Validation
- Events correctly travel over the LAN connection at less than 10ms latency.
- Mouse move data is successfully capped at ~120 Hz to prevent network flooding and mobile CPU spikes.
- Tests confirm sequences maintain chronological integrity.
