# Phase 2.5.1: Natural Keyboard Ownership & Stabilization

## Objective
Provide seamless keyboard usage when controlling the Android device without requiring the user to switch IME back and forth, fix Backspace/Special keys acting as text, handle common shortcuts (Ctrl+C/V/X/A), and ensure lossless queueing of keyboard events on the Windows side. Additionally, correct a return edge detection issue.

## Implementation Details

### Windows Host
- `WindowsInputCaptureService.cs`: Implemented `IsSpecialKey` function to differentiate true text characters from special scan codes (e.g., Backspace, Kana, Kanji, navigation keys). This prevents `ToUnicodeEx` from returning space characters when Backspace is pressed.
- `InputSessionManager.cs`: Updated keyboard event processing. Instead of enqueuing keyboard events for 8ms polling coalescing (which caused latency and out-of-order execution against mouse events), keyboard events are now dispatched to the socket immediately (`ProcessKeyboardEvent`).

### Android Client
- `accessibility_service_config.xml`: Added the `flagInputMethodEditor` flag. This allows the accessibility service on API 33+ to automatically route hardware keyboard input into standard text fields using `AccessibilityInputConnection`, seamlessly taking over from Gboard without disabling it.
- `EZAcrossInputMethodService.kt`: Added `performContextMenuAction` capability to send copy/paste actions as a fallback when API < 33.
- `AndroidControlManager.kt`:
  - **API 33+ IME Routing**: Uses `accessibilityService.inputMethod.currentInputConnection` to commit text and dispatch key events.
  - **Shortcuts**: Intercepts `KeyEvent.META_CTRL_ON` combined with 'C', 'V', 'X', 'A' and maps them to `AccessibilityNodeInfo` standard actions (Copy, Paste, Cut, Select All) or `performContextMenuAction` on the InputConnection.
  - **Japanese Keys**: Maps `VkZenHankaku` to `KeyEvent.KEYCODE_ZENKAKU_HANKAKU` and other standard IME layout keys.
  - **Return Edge**: Improved the return edge checking logic. If Android is physically placed on the right of Windows, pushing against the *left* edge (X <= 0) triggers the return handoff, checking against push distance/acceleration instead of just position thresholds.
- `MainActivity.kt`: Updated UI to indicate "API >= 33 (Automatic Ownership)" and removed the manual IME selection button for Android 13+.

## Verification
- Backspace now deletes text instead of adding a space.
- Special keys properly pass through.
- Keyboard input feels significantly more responsive due to bypassing the 8ms polling loop on the Windows host.
- Keyboard ownership works implicitly without switching IME on supported versions.
- Pushing against the correct edge smoothly returns control to Windows.
