using Avalonia.Input;
using Avalonia.Input.Raw;
using CloudAndBear.Ui.Input;

namespace CloudAndBear.Device;

/// <summary>Šta jedan događaj tastature znači za Avaloniju.</summary>
/// <param name="Key">Taster za prečice (na QWERTZ rasporedu taster sa Z je Key.Z).</param>
/// <param name="KeySymbol">Znak za prečice sa Alt-om (Alt+B daje "b"); null za Ctrl i Win kombinacije.</param>
/// <param name="Text">Uneti tekst; null kad je pritisnut Ctrl, Win ili levi Alt.</param>
public readonly record struct KeyResult(
    Key Key, PhysicalKey Physical, RawInputModifiers Modifiers, string? KeySymbol, string? Text, bool Down);

/// <summary>
/// Pretvara Linux kodove tastera (evdev) u događaje tastature: modifikatori, Caps Lock, AltGr, izabrani raspored i
/// promena rasporeda sa Alt+Shift. Bez ikakvog ulaza/izlaza, pa se proverava običnim testom.
/// </summary>
public sealed class KeyTranslator
{
    private readonly IKeyboardLayoutService _layouts;
    private readonly Action _toggleLayout;
    private RawInputModifiers _mods;
    private bool _caps, _leftAlt, _altGr, _rightAltIsAlt, _togglePending;

    public KeyTranslator(IKeyboardLayoutService layouts, Action? toggleLayout = null)
    {
        _layouts = layouts;
        _toggleLayout = toggleLayout ?? layouts.Next;
    }

    /// <summary>Tastatura je isključena dok je neki taster bio pritisnut: bez ovoga bi Shift ili Alt ostali "zaglavljeni".</summary>
    public void ReleaseAll()
    {
        _mods = RawInputModifiers.None;
        _leftAlt = _altGr = _rightAltIsAlt = _togglePending = false;
    }

    /// <summary>Događaj tastera (value: 0 pušten, 1 pritisnut, 2 ponavljanje); null za taster koji ne poznajemo.</summary>
    public KeyResult? OnKey(int code, int value)
    {
        if (!Keys.TryGetValue(code, out var k)) return null;

        var down = value != 0;
        var modifier = k.Modifier;

        // Desni Alt je AltGr (treći nivo slova) na srpskim rasporedima; na US rasporedu je običan Alt (prečice)
        if (code == 100)
        {
            if (down && value == 1) _rightAltIsAlt = _layouts.Current == KeyboardLayout.Us;
            _altGr = down;
            if (!_rightAltIsAlt) modifier = RawInputModifiers.None;
        }

        if (modifier != RawInputModifiers.None)
            _mods = down ? _mods | modifier : _mods & ~modifier;
        if (code == 58 && value == 1) _caps = !_caps;   // Caps Lock
        if (code == 56) _leftAlt = down;

        TrackLayoutToggle(code, value);

        var letter = Letter(k, code);
        var ctrlOrMeta = (_mods & (RawInputModifiers.Control | RawInputModifiers.Meta)) != 0;
        var alt = (_mods & RawInputModifiers.Alt) != 0;   // AltGr na srpskom rasporedu nije Alt: njegova slova se upisuju
        var text = down && !ctrlOrMeta && !alt ? letter : null;
        var symbol = down && !ctrlOrMeta ? letter : null;
        return new KeyResult(LogicalKey(k, code), k.Physical, _mods, symbol, text, down);
    }

    // Alt+Shift: raspored se menja kad se oba puste, ako između nije pritisnut nijedan drugi taster
    private void TrackLayoutToggle(int code, int value)
    {
        var shiftKey = code is 42 or 54;
        if (value == 1)
        {
            if ((code == 56 && _mods.HasFlag(RawInputModifiers.Shift)) || (shiftKey && _leftAlt)) _togglePending = true;
            else if (code != 56 && !shiftKey) _togglePending = false;
        }
        else if (value == 0 && _togglePending && (code == 56 || shiftKey))
        {
            _togglePending = false;
            _toggleLayout();
        }
    }

    // slovo koje taster daje u izabranom rasporedu (null za tastere bez slova)
    private string? Letter(KeyInfo k, int code)
    {
        var shift = _mods.HasFlag(RawInputModifiers.Shift);
        string? normal = k.Normal, shifted = k.Shifted, altGr = null, shiftAltGr = null;
        if (_layouts.Current.TryGet(code, out var l))
            (normal, shifted, altGr, shiftAltGr) = (l.Normal, l.Shifted, l.AltGr, l.ShiftAltGr);

        if (_altGr && !_rightAltIsAlt) return shift ? shiftAltGr ?? altGr : altGr;   // treći nivo, null kad ga taster nema

        var isLetter = normal is { Length: 1 } n && char.IsLetter(n[0]);
        return shift ^ (_caps && isLetter) ? shifted : normal;
    }

    // Key za prečice: na QWERTZ rasporedu taster sa slovom Z je Key.Z, iako je fizički tamo gde je na US Y
    private Key LogicalKey(KeyInfo k, int code) =>
        _layouts.Current.TryGet(code, out var l) && l.Normal is [>= 'a' and <= 'z' and var c]
            ? Enum.Parse<Key>(char.ToUpper(c).ToString())
            : k.Key;

    // ---- tabela tastera ----

    private readonly record struct KeyInfo(Key Key, PhysicalKey Physical, string? Normal, string? Shifted,
        RawInputModifiers Modifier = RawInputModifiers.None);

    // Linux kod tastera -> Avalonijin Key, fizički taster i slova (US raspored)
    private static readonly Dictionary<int, KeyInfo> Keys = BuildKeys();

    private static Dictionary<int, KeyInfo> BuildKeys()
    {
        var m = new Dictionary<int, KeyInfo>();

        void Letters(int first, string letters)
        {
            for (var i = 0; i < letters.Length; i++)
            {
                var up = char.ToUpper(letters[i]).ToString();
                m[first + i] = new(Enum.Parse<Key>(up), Enum.Parse<PhysicalKey>(up), letters[i].ToString(), up);
            }
        }

        Letters(16, "qwertyuiop");
        Letters(30, "asdfghjkl");
        Letters(44, "zxcvbnm");

        const string shiftedDigits = "!@#$%^&*()";
        for (var i = 0; i < 10; i++)   // kodovi 2..11 su 1,2,...,9,0
        {
            var d = (i + 1) % 10;
            m[2 + i] = new(Enum.Parse<Key>("D" + d), Enum.Parse<PhysicalKey>("Digit" + d), d.ToString(), shiftedDigits[i].ToString());
        }

        void P(int code, Key key, PhysicalKey phys, string normal, string shifted) => m[code] = new(key, phys, normal, shifted);
        P(12, Key.OemMinus, PhysicalKey.Minus, "-", "_");
        P(13, Key.OemPlus, PhysicalKey.Equal, "=", "+");
        P(26, Key.OemOpenBrackets, PhysicalKey.BracketLeft, "[", "{");
        P(27, Key.OemCloseBrackets, PhysicalKey.BracketRight, "]", "}");
        P(39, Key.OemSemicolon, PhysicalKey.Semicolon, ";", ":");
        P(40, Key.OemQuotes, PhysicalKey.Quote, "'", "\"");
        P(41, Key.OemTilde, PhysicalKey.Backquote, "`", "~");
        P(43, Key.OemPipe, PhysicalKey.Backslash, "\\", "|");
        P(51, Key.OemComma, PhysicalKey.Comma, ",", "<");
        P(52, Key.OemPeriod, PhysicalKey.Period, ".", ">");
        P(53, Key.OemQuestion, PhysicalKey.Slash, "/", "?");
        P(57, Key.Space, PhysicalKey.Space, " ", " ");
        P(86, Key.OemBackslash, PhysicalKey.IntlBackslash, "\\", "|");   // dodatni taster na ISO tastaturama (pored levog Shift-a)

        void S(int code, Key key, PhysicalKey phys, RawInputModifiers mod = RawInputModifiers.None) => m[code] = new(key, phys, null, null, mod);
        S(1, Key.Escape, PhysicalKey.Escape);
        S(14, Key.Back, PhysicalKey.Backspace);
        S(15, Key.Tab, PhysicalKey.Tab);
        S(28, Key.Enter, PhysicalKey.Enter);
        S(58, Key.CapsLock, PhysicalKey.CapsLock);
        S(102, Key.Home, PhysicalKey.Home);
        S(103, Key.Up, PhysicalKey.ArrowUp);
        S(104, Key.PageUp, PhysicalKey.PageUp);
        S(105, Key.Left, PhysicalKey.ArrowLeft);
        S(106, Key.Right, PhysicalKey.ArrowRight);
        S(107, Key.End, PhysicalKey.End);
        S(108, Key.Down, PhysicalKey.ArrowDown);
        S(109, Key.PageDown, PhysicalKey.PageDown);
        S(110, Key.Insert, PhysicalKey.Insert);
        S(111, Key.Delete, PhysicalKey.Delete);
        S(42, Key.LeftShift, PhysicalKey.ShiftLeft, RawInputModifiers.Shift);
        S(54, Key.RightShift, PhysicalKey.ShiftRight, RawInputModifiers.Shift);
        S(29, Key.LeftCtrl, PhysicalKey.ControlLeft, RawInputModifiers.Control);
        S(97, Key.RightCtrl, PhysicalKey.ControlRight, RawInputModifiers.Control);
        S(56, Key.LeftAlt, PhysicalKey.AltLeft, RawInputModifiers.Alt);
        S(100, Key.RightAlt, PhysicalKey.AltRight, RawInputModifiers.Alt);
        S(125, Key.LWin, PhysicalKey.MetaLeft, RawInputModifiers.Meta);
        S(126, Key.RWin, PhysicalKey.MetaRight, RawInputModifiers.Meta);

        for (var i = 0; i < 10; i++)   // F1..F10 su kodovi 59..68
            S(59 + i, Enum.Parse<Key>("F" + (i + 1)), Enum.Parse<PhysicalKey>("F" + (i + 1)));
        S(87, Key.F11, PhysicalKey.F11);
        S(88, Key.F12, PhysicalKey.F12);

        return m;
    }
}
