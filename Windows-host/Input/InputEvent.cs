using System;

namespace WindowsHost.Input
{
    public abstract class InputEvent
    {
        public DateTime Timestamp { get; }
        public InputEventType Type { get; }
        public bool IsInjected { get; }

        protected InputEvent(InputEventType type, bool isInjected)
        {
            Timestamp = DateTime.Now;
            Type = type;
            IsInjected = isInjected;
        }

        public abstract override string ToString();
    }
}
