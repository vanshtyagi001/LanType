using LANType.Protocol;
using LANType.Service.Keyboard;
using Xunit;

namespace LANType.Tests;

public class LayoutMapperTests
{
    [Theory]
    [InlineData('a', HidModifier.None, HidUsageCodes.KeyA)]
    [InlineData('z', HidModifier.None, HidUsageCodes.KeyZ)]
    [InlineData('m', HidModifier.None, HidUsageCodes.KeyM)]
    [InlineData('A', HidModifier.LeftShift, HidUsageCodes.KeyA)]
    [InlineData('Z', HidModifier.LeftShift, HidUsageCodes.KeyZ)]
    public void MapCharacter_Letters_CorrectModifiersAndUsage(char input, HidModifier expectedMod, byte expectedKey)
    {
        var result = USKeyboardLayoutMapper.MapCharacter(input);
        Assert.True(result.IsSupported);
        Assert.Equal(expectedMod, result.Modifiers);
        Assert.Equal(expectedKey, result.KeyCode);
    }

    [Theory]
    [InlineData('1', HidModifier.None, HidUsageCodes.Key1)]
    [InlineData('0', HidModifier.None, HidUsageCodes.Key0)]
    [InlineData('!', HidModifier.LeftShift, HidUsageCodes.Key1)]
    [InlineData('@', HidModifier.LeftShift, HidUsageCodes.Key2)]
    [InlineData('#', HidModifier.LeftShift, HidUsageCodes.Key3)]
    [InlineData('$', HidModifier.LeftShift, HidUsageCodes.Key4)]
    [InlineData('%', HidModifier.LeftShift, HidUsageCodes.Key5)]
    [InlineData('^', HidModifier.LeftShift, HidUsageCodes.Key6)]
    [InlineData('&', HidModifier.LeftShift, HidUsageCodes.Key7)]
    [InlineData('*', HidModifier.LeftShift, HidUsageCodes.Key8)]
    [InlineData('(', HidModifier.LeftShift, HidUsageCodes.Key9)]
    [InlineData(')', HidModifier.LeftShift, HidUsageCodes.Key0)]
    public void MapCharacter_DigitsAndShiftedSymbols_CorrectModifiers(char input, HidModifier expectedMod, byte expectedKey)
    {
        var result = USKeyboardLayoutMapper.MapCharacter(input);
        Assert.True(result.IsSupported);
        Assert.Equal(expectedMod, result.Modifiers);
        Assert.Equal(expectedKey, result.KeyCode);
    }

    [Theory]
    [InlineData(' ', HidModifier.None, HidUsageCodes.Space)]
    [InlineData('\t', HidModifier.None, HidUsageCodes.Tab)]
    [InlineData('\n', HidModifier.None, HidUsageCodes.Enter)]
    [InlineData('\r', HidModifier.None, HidUsageCodes.Enter)]
    public void MapCharacter_WhitespaceAndControls(char input, HidModifier expectedMod, byte expectedKey)
    {
        var result = USKeyboardLayoutMapper.MapCharacter(input);
        Assert.True(result.IsSupported);
        Assert.Equal(expectedMod, result.Modifiers);
        Assert.Equal(expectedKey, result.KeyCode);
    }

    [Theory]
    [InlineData('€')] // Euro currency symbol (non-ASCII)
    [InlineData('ж')] // Cyrillic
    [InlineData('文')] // CJK
    public void MapCharacter_UnsupportedUnicode_ReturnsNotSupported(char input)
    {
        var result = USKeyboardLayoutMapper.MapCharacter(input);
        Assert.False(result.IsSupported);
        Assert.NotNull(result.UnsupportedReason);
    }
}
