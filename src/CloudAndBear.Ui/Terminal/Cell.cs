namespace CloudAndBear.Ui.Terminal;

[Flags]
public enum CellAttr : ushort
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Inverse = 16,
    Strike = 32,
    Hidden = 64,

    /// <summary>Prva polovina znaka koji zauzima dve ćelije (kineski, emodži).</summary>
    Wide = 128,

    /// <summary>Druga polovina širokog znaka: ne crta se sama.</summary>
    Continuation = 256,
}

/// <summary>Jedna ćelija ekrana terminala.</summary>
public struct Cell
{
    public int Rune;
    public int Fg;
    public int Bg;
    public CellAttr Attr;

    public static Cell Blank(int bg = TermColor.Default) => new() { Rune = ' ', Fg = TermColor.Default, Bg = bg };
}

/// <summary>
/// Boja ćelije u jednom intu: -1 podrazumevana, 0..255 mesto u paleti (0-15 osnovne, 16-231 kocka, 232-255 sivo),
/// a od 0x1000000 naviše prava boja (0x1RRGGBB).
/// </summary>
public static class TermColor
{
    public const int Default = -1;

    public static int Rgb(int r, int g, int b) => 0x1000000 | (r << 16) | (g << 8) | b;

    public static bool IsRgb(int color) => color >= 0x1000000;

    public static bool IsIndexed(int color) => color is >= 0 and < 256;

    /// <summary>RGB za mesto u paleti 16..255 (0..15 zavise od teme, pa ih daje kontrola).</summary>
    public static int CubeOrGray(int index)
    {
        if (index >= 232)
        {
            var v = 8 + (index - 232) * 10;
            return Rgb(v, v, v);
        }

        var i = index - 16;
        int Level(int n) => n == 0 ? 0 : 55 + n * 40;
        return Rgb(Level(i / 36), Level(i / 6 % 6), Level(i % 6));
    }
}
