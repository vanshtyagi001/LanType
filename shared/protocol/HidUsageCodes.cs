namespace LANType.Protocol;

/// <summary>
/// USB HID Keyboard/Keypad Usage IDs (Page 0x07)
/// Conforms to USB HID Usage Tables specification v1.22
/// </summary>
public static class HidUsageCodes
{
    public const byte None = 0x00;

    // Letters A-Z (0x04 - 0x1D)
    public const byte KeyA = 0x04;
    public const byte KeyB = 0x05;
    public const byte KeyC = 0x06;
    public const byte KeyD = 0x07;
    public const byte KeyE = 0x08;
    public const byte KeyF = 0x09;
    public const byte KeyG = 0x0A;
    public const byte KeyH = 0x0B;
    public const byte KeyI = 0x0C;
    public const byte KeyJ = 0x0D;
    public const byte KeyK = 0x0E;
    public const byte KeyL = 0x0F;
    public const byte KeyM = 0x10;
    public const byte KeyN = 0x11;
    public const byte KeyO = 0x12;
    public const byte KeyP = 0x13;
    public const byte KeyQ = 0x14;
    public const byte KeyR = 0x15;
    public const byte KeyS = 0x16;
    public const byte KeyT = 0x17;
    public const byte KeyU = 0x18;
    public const byte KeyV = 0x19;
    public const byte KeyW = 0x1A;
    public const byte KeyX = 0x1B;
    public const byte KeyY = 0x1C;
    public const byte KeyZ = 0x1D;

    // Numbers 1-9, 0 (0x1E - 0x27)
    public const byte Key1 = 0x1E; // 1 and !
    public const byte Key2 = 0x1F; // 2 and @
    public const byte Key3 = 0x20; // 3 and #
    public const byte Key4 = 0x21; // 4 and $
    public const byte Key5 = 0x22; // 5 and %
    public const byte Key6 = 0x23; // 6 and ^
    public const byte Key7 = 0x24; // 7 and &
    public const byte Key8 = 0x25; // 8 and *
    public const byte Key9 = 0x26; // 9 and (
    public const byte Key0 = 0x27; // 0 and )

    // Controls & Whitespace
    public const byte Enter = 0x28;
    public const byte Escape = 0x29;
    public const byte Backspace = 0x2A;
    public const byte Tab = 0x2B;
    public const byte Space = 0x2C;

    // Symbols & Punctuation
    public const byte Minus = 0x2D;        // - and _
    public const byte Equal = 0x2E;        // = and +
    public const byte LeftBracket = 0x2F;  // [ and {
    public const byte RightBracket = 0x30; // ] and }
    public const byte Backslash = 0x31;    // \ and |
    public const byte Semicolon = 0x33;    // ; and :
    public const byte Quote = 0x34;        // ' and "
    public const byte Grave = 0x35;        // ` and ~
    public const byte Comma = 0x36;        // , and <
    public const byte Period = 0x37;       // . and >
    public const byte Slash = 0x38;        // / and ?
    public const byte CapsLock = 0x39;

    // Function Keys
    public const byte F1 = 0x3A;
    public const byte F2 = 0x3B;
    public const byte F3 = 0x3C;
    public const byte F4 = 0x3D;
    public const byte F5 = 0x3E;
    public const byte F6 = 0x3F;
    public const byte F7 = 0x40;
    public const byte F8 = 0x41;
    public const byte F9 = 0x42;
    public const byte F10 = 0x43;
    public const byte F11 = 0x44;
    public const byte F12 = 0x45;

    // Navigation & Editing
    public const byte PrintScreen = 0x46;
    public const byte ScrollLock = 0x47;
    public const byte Pause = 0x48;
    public const byte Insert = 0x49;
    public const byte Home = 0x4A;
    public const byte PageUp = 0x4B;
    public const byte Delete = 0x4C;
    public const byte End = 0x4D;
    public const byte PageDown = 0x4E;
    public const byte RightArrow = 0x4F;
    public const byte LeftArrow = 0x50;
    public const byte DownArrow = 0x51;
    public const byte UpArrow = 0x52;
}
