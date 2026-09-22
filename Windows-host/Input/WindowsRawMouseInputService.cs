using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsHost.Input
{
    public class WindowsRawMouseInputService : IInputCaptureService, IDisposable
    {
        public event EventHandler<InputEvent>? InputEventCaptured;
        public bool IsCapturing { get; private set; }
        
        public bool SuppressLocalMouseEvents { get; set; }
        public bool SuppressLocalKeyboardEvents { get; set; }
        
        private IntPtr _windowHandle;
        private HwndSource? _hwndSource;

        public WindowsRawMouseInputService(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        }

        public void StartCapture()
        {
            if (IsCapturing) return;

            var device = new NativeMethods.RAWINPUTDEVICE
            {
                usUsagePage = 0x01, // Generic Desktop Controls
                usUsage = 0x02,     // Mouse
                dwFlags = NativeMethods.RIDEV_INPUTSINK, // Capture even if not in foreground
                hwndTarget = _windowHandle
            };

            int structSize = Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE));
            if (NativeMethods.RegisterRawInputDevices(new[] { device }, 1, (uint)structSize))
            {
                _hwndSource = HwndSource.FromHwnd(_windowHandle);
                _hwndSource?.AddHook(WndProc);
                IsCapturing = true;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Failed to register Raw Input: {error}");
            }
        }

        public void StopCapture()
        {
            if (!IsCapturing) return;

            var device = new NativeMethods.RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x02,
                dwFlags = NativeMethods.RIDEV_REMOVE,
                hwndTarget = IntPtr.Zero
            };

            int structSize = Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE));
            NativeMethods.RegisterRawInputDevices(new[] { device }, 1, (uint)structSize);

            _hwndSource?.RemoveHook(WndProc);
            IsCapturing = false;
        }

        private long _rawEventsCount = 0;
        private DateTime _lastRawLogTime = DateTime.UtcNow;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == NativeMethods.WM_INPUT)
                {
                    uint dwSize = 0;
                    uint headerSize = (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER));
                    
                    NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);

                    if (dwSize > 0)
                    {
                        IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                        try
                        {
                            if (NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, buffer, ref dwSize, headerSize) == dwSize)
                            {
                                var header = Marshal.PtrToStructure<NativeMethods.RAWINPUTHEADER>(buffer);
                                
                                if (header.dwType == NativeMethods.RIM_TYPEMOUSE)
                                {
                                    // Instead of marshalling RAWMOUSE which can have padding issues,
                                    // we calculate the offset manually.
                                    // RAWMOUSE starts at buffer + headerSize.
                                    // lLastX is at offset 12 in RAWMOUSE.
                                    // lLastY is at offset 16 in RAWMOUSE.
                                    
                                    int deltaX = Marshal.ReadInt32(buffer, (int)headerSize + 12);
                                    int deltaY = Marshal.ReadInt32(buffer, (int)headerSize + 16);

                                    if (deltaX != 0 || deltaY != 0)
                                    {
                                        System.Threading.Interlocked.Increment(ref _rawEventsCount);

                                        var now = DateTime.UtcNow;
                                        if ((now - _lastRawLogTime).TotalSeconds >= 1.0)
                                        {
                                            long count = System.Threading.Interlocked.Exchange(ref _rawEventsCount, 0);
                                            Logger.Log("METRICS", $"rawEventsPerSecond: {count}");
                                            _lastRawLogTime = now;
                                        }

                                        var mouseEvent = new MouseInputEvent(
                                            InputEventType.MouseMove,
                                            false,
                                            0, 0, // x, y are absolute, not updated here
                                            deltaX, deltaY,
                                            0, 0
                                        );
                                        InputEventCaptured?.Invoke(this, mouseEvent);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RawMouseInput_WndProc");
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
