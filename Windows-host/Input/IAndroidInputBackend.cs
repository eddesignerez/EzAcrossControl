using System.Threading.Tasks;

namespace EZAcrossControl.Input
{
    public interface IAndroidInputBackend
    {
        Task InitializeAsync();
        Task ConnectAsync(string ipAddress);
        void SetRemoteControl(bool isActive);
        
        // Mouse operations
        void SendMouseMove(short dx, short dy);
        void SendMouseButton(byte buttonMask);
        void SendMouseWheel(sbyte wheelVertical, sbyte wheelHorizontal);
        
        // Keyboard operations
        void SendKeyboardReport(byte modifiers, byte[] keys);
        void SendTextInput(string text); // Used by legacy IME backend
        
        Task ShutdownAsync();
    }
}
