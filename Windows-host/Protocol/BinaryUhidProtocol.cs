using System;
using System.IO;

namespace EZAcrossControl.Protocol
{
    public static class BinaryUhidProtocol
    {
        public const int DEFAULT_PORT = 8797;

        public const byte TYPE_HELLO = 0x01;
        public const byte TYPE_HELLO_ACK = 0x02;
        public const byte TYPE_MOUSE_REL = 0x10;
        public const byte TYPE_KEYBOARD_REPORT = 0x20;
        public const byte TYPE_PING = 0x30;
        public const byte TYPE_PONG = 0x31;
        public const byte TYPE_CONTROL_RELEASE = 0x40;

        public static void WriteHello(BinaryWriter writer, string token)
        {
            writer.Write(TYPE_HELLO);
            writer.Write((byte)1); // Version 1
            byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
            writer.Write((byte)tokenBytes.Length);
            writer.Write(tokenBytes);
        }

        public static void WriteMouseRel(BinaryWriter writer, uint sequence, short dx, short dy, byte buttons, sbyte wheelV, sbyte wheelH)
        {
            writer.Write(TYPE_MOUSE_REL);
            // Little Endian natively in BinaryWriter on Windows
            writer.Write(sequence);
            writer.Write(dx);
            writer.Write(dy);
            writer.Write(buttons);
            writer.Write(wheelV);
            writer.Write(wheelH);
        }

        public static void WriteKeyboardReport(BinaryWriter writer, uint sequence, byte modifiers, byte[] keys)
        {
            if (keys == null || keys.Length != 6) throw new ArgumentException("Keys must be exactly 6 bytes.");
            
            writer.Write(TYPE_KEYBOARD_REPORT);
            writer.Write(sequence);
            writer.Write(modifiers);
            writer.Write((byte)0); // reserved
            writer.Write(keys);
        }
        
        public static void WriteControlRelease(BinaryWriter writer)
        {
            writer.Write(TYPE_CONTROL_RELEASE);
        }
    }
}
