# EZ Across Control

EZ Across Control is an application that allows a Windows PC to control an Android tablet over a local network using a shared mouse and keyboard.

- Establish local LAN network connection (WebSocket).
- Automatic UI theming.
- Windows Host global input capture (Phase 2.1 completed - Mouse/Keyboard hooks implemented locally).
- Remote control of Android from Windows (Pending).

## Development Roadmap

- [x] **Phase 1**: Connection Initialization and UI Setup
  - [x] Host/Client WebSocket Connection.
  - [x] UI System and Theme setup.
- [x] Phase 2.1: Windows Input Capture (Hooks)
- [x] Phase 2.2: Edge Transition / Cursor Handoff Preparation
- [x] Phase 2.3: Input Event Protocol & Network Transport
- [ ] Phase 2.3.5: Physical Device Network Validation (MANUAL REQUIRED)

### Phase 3: Android Execution & Remote Control)
- [ ] **Phase 3**: Client Input Injection (Android Accessibility)

## Status
Currently, the basic structure, design system, and WebSocket communication over the local network have been implemented (Phase 1.6). Phase 2.1 (Windows Input Capture) has also been completed.
Validation is passing.

## Architecture
- **Windows Host**: A WPF application providing a WebSocket server (Requires .NET 8 SDK).

Mouse and keyboard control, clipboard syncing, and accessibility features are planned for future phases.
