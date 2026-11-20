namespace CloudAndBear.Ui.Terminal;

/// <summary>
/// Ekran terminala: mreža ćelija, kursor, oblast skrolovanja, alternativni ekran i istorija.
/// Zna samo da menja stanje (ne zna za bajtove i sekvence: to radi <see cref="TerminalEmulator"/>).
/// Sve se radi na jednoj niti (UI).
/// </summary>
public sealed class TerminalScreen
{
    public const int MaxScrollback = 3000;

    // DEC grafika (ESC ( 0): crtanje okvira u ncurses programima
    private const string DecGraphics = " ◆▒␉␌␍␊°±␤␋┘┐┌└┼⎺⎻─⎼⎽├┤┴┬│≤≥π≠£·";   // 0x5F..0x7E

    private readonly List<Cell[]> _scrollback = new();
    private Cell[][] _main;
    private Cell[][] _alt;
    private bool _altActive;
    private Cell[][] _rows;
    private bool[] _tabs = [];
    private int _top;
    private int _bottom;
    private bool _wrapPending;
    private readonly char[] _charsets = ['B', 'B'];
    private int _glSet;
    private SavedCursor _saved;
    private SavedCursor _savedMain;

    private struct SavedCursor
    {
        public int X, Y, Fg, Bg;
        public CellAttr Attr;
        public bool WrapPending, Origin;
        public char G0, G1;
        public int Gl;
    }

    public TerminalScreen(int cols, int rows)
    {
        Cols = Math.Max(1, cols);
        Rows = Math.Max(1, rows);
        _main = NewGrid(Cols, Rows);
        _alt = NewGrid(Cols, Rows);
        _rows = _main;
        _bottom = Rows - 1;
        ResetTabs();
    }

    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public int CursorX { get; private set; }
    public int CursorY { get; private set; }

    /// <summary>Raste sa svakom izmenom: kontrola crta samo kad se promeni.</summary>
    public int Version { get; private set; }

    // pero: boja i atributi sledećih znakova
    public int PenFg { get; set; } = TermColor.Default;
    public int PenBg { get; set; } = TermColor.Default;
    public CellAttr PenAttr { get; set; }

    // režimi
    public bool AutoWrap { get; set; } = true;
    public bool OriginMode { get; set; }
    public bool InsertMode { get; set; }
    public bool CursorVisible { get; set; } = true;
    public bool AppCursorKeys { get; set; }
    public bool BracketedPaste { get; set; }
    public int CursorStyle { get; set; }
    public bool IsAltScreen => _altActive;

    public int ScrollbackCount => _scrollback.Count;
    public int TotalLines => _scrollback.Count + Rows;

    public void Touch() => Version++;

    // ---------- čitanje ----------

    public ref Cell At(int x, int y) => ref _rows[y][x];

    /// <summary>Linija po apsolutnom redu: prvo istorija, pa ekran. Istorija može biti druge širine.</summary>
    public Cell[] GetLine(int absoluteLine) =>
        absoluteLine < _scrollback.Count ? _scrollback[absoluteLine] : _rows[Math.Clamp(absoluteLine - _scrollback.Count, 0, Rows - 1)];

    public string GetLineText(int absoluteLine)
    {
        var line = GetLine(absoluteLine);
        var sb = new System.Text.StringBuilder(line.Length);
        foreach (var cell in line)
        {
            if ((cell.Attr & CellAttr.Continuation) != 0) continue;
            sb.Append(cell.Rune == 0 ? " " : char.ConvertFromUtf32(cell.Rune));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Ceo ekran kao tekst (za testove): linije bez završnih razmaka.</summary>
    public string ScreenText()
    {
        var lines = Enumerable.Range(0, Rows).Select(y => GetLineText(_scrollback.Count + y)).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join("\n", lines);
    }

    // ---------- ispis ----------

    public void Print(int rune)
    {
        // grafika linija ako je izabran DEC skup
        if (_charsets[_glSet] == '0' && rune is >= 0x5F and <= 0x7E) rune = DecGraphics[rune - 0x5F];

        var width = CharWidth.Of(rune);
        if (width == 0) return;

        if (_wrapPending && AutoWrap)
        {
            CarriageReturn();
            LineFeed();
        }

        _wrapPending = false;

        if (width == 2 && CursorX >= Cols - 1)
        {
            // široki znak ne staje u poslednju kolonu
            if (AutoWrap)
            {
                _rows[CursorY][CursorX] = Cell.Blank(PenBg);
                CarriageReturn();
                LineFeed();
            }
            else
            {
                return;
            }
        }

        if (InsertMode) InsertChars(width);

        BreakWide(CursorX, CursorY);
        if (width == 2) BreakWide(CursorX + 1, CursorY);

        _rows[CursorY][CursorX] = new Cell { Rune = rune, Fg = PenFg, Bg = PenBg, Attr = PenAttr | (width == 2 ? CellAttr.Wide : 0) };
        if (width == 2)
            _rows[CursorY][CursorX + 1] = new Cell { Rune = 0, Fg = PenFg, Bg = PenBg, Attr = PenAttr | CellAttr.Continuation };

        CursorX += width;
        if (CursorX >= Cols)
        {
            CursorX = Cols - 1;
            _wrapPending = AutoWrap;
        }

        Version++;
    }

    // ako se piše preko polovine širokog znaka, druga polovina se briše
    private void BreakWide(int x, int y)
    {
        if (x < 0 || x >= Cols) return;

        var row = _rows[y];
        if ((row[x].Attr & CellAttr.Continuation) != 0 && x > 0) row[x - 1] = Cell.Blank(row[x - 1].Bg);
        if ((row[x].Attr & CellAttr.Wide) != 0 && x + 1 < Cols) row[x + 1] = Cell.Blank(row[x + 1].Bg);
    }

    // ---------- kursor ----------

    public void CarriageReturn()
    {
        CursorX = 0;
        _wrapPending = false;
        Version++;
    }

    public void Backspace()
    {
        if (CursorX > 0) CursorX--;
        _wrapPending = false;
        Version++;
    }

    public void LineFeed()
    {
        if (CursorY == _bottom) ScrollUp(1);
        else if (CursorY < Rows - 1) CursorY++;

        _wrapPending = false;
        Version++;
    }

    public void ReverseIndex()
    {
        if (CursorY == _top) ScrollDown(1);
        else if (CursorY > 0) CursorY--;

        _wrapPending = false;
        Version++;
    }

    public void NextLine()
    {
        CarriageReturn();
        LineFeed();
    }

    public void MoveTo(int row, int col)
    {
        // brojevi su od nule; u režimu porekla (DECOM) red se računa od vrha oblasti skrolovanja
        if (OriginMode)
        {
            CursorY = Math.Clamp(_top + row, _top, _bottom);
        }
        else
        {
            CursorY = Math.Clamp(row, 0, Rows - 1);
        }

        CursorX = Math.Clamp(col, 0, Cols - 1);
        _wrapPending = false;
        Version++;
    }

    public void SetColumn(int col)
    {
        CursorX = Math.Clamp(col, 0, Cols - 1);
        _wrapPending = false;
        Version++;
    }

    public void SetRow(int row) => MoveTo(row, CursorX);

    public void MoveUp(int n)
    {
        var limit = CursorY >= _top ? _top : 0;
        CursorY = Math.Max(limit, CursorY - Math.Max(1, n));
        _wrapPending = false;
        Version++;
    }

    public void MoveDown(int n)
    {
        var limit = CursorY <= _bottom ? _bottom : Rows - 1;
        CursorY = Math.Min(limit, CursorY + Math.Max(1, n));
        _wrapPending = false;
        Version++;
    }

    public void MoveRight(int n) => SetColumn(CursorX + Math.Max(1, n));

    public void MoveLeft(int n) => SetColumn(CursorX - Math.Max(1, n));

    public void Tab(int n = 1)
    {
        for (var i = 0; i < Math.Max(1, n); i++)
        {
            var x = CursorX + 1;
            while (x < Cols - 1 && !_tabs[x]) x++;
            CursorX = Math.Min(x, Cols - 1);
        }

        _wrapPending = false;
        Version++;
    }

    public void BackTab(int n = 1)
    {
        for (var i = 0; i < Math.Max(1, n); i++)
        {
            var x = CursorX - 1;
            while (x > 0 && !_tabs[x]) x--;
            CursorX = Math.Max(x, 0);
        }

        _wrapPending = false;
        Version++;
    }

    public void SetTabStop() => _tabs[CursorX] = true;

    public void ClearTabStop(bool all)
    {
        if (all) Array.Clear(_tabs);
        else _tabs[CursorX] = false;
    }

    private void ResetTabs()
    {
        _tabs = new bool[Cols];
        for (var x = 8; x < Cols; x += 8) _tabs[x] = true;
    }

    public void SaveCursor() => _saved = Capture();

    public void RestoreCursor() => Restore(_saved);

    private SavedCursor Capture() => new()
    {
        X = CursorX, Y = CursorY, Fg = PenFg, Bg = PenBg, Attr = PenAttr,
        WrapPending = _wrapPending, Origin = OriginMode, G0 = _charsets[0], G1 = _charsets[1], Gl = _glSet,
    };

    private void Restore(SavedCursor s)
    {
        if (s.G0 == 0) return;   // nikad nije sačuvano

        CursorX = Math.Clamp(s.X, 0, Cols - 1);
        CursorY = Math.Clamp(s.Y, 0, Rows - 1);
        PenFg = s.Fg;
        PenBg = s.Bg;
        PenAttr = s.Attr;
        _wrapPending = s.WrapPending;
        OriginMode = s.Origin;
        _charsets[0] = s.G0;
        _charsets[1] = s.G1;
        _glSet = s.Gl;
        Version++;
    }

    public void DesignateCharset(int slot, char set) => _charsets[slot] = set;

    public void ShiftCharset(int slot) => _glSet = slot;

    // ---------- brisanje ----------

    private Cell Blank() => Cell.Blank(PenBg);

    private void Fill(int y, int from, int to)
    {
        var row = _rows[y];
        for (var x = Math.Max(0, from); x < Math.Min(Cols, to); x++) row[x] = Blank();
    }

    public void EraseInLine(int mode)
    {
        switch (mode)
        {
            case 0: BreakWide(CursorX, CursorY); Fill(CursorY, CursorX, Cols); break;
            case 1: BreakWide(CursorX, CursorY); Fill(CursorY, 0, CursorX + 1); break;
            default: Fill(CursorY, 0, Cols); break;
        }

        _wrapPending = false;
        Version++;
    }

    public void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                Fill(CursorY, CursorX, Cols);
                for (var y = CursorY + 1; y < Rows; y++) Fill(y, 0, Cols);
                break;
            case 1:
                for (var y = 0; y < CursorY; y++) Fill(y, 0, Cols);
                Fill(CursorY, 0, CursorX + 1);
                break;
            case 3:
                _scrollback.Clear();
                break;
            default:
                for (var y = 0; y < Rows; y++) Fill(y, 0, Cols);
                break;
        }

        Version++;
    }

    public void EraseChars(int n)
    {
        Fill(CursorY, CursorX, CursorX + Math.Max(1, n));
        Version++;
    }

    public void InsertChars(int n)
    {
        n = Math.Clamp(n, 1, Cols - CursorX);
        var row = _rows[CursorY];
        Array.Copy(row, CursorX, row, CursorX + n, Cols - CursorX - n);
        Fill(CursorY, CursorX, CursorX + n);
        Version++;
    }

    public void DeleteChars(int n)
    {
        n = Math.Clamp(n, 1, Cols - CursorX);
        var row = _rows[CursorY];
        Array.Copy(row, CursorX + n, row, CursorX, Cols - CursorX - n);
        Fill(CursorY, Cols - n, Cols);
        Version++;
    }

    public void InsertLines(int n)
    {
        if (CursorY < _top || CursorY > _bottom) return;

        n = Math.Clamp(n, 1, _bottom - CursorY + 1);
        ShiftRowsDown(CursorY, _bottom, n);
        CursorX = 0;
        _wrapPending = false;
        Version++;
    }

    public void DeleteLines(int n)
    {
        if (CursorY < _top || CursorY > _bottom) return;

        n = Math.Clamp(n, 1, _bottom - CursorY + 1);
        ShiftRowsUp(CursorY, _bottom, n, toScrollback: false);
        CursorX = 0;
        _wrapPending = false;
        Version++;
    }

    // ---------- skrolovanje ----------

    public void SetScrollRegion(int top, int bottom)
    {
        // brojevi su od nule; nevažeći opseg poništava oblast (ceo ekran)
        if (top < 0 || bottom >= Rows || top >= bottom)
        {
            top = 0;
            bottom = Rows - 1;
        }

        _top = top;
        _bottom = bottom;
        MoveTo(0, 0);
    }

    public void ScrollUp(int n)
    {
        n = Math.Clamp(n, 1, _bottom - _top + 1);
        var keep = !_altActive && _top == 0 && _bottom == Rows - 1;
        ShiftRowsUp(_top, _bottom, n, keep);
        Version++;
    }

    public void ScrollDown(int n)
    {
        n = Math.Clamp(n, 1, _bottom - _top + 1);
        ShiftRowsDown(_top, _bottom, n);
        Version++;
    }

    // redovi gore u oblasti; izgurani idu u istoriju samo kad se skroluje ceo glavni ekran
    private void ShiftRowsUp(int top, int bottom, int n, bool toScrollback)
    {
        for (var i = 0; i < n; i++)
        {
            if (toScrollback) _scrollback.Add(_rows[top]);

            Array.Copy(_rows, top + 1, _rows, top, bottom - top);
            _rows[bottom] = NewRow(Cols, PenBg);
        }

        if (_scrollback.Count > MaxScrollback + 200) _scrollback.RemoveRange(0, _scrollback.Count - MaxScrollback);
    }

    private void ShiftRowsDown(int top, int bottom, int n)
    {
        for (var i = 0; i < n; i++)
        {
            Array.Copy(_rows, top, _rows, top + 1, bottom - top);
            _rows[top] = NewRow(Cols, PenBg);
        }
    }

    // ---------- alternativni ekran ----------

    /// <summary>Režimi 47, 1047 i 1049: ceo ekran programa kao što su vim, less i htop.</summary>
    public void SetAltScreen(bool on, bool saveCursor)
    {
        if (on == _altActive) return;

        if (on)
        {
            if (saveCursor) _savedMain = Capture();
            _altActive = true;
            _rows = _alt;
            for (var y = 0; y < Rows; y++) _rows[y] = NewRow(Cols, TermColor.Default);
        }
        else
        {
            _altActive = false;
            _rows = _main;
            if (saveCursor) Restore(_savedMain);
        }

        _top = 0;
        _bottom = Rows - 1;
        _wrapPending = false;
        Version++;
    }

    // ---------- veličina ----------

    public void Resize(int cols, int rows)
    {
        cols = Math.Max(1, cols);
        rows = Math.Max(1, rows);
        if (cols == Cols && rows == Rows) return;

        // glavni ekran: ako je kursor ispod nove donje ivice, gornji redovi odlaze u istoriju
        if (rows < Rows && !_altActive)
        {
            var overflow = Math.Min(Math.Max(0, CursorY - rows + 1), Rows - 1);
            for (var i = 0; i < overflow; i++)
            {
                _scrollback.Add(_main[0]);
                Array.Copy(_main, 1, _main, 0, Rows - 1);
                _main[Rows - 1] = NewRow(Cols, TermColor.Default);
                CursorY--;
            }
        }

        _main = Resized(_main, cols, rows);
        _alt = Resized(_alt, cols, rows);
        _rows = _altActive ? _alt : _main;

        Cols = cols;
        Rows = rows;
        _top = 0;
        _bottom = rows - 1;
        CursorX = Math.Clamp(CursorX, 0, cols - 1);
        CursorY = Math.Clamp(CursorY, 0, rows - 1);
        _wrapPending = false;
        ResetTabs();
        Version++;
    }

    private Cell[][] Resized(Cell[][] old, int cols, int rows)
    {
        var grid = new Cell[rows][];
        for (var y = 0; y < rows; y++)
        {
            grid[y] = NewRow(cols, TermColor.Default);
            if (y < old.Length) Array.Copy(old[y], grid[y], Math.Min(old[y].Length, cols));
        }

        return grid;
    }

    // ---------- reset ----------

    public void FullReset()
    {
        _altActive = false;
        _rows = _main;
        for (var y = 0; y < Rows; y++)
        {
            _main[y] = NewRow(Cols, TermColor.Default);
            _alt[y] = NewRow(Cols, TermColor.Default);
        }

        _scrollback.Clear();
        PenFg = PenBg = TermColor.Default;
        PenAttr = CellAttr.None;
        AutoWrap = true;
        OriginMode = false;
        InsertMode = false;
        CursorVisible = true;
        AppCursorKeys = false;
        BracketedPaste = false;
        CursorStyle = 0;
        _charsets[0] = _charsets[1] = 'B';
        _glSet = 0;
        _saved = default;
        _top = 0;
        _bottom = Rows - 1;
        CursorX = CursorY = 0;
        _wrapPending = false;
        ResetTabs();
        Version++;
    }

    // ---------- pomoćno ----------

    private static Cell[][] NewGrid(int cols, int rows)
    {
        var grid = new Cell[rows][];
        for (var y = 0; y < rows; y++) grid[y] = NewRow(cols, TermColor.Default);
        return grid;
    }

    private static Cell[] NewRow(int cols, int bg)
    {
        var row = new Cell[cols];
        Array.Fill(row, Cell.Blank(bg));
        return row;
    }
}
