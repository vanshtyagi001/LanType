using System.Runtime.InteropServices;

namespace LANType.Protocol;

[Flags]
public enum HidModifier : byte
{
    None = 0,
    LeftControl = 1 << 0,  // 0x01
    LeftShift = 1 << 1,    // 0x02
    LeftAlt = 1 << 2,      // 0x04
    LeftGUI = 1 << 3,      // 0x08 (Windows key)
    RightControl = 1 << 4, // 0x10
    RightShift = 1 << 5,   // 0x20
    RightAlt = 1 << 6,     // 0x40 (AltGr)
    RightGUI = 1 << 7      // 0x80
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HidKeyboardReport
{
    public byte Modifiers;
    public byte Reserved;
    public byte Key1;
    public byte Key2;
    public byte Key3;
    public byte Key4;
    public byte Key5;
    public byte Key6;

    public static HidKeyboardReport Empty => new()
    {
        Modifiers = 0,
        Reserved = 0,
        Key1 = 0,
        Key2 = 0,
        Key3 = 0,
        Key4 = 0,
        Key5 = 0,
        Key6 = 0
    };

    public static HidKeyboardReport SingleKey(byte modifiers, byte keyCode) => new()
    {
        Modifiers = modifiers,
        Reserved = 0,
        Key1 = keyCode,
        Key2 = 0,
        Key3 = 0,
        Key4 = 0,
        Key5 = 0,
        Key6 = 0
    };

    public static HidKeyboardReport SingleKey(HidModifier modifiers, byte keyCode) => 
        SingleKey((byte)modifiers, keyCode);

    public readonly bool IsEmpty => 
        Modifiers == 0 && Key1 == 0 && Key2 == 0 && Key3 == 0 && Key4 == 0 && Key5 == 0 && Key6 == 0;

    public readonly byte[] ToByteArray()
    {
        return [Modifiers, Reserved, Key1, Key2, Key3, Key4, Key5, Key6];
    }
}
