# Architecture

## Overview
EZ Across Control is designed to allow seamless control of an Android tablet from a Windows PC over a local network.

## Components
1. **Windows Host**: A .NET 8 WPF application. It acts as the server, opening a WebSocket server and waiting for connections from the Android client. Once connected, it will capture local mouse/keyboard events and send them over the network (Phase 2+).
2. **Android Client**: A Kotlin Android application. It acts as the client, connecting to the Windows Host's IP and port via WebSockets.
3. **InputSessionManager (Android)**: Receives the events, parsing coordinates and clicks.
4. **AndroidControlManager (Android)**: Acts as a state machine (`Disabled`, `Ready`, `Controlling`) to manage the remote control session and dispatch events to the overlay and executor.
5. **EZAcrossAccessibilityService (Android)**: An `AccessibilityService` that draws a floating cursor (`CursorOverlayManager`) and uses `dispatchGesture()` (`MouseActionExecutor`) to inject clicks and scrolls into the UI without root privileges.
6. **Input Capture Layer (Windows)**: A decoupled service (`WindowsInputCaptureService`) using Win32 low-level hooks (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) to observe raw input events (mouse, keyboard, scroll, modifiers) globally.
7. **Edge Transition Layer**: `EdgeTransitionService` detects when the cursor reaches external monitor edges (`MonitorGeometry`) and manages the State Machine (Idle -> Candidate -> Armed) for transition intent, without coupling the user interface.
8. **Protocol**: The JSON-based WebSocket messaging protocol that ensures decoupling between the platforms.
9. **Design System & Theming**: Both platforms implement a unified design system with Light, Dark, and System modes. Themes are persisted locally (DataStore on Android, App Settings on Windows). Interfaces are built using semantic tokens rather than hardcoded HEX colors.

## Input Session Management
- **Rate-Limiting & Coalescing**: Throttles high-frequency mouse movement events to ~120Hz to prevent network congestion and payload buildup.
- **Sequence Tagging**: Assigns incremental sequence numbers to ensure ordered event processing and detect dropped or out-of-order packets.
- **Payload Packaging**: Wraps raw input events into standardized JSON Message Envelopes containing metadata, timestamps, and action payloads for reliable transport.
- **Session Lifecycle**: Manages active control handshakes, cursor capture transitions, and graceful disconnection or fallback when edge boundaries are exited.

## Protocol Details
- **Transport Format**: Lightweight JSON serialization over WebSocket frames.
- **Message Envelope**: Every message consists of a structured envelope containing:
  - `type`: Identifies the event category (e.g., `MOUSE_MOVE`, `KEY_DOWN`, `SCROLL`, `SESSION_CONTROL`).
  - `sequence`: Monotonically increasing integer for ordering and acknowledgment.
  - `timestamp`: High-precision timestamp for latency tracking and synchronization.
  - `payload`: Specific data object corresponding to the event type.
- **Decoupling**: Ensures that neither the Windows host nor the Android client shares direct platform-specific input dependencies, allowing extensible command additions in future phases.

## Networking
- **Transport**: WebSockets over LAN.
- **Port**: Default is 8765, centrally configurable in `Config.cs` (Windows) and `Constants.kt` (Android).
- **Why WebSockets?**: Provides a full-duplex communication channel over a single TCP connection, which is ideal for real-time input event streaming and low latency.
