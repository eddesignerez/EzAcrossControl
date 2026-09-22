# Phase 3.2.1 - SCRCPY UHID PARITY AUDIT

## Objective
The goal of this phase is to achieve absolute functional parity with `scrcpy`'s UHID implementation. Testing showed that our initial UHID implementation had issues with click alignment (likely caused by the Accessibility cursor overlay), scroll functionality, and Japanese keyboard layout mapping.

Since `scrcpy --no-video --no-audio -MK` works perfectly on the same device, we will use it as the authoritative reference for UHID descriptors, report formats, and key mappings.

## Rules & Constraints
1. **No LAN Yet**: This phase still uses `adb forward tcp:8797 tcp:8797` for transport to isolate descriptor/report issues from transport issues.
2. **Disable Accessibility in UHID Mode**: Ensure that `CursorOverlayManager`, `MouseActionExecutor`, and the EZ Across visual pointer are strictly disabled when the UHID backend is active to prevent pointer conflicts.
3. **Exact Descriptor Parity**: Use the exact byte arrays from scrcpy 4.1 (or latest `master`) for both Mouse and Keyboard HID Report Descriptors. Preserve Apache-2.0 copyright notices for any borrowed descriptors.
4. **Mouse Report Parity**: Must be exactly 5 bytes (`buttons`, `relative X`, `relative Y`, `vertical scroll`, `horizontal scroll`).
5. **Keyboard Report Parity**: Must be exactly 8 bytes (`modifiers`, `reserved`, 6 `HID scancodes`).
6. **No Raw VK usage**: Do not map Windows Virtual Keycodes directly to HID usages. Use proper translation.
7. **Japanese physical keys**: Ensure the Android device sees EZ Across UHID as a physical keyboard and properly map Japanese keys (半角/全角, 変換, 無変換).

## Exit Criteria (Parity Gate)
- Native pointer only (no accessibility cursor overlay).
- Visual/click alignment perfect.
- Hold, drag, and text selection maintain button states during motion.
- Vertical/Horizontal wheel scrolling works identically to scrcpy.
- Keyboard works perfectly, including Japanese specific physical keys, and IME composition matches scrcpy.
