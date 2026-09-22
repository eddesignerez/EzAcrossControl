using System;

namespace WindowsHost.Input
{
    public interface IInputCaptureService
    {
        event EventHandler<InputEvent> InputEventCaptured;
        bool IsCapturing { get; }
        bool SuppressLocalMouseEvents { get; set; }
        bool SuppressLocalKeyboardEvents { get; set; }
        void StartCapture();
        void StopCapture();
    }
}
