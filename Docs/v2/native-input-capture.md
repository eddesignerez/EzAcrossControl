# Native mouse capture

The Windows host parks its existing 1px SDL input window at the selected handoff edge. USB and Wi-Fi use the same UHID mouse and keyboard path. Connection preferences and the companion JSON protocol are unchanged.

During native capture, SDL relative mode supplies motion and UHID button events. SDL 3.4.10 deliberately skips its automatic Win32 mouse capture in relative mode (`SDL_UpdateMouseCapture`, `src/events/SDL_mouse.c`). EZ Across therefore calls `SetCapture` on its own foreground HWND, on the SDL thread, after enabling relative mode. This routes legacy Windows button messages to the input surface instead of the application behind it. The capture is released only if that HWND still owns it. No cursor warp, larger input window, foreground retry loop, or forced focus recovery is added.

`CAPTURED` is sent only after relative mode and native button capture succeed. A failed native button capture rolls back relative mode and replies `ERROR CAPTURE_FAILED`. These acknowledgements are written on the SDL thread before subsequent focus notifications; all IPC writes share a lock.

A genuine focus loss clears the native capture intent and sends `CAPTURE_LOST`. A queued focus-loss event from an earlier handoff is ignored if SDL's current keyboard focus is still the input window. After a genuine release, clicks and capture shortcuts cannot silently recapture without a new host request. Explicit `CAPTURE_OFF` retains the existing guard against reapplying an unchanged relative mode and moving the cursor twice.

The host reconciles `CAPTURE_LOST`, `RELEASED`, and capture failure with Windows ownership. Pending capture confirmation and native release share a lock, so a delayed continuation cannot restore `Captured` after focus loss. Notifications from replaced IPC clients are ignored. A rejected or timed-out capture rolls back the prepared handoff, allowing the existing UI and companion status to update.

Regression checks:

- `scripts/test-native-mouse-capture.ps1` compiles the real native handler against fixtures without physical input, windows, ADB, or network access.
- `ScrcpyCaptureStateTests` covers successful capture/release, pending rejection, acknowledgement/focus-loss ordering, and stale IPC notifications.
- Manual acceptance requires clicks, double clicks, scrolling, held drags, Escape, intentional edge return, and focus changes on both USB and Wi-Fi. A simulated regression pass does not establish physical acceptance.

Reference: [SDL 3.4.10 mouse capture implementation](https://github.com/libsdl-org/SDL/blob/release-3.4.10/src/events/SDL_mouse.c).
