using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace WindowsHost.Input
{
    public class WindowsInputCaptureService : IInputCaptureService, IDisposable
    {
        public event EventHandler<InputEvent>? InputEventCaptured;
        
        public bool IsCapturing { get; private set; }
        public bool SuppressLocalMouseEvents { get; set; } = false;
        public bool SuppressLocalKeyboardEvents { get; set; } = false;

        private IntPtr _mouseHookID = IntPtr.Zero;
        private IntPtr _keyboardHookID = IntPtr.Zero;

        private NativeMethods.LowLevelProc _mouseProc;
        private NativeMethods.LowLevelProc _keyboardProc;

        private int _lastMouseX = -1;
        private int _lastMouseY = -1;

        public WindowsInputCaptureService()
        {
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        public void StartCapture()
        {
            if (IsCapturing) return;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule.ModuleName);

                _mouseHookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
                _keyboardHookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            }

            IsCapturing = true;
        }

        public void StopCapture()
        {
            if (!IsCapturing) return;

            if (_mouseHookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookID);
                _mouseHookID = IntPtr.Zero;
            }

            if (_keyboardHookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookID);
                _keyboardHookID = IntPtr.Zero;
            }

            IsCapturing = false;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                bool isInjected = (hookStruct.flags & NativeMethods.LLMHF_INJECTED) != 0;

                InputEventType? eventType = (int)wParam switch
                {
                    NativeMethods.WM_MOUSEMOVE => InputEventType.MouseMove,
                    NativeMethods.WM_LBUTTONDOWN => InputEventType.LeftButtonDown,
                    NativeMethods.WM_LBUTTONUP => InputEventType.LeftButtonUp,
                    NativeMethods.WM_RBUTTONDOWN => InputEventType.RightButtonDown,
                    NativeMethods.WM_RBUTTONUP => InputEventType.RightButtonUp,
                    NativeMethods.WM_MBUTTONDOWN => InputEventType.MiddleButtonDown,
                    NativeMethods.WM_MBUTTONUP => InputEventType.MiddleButtonUp,
                    NativeMethods.WM_MOUSEWHEEL => InputEventType.MouseWheel,
                    NativeMethods.WM_MOUSEHWHEEL => InputEventType.HorizontalWheel,
                    _ => null
                };

                if (eventType.HasValue)
                {
                    int wheelDelta = 0;
                    if (eventType == InputEventType.MouseWheel || eventType == InputEventType.HorizontalWheel)
                    {
                        // High-order word of mouseData contains the wheel delta
                        wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    }

                    int button = 0; // Not fully mapped for extra buttons yet, mapping standard types is enough for now

                    int deltaX = 0;
                    int deltaY = 0;
                    if (_lastMouseX != -1 && _lastMouseY != -1)
                    {
                        deltaX = hookStruct.pt.x - _lastMouseX;
                        deltaY = hookStruct.pt.y - _lastMouseY;
                    }
                    _lastMouseX = hookStruct.pt.x;
                    _lastMouseY = hookStruct.pt.y;

                    var mouseEvent = new MouseInputEvent(
                        eventType.Value,
                        isInjected,
                        hookStruct.pt.x,
                        hookStruct.pt.y,
                        deltaX, deltaY,
                        button,
                        wheelDelta
                    );

                    InputEventCaptured?.Invoke(this, mouseEvent);
                }

                if (SuppressLocalMouseEvents)
                {
                    // Block local mouse events from propagating to Windows
                    return (IntPtr)1;
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        private bool IsSpecialKey(uint vkCode)
        {
            return vkCode switch
            {
                NativeMethods.VK_BACK => true, // 0x08
                NativeMethods.VK_TAB => true, // 0x09
                NativeMethods.VK_RETURN => true, // 0x0D
                NativeMethods.VK_ESCAPE => true, // 0x1B
                NativeMethods.VK_DELETE => true, // 0x2E
                0x15 => true, // VK_KANA / VK_HANGUL / VK_HANGEUL
                0x19 => true, // VK_KANJI / VK_HANJA
                0x1C => true, // VK_CONVERT
                0x1D => true, // VK_NONCONVERT
                0x21 => true, // PAGE_UP
                0x22 => true, // PAGE_DOWN
                0x23 => true, // END
                0x24 => true, // HOME
                0x25 => true, // LEFT
                0x26 => true, // UP
                0x27 => true, // RIGHT
                0x28 => true, // DOWN
                0xF2 => true, // VK_OEM_COPY (Katakana/Hiragana on some jp keyboards)
                _ => false
            };
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                bool isInjected = (hookStruct.flags & NativeMethods.LLKHF_INJECTED) != 0;
                bool isExtended = (hookStruct.flags & NativeMethods.LLKHF_EXTENDED) != 0;

                InputEventType? eventType = (int)wParam switch
                {
                    NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN => InputEventType.KeyDown,
                    NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP => InputEventType.KeyUp,
                    _ => null
                };

                if (eventType.HasValue)
                {
                    bool ctrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                    bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                    bool alt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
                    bool win = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || 
                               (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                    // Convert virtual key to string representation using WPF KeyInterop
                    string keyName = "Unknown";
                    try
                    {
                        Key key = KeyInterop.KeyFromVirtualKey((int)hookStruct.vkCode);
                        keyName = key.ToString();
                    }
                    catch
                    {
                        keyName = $"VK_{hookStruct.vkCode}";
                    }

                    string? textContent = null;
                    bool shouldSuppress = false;
                    bool isSpecialKey = IsSpecialKey(hookStruct.vkCode);

                    if (SuppressLocalKeyboardEvents)
                    {
                        if (hookStruct.vkCode == NativeMethods.VK_ESCAPE)
                        {
                            shouldSuppress = false;
                        }
                        else if (win || (alt && hookStruct.vkCode == NativeMethods.VK_TAB) || (ctrl && hookStruct.vkCode >= 0x41 && hookStruct.vkCode <= 0x5A))
                        {
                            shouldSuppress = true; // Block OS combos and Ctrl+A..Z so they don't affect local window
                        }
                        else
                        {
                            // Do not suppress other keys. 
                            // This allows WPF and IME (like Japanese) to process the keystrokes
                            // and generate text via our HiddenImeSink in MainWindow.
                            shouldSuppress = false;
                        }
                    }

                    var keyEvent = new KeyboardInputEvent(
                        eventType.Value,
                        isInjected,
                        (int)hookStruct.vkCode,
                        (int)hookStruct.scanCode,
                        keyName,
                        isExtended,
                        ctrl, shift, alt, win,
                        textContent
                    );

                    InputEventCaptured?.Invoke(this, keyEvent);

                    if (shouldSuppress)
                    {
                        return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
