# Phase 2.1 - Windows Input Capture

## Overview
Phase 2.1 implemented local input capture (mouse and keyboard) in the Windows Host application. The goal was to accurately detect input events using Win32 low-level hooks, without sending them over the network or controlling the Android device yet.

## Architecture
A decoupled layer was created in `Windows-host/Input/`:
- `IInputCaptureService.cs`: Interface for starting/stopping capture.
- `WindowsInputCaptureService.cs`: Implementation that sets global hooks.
- `NativeMethods.cs`: P/Invoke signatures for `SetWindowsHookEx`, `UnhookWindowsHookEx`, `GetAsyncKeyState`.
- `InputEvent.cs` and inherited `MouseInputEvent.cs` / `KeyboardInputEvent.cs` models.

## Technology Used
- **Win32 Low-Level Hooks**: `WH_MOUSE_LL` (14) and `WH_KEYBOARD_LL` (13).
- **WPF UI Batching**: The service raises events on background threads (from the hook message pump). To prevent UI freezing, especially from rapid `MouseMove` events, a `ConcurrentQueue<InputEvent>` is used alongside a `DispatcherTimer` running at ~20 FPS (50ms interval) to batch UI updates.

## Features Supported
- **Mouse**: `MouseMove`, `LeftButtonDown`, `LeftButtonUp`, `RightButtonDown`, `RightButtonUp`, `MiddleButtonDown`, `MiddleButtonUp`, `MouseWheel`, `HorizontalWheel`.
- **Keyboard**: `KeyDown`, `KeyUp`.
- **Modifiers**: Ctrl, Shift, Alt, Windows (detected via `GetAsyncKeyState`).
- **Injected Flag**: Detects `LLMHF_INJECTED` and `LLKHF_INJECTED` to flag artificial events, preventing future feedback loops.

## Multi-Monitor & DPI
- Windows low-level mouse hooks (`WH_MOUSE_LL`) report absolute coordinates of the virtual screen.
- WPF applications are typically System DPI aware by default (defined in manifest/framework defaults). 
- In this phase, we capture the raw `X`/`Y` reported by the hook. Future phases will need to translate these virtual screen coordinates to Android screen coordinates.

## Security
- No keystrokes are written to disk.
- No string building or keylogging sentences.
- Events are buffered in memory only up to the last 100 items for debug UI purposes.
- Hooks are cleanly disposed upon stopping the capture or closing the app.

## Testing Performed
- Validated `dotnet build` passes with zero errors.
- Manual testing verified start/stop lifecycle, memory-safe hook removal, performance under heavy mouse movement (no UI freezing thanks to the queue/timer), and correct capturing of modifier keys (Ctrl+C, etc).
