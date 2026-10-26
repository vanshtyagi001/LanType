using LANType.Protocol;

namespace LANType.Service.Keyboard;

public record KeyMappingResult(bool IsSupported, HidModifier Modifiers, byte KeyCode, string? UnsupportedReason = null);

/// <summary>
/// Maps characters to standard US English HID Keyboard usage codes and modifiers.
/// Strictly implements section 4.4 of project specification.
/// </summary>
public static class USKeyboardLayoutMapper
{
    public static KeyMappingResult MapCharacter(char c)
    {
        // Lowercase letters a-z
        if (c >= 'a' && c <= 'z')
        {
            byte code = (byte)(HidUsageCodes.KeyA + (c - 'a'));
            return new KeyMappingResult(true, HidModifier.None, code);
        }

        // Uppercase letters A-Z (Shift + Key)
        if (c >= 'A' && c <= 'Z')
        {
            byte code = (byte)(HidUsageCodes.KeyA + (c - 'A'));
            return new KeyMappingResult(true, HidModifier.LeftShift, code);
        }

        // Digits 1-9
        if (c >= '1' && c <= '9')
        {
            byte code = (byte)(HidUsageCodes.Key1 + (c - '1'));
            return new KeyMappingResult(true, HidModifier.None, code);
        }

        // Digit 0
        if (c == '0')
        {
            return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Key0);
        }

        // Whitespace and controls
        switch (c)
        {
            case ' ':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Space);
            case '\t':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Tab);
            case '\r':
            case '\n':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Enter);
            case '\b':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Backspace);
        }

        // Unshifted symbols
        switch (c)
        {
            case '-':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Minus);
            case '=':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Equal);
            case '[':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.LeftBracket);
            case ']':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.RightBracket);
            case '\\':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Backslash);
            case ';':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Semicolon);
            case '\'':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Quote);
            case '`':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Grave);
            case ',':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Comma);
            case '.':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Period);
            case '/':
                return new KeyMappingResult(true, HidModifier.None, HidUsageCodes.Slash);
        }

        // Shifted symbols
        switch (c)
        {
            case '!':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key1);
            case '@':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key2);
            case '#':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key3);
            case '$':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key4);
            case '%':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key5);
            case '^':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key6);
            case '&':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key7);
            case '*':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key8);
            case '(':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key9);
            case ')':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Key0);
            case '_':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Minus);
            case '+':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Equal);
            case '{':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.LeftBracket);
            case '}':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.RightBracket);
            case '|':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Backslash);
            case ':':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Semicolon);
            case '"':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Quote);
            case '~':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Grave);
            case '<':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Comma);
            case '>':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Period);
            case '?':
                return new KeyMappingResult(true, HidModifier.LeftShift, HidUsageCodes.Slash);
        }

        // Unsupported character (Unicode, emoji, non-Latin)
        return new KeyMappingResult(false, HidModifier.None, HidUsageCodes.None, 
            $"Character '{c}' (U+{(int)c:X4}) cannot be represented in US English HID keyboard layout.");
    }
}
