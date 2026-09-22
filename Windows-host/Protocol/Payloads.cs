using System.Collections.Generic;

namespace WindowsHost.Protocol
{
    public class HelloPayload
    {
        public string DeviceName { get; set; } = string.Empty;
    }

    public class MouseMovePayload
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int DeltaX { get; set; }
        public int DeltaY { get; set; }
        public double NormalizedX { get; set; }
        public double NormalizedY { get; set; }
        public bool IsInjected { get; set; }
    }

    public class MouseButtonPayload
    {
        public string Button { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsInjected { get; set; }
    }

    public class MouseWheelPayload
    {
        public string Axis { get; set; } = string.Empty;
        public int Delta { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class KeyPayload
    {
        public int VirtualKeyCode { get; set; }
        public int ScanCode { get; set; }
        public bool IsExtended { get; set; }
        public bool IsInjected { get; set; }
        public ModifiersPayload Modifiers { get; set; } = new ModifiersPayload();
    }

    public class ModifiersPayload
    {
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }
    }

    public class HandoffBeginPayload
    {
        public string Edge { get; set; }
        public int SessionId { get; set; }
        public double EntryNormalizedY { get; set; }
        public long ClientTxTimestamp { get; set; }
    }

    public class TextCommitPayload
    {
        public string Text { get; set; } = string.Empty;
        // Optionally timestamp can be serialized here or in the envelope
    }
}
