using System.Text;

namespace CloudAndBear.Ui.Terminal;

/// <summary>
/// Emulator terminala (podskup xterm-a): čita bajtove programa (UTF-8, ESC i CSI sekvence, OSC naslov) i menja
/// <see cref="TerminalScreen"/>. Odgovore koje programi traže (pozicija kursora, vrsta terminala) vraća preko
/// <see cref="Response"/>. Sve na jednoj niti.
/// </summary>
public sealed class TerminalEmulator
{
    private enum State { Ground, Escape, EscapeIntermediate, CsiEntry, CsiParam, CsiIntermediate, CsiIgnore, Osc, StringIgnore }

    private const int MaxParams = 16;
    private const int MaxOsc = 4096;

    private readonly int[] _params = new int[MaxParams];
    private readonly List<byte> _osc = new();
    private State _state = State.Ground;
    private int _paramCount;
    private int _cur;
    private bool _hasParam;
    private char _private;
    private char _interm;
    private char _escIntermediate;

    private int _utf8Need;
    private int _utf8Acc;
    private int _utf8Min;
    private int _lastRune;

    public TerminalEmulator(int cols, int rows)
    {
        Screen = new TerminalScreen(cols, rows);
    }

    public TerminalScreen Screen { get; }

    public string Title { get; private set; } = "";

    /// <summary>Režim praćenja miša koji je program tražio: 0 nema, 1000 klikovi, 1002 prevlačenje, 1003 sve.</summary>
    public int MouseMode { get; private set; }

    /// <summary>Miš se javlja u SGR obliku (režim 1006).</summary>
    public bool MouseSgr { get; private set; }

    public bool FocusReporting { get; private set; }

    public event Action<string>? TitleChanged;

    /// <summary>Bajtovi koje treba poslati programu (odgovor na upit).</summary>
    public event Action<byte[]>? Response;

    public event Action? Bell;

    public void Feed(ReadOnlySpan<byte> data)
    {
        foreach (var b in data) Step(b);
    }

    public void Feed(string text) => Feed(Encoding.UTF8.GetBytes(text));

    public void Resize(int cols, int rows) => Screen.Resize(cols, rows);

    // ---------- automat ----------

    private void Step(byte b)
    {
        if (_state is State.Osc or State.StringIgnore)
        {
            StringByte(b);
            return;
        }

        if (b == 0x1B)
        {
            _utf8Need = 0;
            _state = State.Escape;
            return;
        }

        if (b is 0x18 or 0x1A)
        {
            _state = State.Ground;
            return;
        }

        if (b < 0x20)
        {
            _utf8Need = 0;
            Execute(b);
            return;
        }

        switch (_state)
        {
            case State.Ground: GroundByte(b); break;
            case State.Escape: EscapeByte(b); break;
            case State.EscapeIntermediate: EscapeIntermediateByte(b); break;
            default: CsiByte(b); break;
        }
    }

    private void Execute(byte b)
    {
        switch (b)
        {
            case 0x07: Bell?.Invoke(); break;
            case 0x08: Screen.Backspace(); break;
            case 0x09: Screen.Tab(); break;
            case 0x0A or 0x0B or 0x0C: Screen.LineFeed(); break;
            case 0x0D: Screen.CarriageReturn(); break;
            case 0x0E: Screen.ShiftCharset(1); break;
            case 0x0F: Screen.ShiftCharset(0); break;
        }
    }

    private void GroundByte(byte b)
    {
        if (b < 0x80)
        {
            _utf8Need = 0;
            if (b != 0x7F) PrintRune(b);
            return;
        }

        if (_utf8Need == 0)
        {
            if (b is >= 0xC2 and <= 0xDF) { _utf8Acc = b & 0x1F; _utf8Need = 1; _utf8Min = 0x80; }
            else if (b is >= 0xE0 and <= 0xEF) { _utf8Acc = b & 0x0F; _utf8Need = 2; _utf8Min = 0x800; }
            else if (b is >= 0xF0 and <= 0xF4) { _utf8Acc = b & 0x07; _utf8Need = 3; _utf8Min = 0x10000; }
            else PrintRune(0xFFFD);
            return;
        }

        if ((b & 0xC0) != 0x80)
        {
            // prekinut višebajtni znak: javi grešku i obradi bajt iznova
            _utf8Need = 0;
            PrintRune(0xFFFD);
            GroundByte(b);
            return;
        }

        _utf8Acc = (_utf8Acc << 6) | (b & 0x3F);
        if (--_utf8Need > 0) return;

        var rune = _utf8Acc;
        if (rune < _utf8Min || rune > 0x10FFFF || rune is >= 0xD800 and <= 0xDFFF) rune = 0xFFFD;
        PrintRune(rune);
    }

    private void PrintRune(int rune)
    {
        _lastRune = rune;
        Screen.Print(rune);
    }

    private void EscapeByte(byte b)
    {
        switch ((char)b)
        {
            case '[':
                _paramCount = 0;
                _cur = 0;
                _hasParam = false;
                _private = '\0';
                _interm = '\0';
                _state = State.CsiEntry;
                return;
            case ']':
                _osc.Clear();
                _state = State.Osc;
                return;
            case 'P' or 'X' or '^' or '_':
                _state = State.StringIgnore;
                return;
            case '(' or ')' or '*' or '+' or '#' or '%' or ' ':
                _escIntermediate = (char)b;
                _state = State.EscapeIntermediate;
                return;
        }

        switch ((char)b)
        {
            case '7': Screen.SaveCursor(); break;
            case '8': Screen.RestoreCursor(); break;
            case 'D': Screen.LineFeed(); break;
            case 'E': Screen.NextLine(); break;
            case 'H': Screen.SetTabStop(); break;
            case 'M': Screen.ReverseIndex(); break;
            case 'c': Screen.FullReset(); break;
            case 'Z': Reply("\x1b[?62;22c"); break;
        }

        _state = State.Ground;
    }

    private void EscapeIntermediateByte(byte b)
    {
        if (b is >= 0x20 and <= 0x2F) return;   // još međuznakova

        switch (_escIntermediate)
        {
            case '(': Screen.DesignateCharset(0, b == '0' ? '0' : 'B'); break;
            case ')': Screen.DesignateCharset(1, b == '0' ? '0' : 'B'); break;
            case '#' when b == '8':
                // DECALN: ceo ekran slovom E (proba poravnanja)
                for (var y = 0; y < Screen.Rows; y++)
                    for (var x = 0; x < Screen.Cols; x++)
                        Screen.At(x, y) = new Cell { Rune = 'E', Fg = TermColor.Default, Bg = TermColor.Default };
                Screen.Touch();
                break;
        }

        _state = State.Ground;
    }

    private void CsiByte(byte b)
    {
        var ignore = _state == State.CsiIgnore;

        if (b is >= (byte)'0' and <= (byte)'9')
        {
            if (_state == State.CsiIntermediate) _state = State.CsiIgnore;
            else if (!ignore)
            {
                _cur = Math.Min(_cur * 10 + (b - '0'), 99999);
                _hasParam = true;
                _state = State.CsiParam;
            }

            return;
        }

        if (b is (byte)';' or (byte)':')
        {
            if (_state == State.CsiIntermediate) _state = State.CsiIgnore;
            else if (!ignore)
            {
                PushParam();
                _state = State.CsiParam;
            }

            return;
        }

        if (b is >= 0x3C and <= 0x3F)
        {
            if (_state == State.CsiEntry)
            {
                _private = (char)b;
                _state = State.CsiParam;
            }
            else _state = State.CsiIgnore;

            return;
        }

        if (b is >= 0x20 and <= 0x2F)
        {
            if (!ignore)
            {
                if (_interm == '\0') _interm = (char)b;
                _state = State.CsiIntermediate;
            }

            return;
        }

        if (b is >= 0x40 and <= 0x7E)
        {
            if (!ignore)
            {
                if (_hasParam || _paramCount > 0) PushParam();
                DispatchCsi((char)b);
            }

            _state = State.Ground;
        }
    }

    private void PushParam()
    {
        if (_paramCount < MaxParams) _params[_paramCount++] = _hasParam ? _cur : -1;
        _cur = 0;
        _hasParam = false;
    }

    private int Get(int index, int fallback) => index < _paramCount && _params[index] >= 0 ? _params[index] : fallback;

    // nula i izostavljen parametar znače 1 (pomeranje, broj redova...)
    private int Count(int index) => Math.Max(1, Get(index, 1));

    private void StringByte(byte b)
    {
        if (b == 0x1B)
        {
            if (_state == State.Osc) DispatchOsc();
            _state = State.Escape;
            return;
        }

        if (b is 0x18 or 0x1A)
        {
            _state = State.Ground;
            return;
        }

        if (_state != State.Osc) return;

        if (b == 0x07)
        {
            DispatchOsc();
            _state = State.Ground;
            return;
        }

        if (_osc.Count < MaxOsc) _osc.Add(b);
    }

    // ---------- naredbe ----------

    private void DispatchCsi(char final)
    {
        if (_private == '?')
        {
            switch (final)
            {
                case 'h': SetPrivateModes(true); break;
                case 'l': SetPrivateModes(false); break;
                case 'n' when Get(0, 0) == 6:
                    Reply($"\x1b[?{Screen.CursorY + 1};{Screen.CursorX + 1}R");
                    break;
            }

            return;
        }

        if (_private == '>')
        {
            if (final == 'c') Reply("\x1b[>1;10;0c");
            return;
        }

        if (_private != '\0') return;

        if (_interm == ' ' && final == 'q')
        {
            Screen.CursorStyle = Get(0, 0);
            return;
        }

        if (_interm == '!' && final == 'p')
        {
            SoftReset();
            return;
        }

        if (_interm != '\0') return;

        switch (final)
        {
            case '@': Screen.InsertChars(Count(0)); break;
            case 'A': Screen.MoveUp(Count(0)); break;
            case 'B' or 'e': Screen.MoveDown(Count(0)); break;
            case 'C' or 'a': Screen.MoveRight(Count(0)); break;
            case 'D': Screen.MoveLeft(Count(0)); break;
            case 'E': Screen.MoveDown(Count(0)); Screen.SetColumn(0); break;
            case 'F': Screen.MoveUp(Count(0)); Screen.SetColumn(0); break;
            case 'G' or '`': Screen.SetColumn(Count(0) - 1); break;
            case 'H' or 'f': Screen.MoveTo(Count(0) - 1, Count(1) - 1); break;
            case 'I': Screen.Tab(Count(0)); break;
            case 'J': Screen.EraseInDisplay(Get(0, 0)); break;
            case 'K': Screen.EraseInLine(Get(0, 0)); break;
            case 'L': Screen.InsertLines(Count(0)); break;
            case 'M': Screen.DeleteLines(Count(0)); break;
            case 'P': Screen.DeleteChars(Count(0)); break;
            case 'S': Screen.ScrollUp(Count(0)); break;
            case 'T' when _paramCount <= 1: Screen.ScrollDown(Count(0)); break;
            case 'X': Screen.EraseChars(Count(0)); break;
            case 'Z': Screen.BackTab(Count(0)); break;
            case 'b':
                for (var i = 0; i < Math.Min(Count(0), 4096) && _lastRune != 0; i++) Screen.Print(_lastRune);
                break;
            case 'c' when Get(0, 0) == 0: Reply("\x1b[?62;22c"); break;
            case 'd': Screen.SetRow(Count(0) - 1); break;
            case 'g': Screen.ClearTabStop(Get(0, 0) == 3); break;
            case 'h': SetAnsiModes(true); break;
            case 'l': SetAnsiModes(false); break;
            case 'm': Sgr(); break;
            case 'n':
                if (Get(0, 0) == 5) Reply("\x1b[0n");
                else if (Get(0, 0) == 6) Reply($"\x1b[{Screen.CursorY + 1};{Screen.CursorX + 1}R");
                break;
            case 'r': Screen.SetScrollRegion(Count(0) - 1, Get(1, Screen.Rows) - 1); break;
            case 's' when _paramCount == 0: Screen.SaveCursor(); break;
            case 'u': Screen.RestoreCursor(); break;
        }
    }

    private void SetAnsiModes(bool on)
    {
        for (var i = 0; i < _paramCount; i++)
        {
            if (_params[i] == 4) Screen.InsertMode = on;
        }
    }

    private void SetPrivateModes(bool on)
    {
        for (var i = 0; i < _paramCount; i++)
        {
            switch (_params[i])
            {
                case 1: Screen.AppCursorKeys = on; break;
                case 6: Screen.OriginMode = on; Screen.MoveTo(0, 0); break;
                case 7: Screen.AutoWrap = on; break;
                case 25: Screen.CursorVisible = on; break;
                case 47 or 1047: Screen.SetAltScreen(on, saveCursor: false); break;
                case 1048:
                    if (on) Screen.SaveCursor();
                    else Screen.RestoreCursor();
                    break;
                case 1049: Screen.SetAltScreen(on, saveCursor: true); break;
                case 1000 or 1002 or 1003: MouseMode = on ? _params[i] : 0; break;
                case 1004: FocusReporting = on; break;
                case 1006: MouseSgr = on; break;
                case 2004: Screen.BracketedPaste = on; break;
            }
        }

        Screen.Touch();
    }

    private void SoftReset()
    {
        Screen.PenFg = Screen.PenBg = TermColor.Default;
        Screen.PenAttr = CellAttr.None;
        Screen.AutoWrap = true;
        Screen.OriginMode = false;
        Screen.InsertMode = false;
        Screen.CursorVisible = true;
        Screen.AppCursorKeys = false;
        Screen.SetScrollRegion(-1, -1);
    }

    private void Sgr()
    {
        if (_paramCount == 0)
        {
            ResetPen();
            return;
        }

        for (var i = 0; i < _paramCount; i++)
        {
            var p = Math.Max(_params[i], 0);
            switch (p)
            {
                case 0: ResetPen(); break;
                case 1: Screen.PenAttr |= CellAttr.Bold; break;
                case 2: Screen.PenAttr |= CellAttr.Dim; break;
                case 3: Screen.PenAttr |= CellAttr.Italic; break;
                case 4 or 21: Screen.PenAttr |= CellAttr.Underline; break;
                case 7: Screen.PenAttr |= CellAttr.Inverse; break;
                case 8: Screen.PenAttr |= CellAttr.Hidden; break;
                case 9: Screen.PenAttr |= CellAttr.Strike; break;
                case 22: Screen.PenAttr &= ~(CellAttr.Bold | CellAttr.Dim); break;
                case 23: Screen.PenAttr &= ~CellAttr.Italic; break;
                case 24: Screen.PenAttr &= ~CellAttr.Underline; break;
                case 27: Screen.PenAttr &= ~CellAttr.Inverse; break;
                case 28: Screen.PenAttr &= ~CellAttr.Hidden; break;
                case 29: Screen.PenAttr &= ~CellAttr.Strike; break;
                case >= 30 and <= 37: Screen.PenFg = p - 30; break;
                case 38: Screen.PenFg = ExtendedColor(ref i, Screen.PenFg); break;
                case 39: Screen.PenFg = TermColor.Default; break;
                case >= 40 and <= 47: Screen.PenBg = p - 40; break;
                case 48: Screen.PenBg = ExtendedColor(ref i, Screen.PenBg); break;
                case 49: Screen.PenBg = TermColor.Default; break;
                case >= 90 and <= 97: Screen.PenFg = p - 90 + 8; break;
                case >= 100 and <= 107: Screen.PenBg = p - 100 + 8; break;
            }
        }
    }

    // 38;5;n  38;2;r;g;b  (i oblik sa dve tačke, 38:2::r:g:b, sa praznim identifikatorom prostora boja)
    private int ExtendedColor(ref int i, int current)
    {
        if (i + 1 >= _paramCount) return current;

        var mode = _params[i + 1];
        if (mode == 5 && i + 2 < _paramCount)
        {
            var index = Math.Clamp(_params[i + 2], 0, 255);
            i += 2;
            return index;
        }

        if (mode == 2)
        {
            var j = i + 2;
            if (_paramCount - j >= 4 && _params[j] < 0) j++;
            if (j + 2 >= _paramCount) { i = _paramCount; return current; }

            var color = TermColor.Rgb(Math.Clamp(_params[j], 0, 255), Math.Clamp(_params[j + 1], 0, 255), Math.Clamp(_params[j + 2], 0, 255));
            i = j + 2;
            return color;
        }

        return current;
    }

    private void ResetPen()
    {
        Screen.PenFg = Screen.PenBg = TermColor.Default;
        Screen.PenAttr = CellAttr.None;
    }

    private void DispatchOsc()
    {
        var text = Encoding.UTF8.GetString(_osc.ToArray());
        var semi = text.IndexOf(';');
        if (semi <= 0 || !int.TryParse(text.AsSpan(0, semi), out var code)) return;

        if (code is 0 or 2)
        {
            Title = text[(semi + 1)..];
            TitleChanged?.Invoke(Title);
        }
    }

    private void Reply(string text) => Response?.Invoke(Encoding.ASCII.GetBytes(text));
}
