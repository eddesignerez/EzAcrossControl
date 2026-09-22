using System;

namespace WindowsHost.Protocol
{
    public class MessageEnvelope
    {
        public string Type { get; set; } = string.Empty;
        public int ProtocolVersion { get; set; } = 1;
        public long Sequence { get; set; }
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public object Payload { get; set; } = new object();
    }
}
