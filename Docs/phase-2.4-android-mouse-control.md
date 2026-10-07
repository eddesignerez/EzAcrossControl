# Phase 2.4 - Android Mouse Control Layer

## Overview
This phase introduces the core Android-side control layer, taking the mouse events received via WebSocket and executing them on the device. It utilizes Android's `AccessibilityService` to render a virtual cursor overlay and execute gestures (taps and scrolls) without requiring root privileges.

## Architecture

The control layer is designed around the `AndroidControlManager`, which acts as the orchestrator bridging network events from the `InputSessionManager` to the `EZAcrossAccessibilityService`.

### Key Components

- **`EZAcrossAccessibilityService`**: A required Android Service that must be manually enabled by the user. It provides the privileges to draw over other apps (`TYPE_ACCESSIBILITY_OVERLAY`) and dispatch gestures.
- **`AndroidControlManager`**: The singleton state machine that manages the connection and control states (`Disabled`, `Ready`, `Controlling`, `Disconnected`). It reacts to Handoff events and delegates coordinate mapping and gesture execution.
- **`CoordinateMapper`**: Translates normalized coordinates (0.0 to 1.0) received from the Windows Host into absolute pixel coordinates on the Android display, respecting bounds and screen dimensions.
- **`CursorOverlayManager`**: Draws a lightweight vector drawable (`ic_cursor.xml`) as a floating overlay. It uses `WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY` to ensure it can display over everything while ignoring touch events.
- **`MouseActionExecutor`**: Translates discrete mouse button/wheel events into Android `dispatchGesture` calls. For example, a left click translates into a fast `StrokeDescription` tap.

### UI & Lifecycle
The main UI now includes a "Remote Control" section that displays the current Accessibility status and provides a quick link to the Android Settings to enable the service. A "Stop Control" button is also provided as a manual escape hatch to terminate remote input locally.

## Limitations
- **Long Press / Drag**: A robust hold gesture requires specific timing and ongoing path dispatching, which introduces complexity and potential race conditions with high-frequency WebSocket updates. For Phase 2.4, only basic clicks and vertical scrolling are implemented.
- **Right/Middle Click**: Android has no native equivalent for these actions in an accessibility context; they are logged but ignored.
- **Manual Enabling**: Accessibility Services cannot be activated silently; the user must manually navigate to settings to enable the service.

## Physical Validation Procedure
To validate this phase:
1. Build and install the Android APK on a physical tablet.
2. Launch the app and tap "Open Settings" to enable `EZAcrossAccessibilityService`.
3. Start the Windows Host and connect the tablet.
4. Enable "Input Streaming Test" on the Windows side.
5. Move the mouse to observe the virtual cursor overlay tracking.
6. Test Left Clicks on Android UI elements.
7. Test Vertical Scrolling with the mouse wheel.
8. Test Handoff logic (if enabled).
