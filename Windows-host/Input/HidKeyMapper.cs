using System.Collections.Generic;

namespace EZAcrossControl.Input
{
    public static class HidKeyMapper
    {
        // Maps Windows Virtual Key Codes to USB HID Usage IDs (Keyboard/Keypad Page 0x07)
        public static byte GetHidUsageId(int vkCode)
        {
            return _vkToHid.TryGetValue(vkCode, out byte hid) ? hid : (byte)0;
        }

        private static readonly Dictionary<int, byte> _vkToHid = new Dictionary<int, byte>()
        {
            { 0x41, 0x04 }, // A
            { 0x42, 0x05 }, // B
            { 0x43, 0x06 }, // C
            { 0x44, 0x07 }, // D
            { 0x45, 0x08 }, // E
            { 0x46, 0x09 }, // F
            { 0x47, 0x0A }, // G
            { 0x48, 0x0B }, // H
            { 0x49, 0x0C }, // I
            { 0x4A, 0x0D }, // J
            { 0x4B, 0x0E }, // K
            { 0x4C, 0x0F }, // L
            { 0x4D, 0x10 }, // M
            { 0x4E, 0x11 }, // N
            { 0x4F, 0x12 }, // O
            { 0x50, 0x13 }, // P
            { 0x51, 0x14 }, // Q
            { 0x52, 0x15 }, // R
            { 0x53, 0x16 }, // S
            { 0x54, 0x17 }, // T
            { 0x55, 0x18 }, // U
            { 0x56, 0x19 }, // V
            { 0x57, 0x1A }, // W
            { 0x58, 0x1B }, // X
            { 0x59, 0x1C }, // Y
            { 0x5A, 0x1D }, // Z
            { 0x31, 0x1E }, // 1
            { 0x32, 0x1F }, // 2
            { 0x33, 0x20 }, // 3
            { 0x34, 0x21 }, // 4
            { 0x35, 0x22 }, // 5
            { 0x36, 0x23 }, // 6
            { 0x37, 0x24 }, // 7
            { 0x38, 0x25 }, // 8
            { 0x39, 0x26 }, // 9
            { 0x30, 0x27 }, // 0
            { 0x0D, 0x28 }, // Enter
            { 0x1B, 0x29 }, // Esc
            { 0x08, 0x2A }, // Backspace
            { 0x09, 0x2B }, // Tab
            { 0x20, 0x2C }, // Space
            { 0xBD, 0x2D }, // - (Minus)
            { 0xBB, 0x2E }, // = (Equal)
            { 0xDB, 0x2F }, // [
            { 0xDD, 0x30 }, // ]
            { 0xDC, 0x31 }, // \
            { 0xBA, 0x33 }, // ;
            { 0xDE, 0x34 }, // '
            { 0xC0, 0x35 }, // `
            { 0xBC, 0x36 }, // ,
            { 0xBE, 0x37 }, // .
            { 0xBF, 0x38 }, // /
            { 0x14, 0x39 }, // Caps Lock
            { 0x70, 0x3A }, // F1
            { 0x71, 0x3B }, // F2
            { 0x72, 0x3C }, // F3
            { 0x73, 0x3D }, // F4
            { 0x74, 0x3E }, // F5
            { 0x75, 0x3F }, // F6
            { 0x76, 0x40 }, // F7
            { 0x77, 0x41 }, // F8
            { 0x78, 0x42 }, // F9
            { 0x79, 0x43 }, // F10
            { 0x7A, 0x44 }, // F11
            { 0x7B, 0x45 }, // F12
            { 0x2C, 0x46 }, // Print Screen
            { 0x91, 0x47 }, // Scroll Lock
            { 0x13, 0x48 }, // Pause
            { 0x2D, 0x49 }, // Insert
            { 0x24, 0x4A }, // Home
            { 0x21, 0x4B }, // Page Up
            { 0x2E, 0x4C }, // Delete
            { 0x23, 0x4D }, // End
            { 0x22, 0x4E }, // Page Down
            { 0x27, 0x4F }, // Right
            { 0x25, 0x50 }, // Left
            { 0x28, 0x51 }, // Down
            { 0x26, 0x52 }, // Up
            { 0x90, 0x53 }, // Num Lock
            // Japanese Keys
            { 0xF3, 0x85 }, // Hankaku/Zenkaku (Kanji)
            { 0x1C, 0x8A }, // Henkan
            { 0x1D, 0x8B }, // Muhenkan
            { 0xF2, 0x88 }, // Katakana/Hiragana (Romaji)
            { 0xF4, 0x85 }, // Hankaku/Zenkaku Alt
        };
    }
}
