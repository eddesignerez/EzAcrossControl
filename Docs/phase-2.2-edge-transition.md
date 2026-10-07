# Phase 2.2 - Edge Transition / Cursor Handoff Preparation

## Overview
This phase introduces the logical preparation for transitioning the cursor from the Windows Host to the Android Client when the user hits a predefined screen edge. It strictly handles the intent detection and state transitions locally on Windows, avoiding any network transmission, clipboard transfer, or actual cursor blocking at this stage.

## Architecture

The Edge Transition logic is cleanly decoupled in `Windows-host/Input/Edge/`:
- `ScreenEdge`: Enum representing boundaries (`Left`, `Right`, `Top`, `Bottom`, `Disabled`).
- `EdgeTransitionState`: Enum for the lifecycle (`Disabled`, `Idle`, `Candidate`, `Armed`, `Cancelled`).
- `EdgeTransitionOptions`: Configurations like active edge, threshold, and delay.
- `MonitorGeometry`: A helper class that abstracts Win32 P/Invoke calls (`EnumDisplayMonitors`, `MonitorFromPoint`) to determine monitor bounds.
- `EdgeTransitionService`: The core state machine that subscribes to raw mouse events from `IInputCaptureService`.

### State Machine Lifecycle
1. **Idle**: The cursor is freely moving within the screen.
2. **Candidate**: The cursor enters the defined `EdgeThresholdPixels` near the `ActiveEdge`, and its movement intent is towards that edge. A timer (`EdgeActivationDelayMs`) starts.
3. **Armed**: The cursor remains in the threshold zone for the duration of the delay. The system is ready to hand off to the Android device. (In future phases, this state will trigger the cursor lock and network transition).
4. **Cancelled**: If the cursor moves out of the threshold zone before the delay expires, the transition is aborted and it returns to `Idle`.

## Key Features

- **Multi-Monitor Geometry**: Computes raw RECT bounds across any monitor topology (including virtual desktops and negative coordinates), avoiding WPF DIP scaling artifacts.
- **Internal Edge Avoidance**: Correctly identifies if an active edge is physically adjacent to another monitor and suppresses handoff, ensuring it only triggers on the outer boundary of the multi-monitor setup.
- **DPI Awareness**: Because it operates on raw Win32 coordinates directly downstream from the Low-Level Hooks, it inherently matches the DPI space of the inputs without needing coordinate conversions.
- **Persistent Configuration**: Saved to `config.json` via the new `ConfigManager`.

## Testing
An automated test suite (`Tests/WindowsHost.Tests`) validates:
- State transitions (Idle -> Candidate -> Armed / Cancelled).
- Threshold arithmetic.
- Negative coordinate handling.
- Internal monitor edge suppression.
