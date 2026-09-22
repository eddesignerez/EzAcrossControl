namespace WindowsHost.Input
{
    public class MouseInputEvent : InputEvent
    {
        public int X { get; }
        public int Y { get; }
        public int DeltaX { get; }
        public int DeltaY { get; }
        public int Button { get; }
        public int WheelDelta { get; }

        public MouseInputEvent(InputEventType type, bool isInjected, int x, int y, int deltaX = 0, int deltaY = 0, int button = 0, int wheelDelta = 0) 
            : base(type, isInjected)
        {
            X = x;
            Y = y;
            DeltaX = deltaX;
            DeltaY = deltaY;
            Button = button;
            WheelDelta = wheelDelta;
        }

        public override string ToString()
        {
            if (Type == InputEventType.MouseWheel || Type == InputEventType.HorizontalWheel)
            {
                return $"{Type} WheelDelta={WheelDelta} X={X} Y={Y} Injected={IsInjected}";
            }
            return $"{Type} X={X} Y={Y} Injected={IsInjected}";
        }
    }
}
