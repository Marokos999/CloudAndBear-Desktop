namespace CloudAndBear.Ui.Input;

/// <summary>
/// Raspored tastature: koje slovo daje koji taster. US je osnova (tabela u uređaju); ostali rasporedi zamenjuju samo
/// tastere koji se razlikuju. AltGr je desni Alt. Brojevi su Linux kodovi tastera: 2..11 cifre, 16.. gornji red, 30.. srednji, 44.. donji.
/// </summary>
public sealed class KeyboardLayout
{
    /// <summary>Slova koja taster daje: bez Shift-a, sa Shift-om, sa AltGr i sa AltGr+Shift (kad nije zadato, važi AltGr).</summary>
    public readonly record struct Letters(string Normal, string Shifted, string? AltGr = null, string? ShiftAltGr = null);

    private readonly Dictionary<int, Letters> _keys;

    private KeyboardLayout(string name, Dictionary<int, Letters> keys)
    {
        Name = name;
        _keys = keys;
    }

    public string Name { get; }

    public bool TryGet(int code, out Letters letters) => _keys.TryGetValue(code, out letters);

    /// <summary>Slova kao na američkoj tastaturi.</summary>
    public static readonly KeyboardLayout Us = new("US", new());

    /// <summary>Srpska latinica kao na Windowsu (QWERTZ): z i y zamenjuju mesta, š đ č ć ž su na [ ] ; ' \, a interpunkcija je drugačija.</summary>
    public static readonly KeyboardLayout Serbian = new("SR", new()
    {
        [2] = new("1", "!", "~"),
        [3] = new("2", "\""),
        [4] = new("3", "#", "^"),
        [5] = new("4", "$"),
        [6] = new("5", "%"),
        [7] = new("6", "&"),
        [8] = new("7", "/", "`"),
        [9] = new("8", "("),
        [10] = new("9", ")"),
        [11] = new("0", "="),
        [12] = new("'", "?"),
        [13] = new("+", "*"),
        [16] = new("q", "Q", "\\"),
        [17] = new("w", "W", "|"),
        [18] = new("e", "E", "€"),
        [21] = new("z", "Z"),
        [26] = new("š", "Š", "÷"),
        [27] = new("đ", "Đ", "×"),
        [33] = new("f", "F", "["),
        [34] = new("g", "G", "]"),
        [39] = new("č", "Č"),
        [40] = new("ć", "Ć", "ß"),
        [43] = new("ž", "Ž", "¤"),
        [44] = new("y", "Y"),
        [47] = new("v", "V", "@"),
        [48] = new("b", "B", "{"),
        [49] = new("n", "N", "}"),
        [50] = new("m", "M", "§"),
        [51] = new(",", ";", "<"),
        [52] = new(".", ":", ">"),
        [53] = new("-", "_"),
        [86] = new("<", ">"),
    });

    /// <summary>Američka tastatura sa srpskim slovima: sve je kao na US, samo [ ] ; ' \ daju š đ č ć ž, a AltGr vraća staro značenje.</summary>
    public static readonly KeyboardLayout SerbianUs = new("SR-US", new()
    {
        [26] = new("š", "Š", "[", "{"),
        [27] = new("đ", "Đ", "]", "}"),
        [39] = new("č", "Č", ";", ":"),
        [40] = new("ć", "Ć", "'", "\""),
        [43] = new("ž", "Ž", "\\", "|"),
    });

    public static IReadOnlyList<KeyboardLayout> All { get; } = [Us, Serbian, SerbianUs];
}
