using System.Globalization;

namespace CloudAndBear.Ui.Terminal;

/// <summary>Koliko ćelija zauzima znak: 0 (kombinujući), 1 ili 2 (kineski, japanski, emodži).</summary>
public static class CharWidth
{
    public static int Of(int rune)
    {
        if (rune < 0x300) return rune >= 0x20 ? 1 : 0;

        if (rune is >= 0x0300 and <= 0x036F or >= 0x200B and <= 0x200F or >= 0xFE00 and <= 0xFE0F or 0x2060 or 0xFEFF)
            return 0;

        if (IsWide(rune)) return 2;

        if (rune <= 0xFFFF)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory((char)rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) return 0;
        }

        return 1;
    }

    private static bool IsWide(int r) =>
        r is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3041 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xA000 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1F64F
            or >= 0x1F900 and <= 0x1F9FF
            or >= 0x20000 and <= 0x3FFFD;
}
