using System.Text;

namespace WindowsHost.Input
{
    public class KeyboardInputEvent : InputEvent
    {
        public int VirtualKeyCode { get; }
        public int ScanCode { get; }
        public string KeyName { get; }
        public bool IsExtended { get; }
        
        public bool CtrlPressed { get; }
        public bool ShiftPressed { get; }
        public bool AltPressed { get; }
        public bool WinPressed { get; }
        
        public string? TextContent { get; }

        public KeyboardInputEvent(InputEventType type, bool isInjected, int virtualKeyCode, int scanCode, string keyName, bool isExtended, bool ctrl, bool shift, bool alt, bool win, string? textContent = null) 
            : base(type, isInjected)
        {
            VirtualKeyCode = virtualKeyCode;
            ScanCode = scanCode;
            KeyName = keyName;
            IsExtended = isExtended;
            CtrlPressed = ctrl;
            ShiftPressed = shift;
            AltPressed = alt;
            WinPressed = win;
            TextContent = textContent;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{Type} Key={KeyName} ");
            
            var mods = new System.Collections.Generic.List<string>();
            if (CtrlPressed) mods.Add("Ctrl");
            if (ShiftPressed) mods.Add("Shift");
            if (AltPressed) mods.Add("Alt");
            if (WinPressed) mods.Add("Win");
            
            if (mods.Count > 0)
            {
                sb.Append($"Mods=[{string.Join("+", mods)}] ");
            }
            
            if (!string.IsNullOrEmpty(TextContent))
            {
                sb.Append($"Text='{TextContent}' ");
            }
            
            sb.Append($"Injected={IsInjected}");
            return sb.ToString();
        }
    }
}
