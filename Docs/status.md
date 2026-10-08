# Current Status

## Distribution 1.1.0

Installable Windows and signed Android packages, 11 languages and current validation evidence are documented in [Release 1.1.0](release-1.1.0.md).

## Phase 1.5: Configuration, Ports and Design System (Completed)
- **Objective**: Consolidate the base by introducing a unified Design System (Light/Dark/System), centralizing network configurations, and standardizing the UI with the Inter font.
- **Implemented**:
  - Unified Design System implementation (WPF & Compose).
  - Centralized port configuration (default 8765).
  - Theme persistence.

## Phase 1.6: Security & Validation (Completed)
- **Goal**: Validate LAN network rules and basic WebSocket communication.
- **Tasks**:
  - Centralized port configuration (default 8765).
  - Configured Android cleartext traffic specifically for LAN (via `network_security_config.xml`).
  - Fixed Windows Host `HttpListener` bind to avoid Admin elevation (`http://localhost` and specific local IP fallback).
  - Executed automated WebSocket client test (Python).
- **Status**: Completed.
  - Windows built successfully (.NET 8).
  - Android built successfully.
  - Protocol rules and port (8765) verified.
  - **Phase 1.6**: Validação Técnica ✅ (Concluído)

## Phase 2: Input Capture & Network Transport
- [x] Phase 2.1 — Windows Input Capture
- [x] Phase 2.2 — Edge Transition / Cursor Handoff Preparation
- [x] Phase 2.3 — Input Protocol & Network Transport
- [x] Phase 2.3.5 — Physical Device Network Validation
- [x] Phase 2.4 — Android Mouse Control Layer
- [x] Phase 2.5 — Android Keyboard Control Layer
- [ ] Phase 2.6 — Continuous Handoff Refinement

## Phase 3: UHID & V2 Architecture
- [x] Phase 3.0 — UHID Feasibility Prototype
- [x] Phase 3.2.1 — Scrcpy UHID Parity Audit
- [x] Phase V2.1 — Scrcpy Core Integration
- [ ] Phase V2.2 — Dual Transport (USB + Network)
- [ ] Phase V2.3 — Edge Handoff & Robustness

## Documentation Index
- [Architecture](architecture.md)
- [Protocol](../Protocol/protocol.md)
- [Phase 2.2 Edge Transition](phase-2.2-edge-transition.md)
- [Phase 2.3 Input Protocol](phase-2.3-input-protocol.md)
- [Phase 2.4 Android Mouse Control](phase-2.4-android-mouse-control.md)
- [Phase 2.5 Natural Keyboard](phase-2.5-natural-keyboard.md)
- [Phase 3.0 UHID Feasibility](phase-3.0-uhid-feasibility.md)
- [Phase 3.2.1 Scrcpy UHID Parity](phase-3.2.1-scrcpy-uhid-parity.md)
- [V2: Build Scrcpy Windows](v2/build-scrcpy-windows.md)
- [V2: Dual Transport](v2/dual-transport.md)
