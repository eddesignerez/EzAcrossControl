namespace WindowsHost.Engine
{
    public class AndroidDevice
    {
        public required string DeviceId { get; set; }
        public string? UsbSerial { get; set; }
        public string? TcpSerial { get; set; }
        public string Model { get; set; } = string.Empty;

        public bool UsbAvailable => !string.IsNullOrEmpty(UsbSerial);
        public bool NetworkAvailable => !string.IsNullOrEmpty(TcpSerial);

        public string DisplayName => string.IsNullOrEmpty(Model) ? "Unknown Device" : Model;

        public string? GetSerial(ConnectionMode mode) => mode switch
        {
            ConnectionMode.Usb => UsbSerial,
            ConnectionMode.Network => TcpSerial,
            _ => UsbAvailable ? UsbSerial : TcpSerial
        };
    }
}
