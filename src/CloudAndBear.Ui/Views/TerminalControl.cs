using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CloudAndBear.Ui.Terminal;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

/// <summary>
/// Crta terminal: mreža ćelija iz <see cref="TerminalScreen"/>, kursor, izbor mišem, skrolovanje istorije.
/// Primarni font je neki monospace; na Debianu je to DejaVu Sans Mono (paket fonts-dejavu-core).
/// </summary>
public sealed class TerminalControl : Control, IWindowFocusTarget
{
    private const string FontNames = "Cascadia Mono, DejaVu Sans Mono, Consolas, Liberation Mono, monospace";
    private const double FontSize = 15;

    private static string _clipboard = "";   // kad sistem nema međuspremnik (uređaj bez Wayland-a)
    private static string _primary = "";     // poslednje izabrano: srednji klik lepi (kao na X11)

    private readonly Typeface[] _faces;
    private readonly Dictionary<int, IBrush> _brushes = new();
    private readonly Dictionary<(string Text, int Fg, int Face), FormattedText> _texts = new();
    private readonly DispatcherTimer _blink = new() { Interval = TimeSpan.FromMilliseconds(530) };
    private readonly StringBuilder _run = new();

    private TerminalViewModel? _vm;
    private double _cellW = 9;
    private double _cellH = 18;
    private Color[] _ansi = DefaultAnsi();
    private Color _background = Color.Parse("#0B1118");
    private Color _foreground = Color.Parse("#D7DEE6");
    private Color _cursor = Color.Parse("#9ECBFF");
    private Color _selection = Color.Parse("#559ECBFF");
    private int _scrollOffset;
    private int _lastScrollback;
    private bool _cursorOn = true;
    private bool _selecting;
    private (int Line, int Col)? _selStart;
    private (int Line, int Col)? _selEnd;
    private DateTime _altKeyHandledAt = DateTime.MinValue;
    private string? _altKeySymbol;

    static TerminalControl()
    {
        FocusableProperty.OverrideDefaultValue<TerminalControl>(true);
        AffectsRender<TerminalControl>(BoundsProperty);
    }

    public TerminalControl()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);

        _faces =
        [
            new Typeface(new FontFamily(FontNames), FontStyle.Normal, FontWeight.Normal),
            new Typeface(new FontFamily(FontNames), FontStyle.Normal, FontWeight.Bold),
            new Typeface(new FontFamily(FontNames), FontStyle.Italic, FontWeight.Normal),
            new Typeface(new FontFamily(FontNames), FontStyle.Italic, FontWeight.Bold),
        ];

        MeasureCell();
        _blink.Tick += (_, _) =>
        {
            _cursorOn = !_cursorOn;
            InvalidateVisual();
        };
    }

    /// <summary>Koliko redova istorije je iznad prikaza (0 = dno, prati novi ispis).</summary>
    public int ScrollOffset => _scrollOffset;

    public double CellWidth => _cellW;

    public double CellHeight => _cellH;

    // ---------- veza sa ViewModel-om ----------

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null) _vm.ScreenChanged -= OnScreenChanged;
        _vm = DataContext as TerminalViewModel;
        if (_vm is not null)
        {
            _vm.ScreenChanged += OnScreenChanged;
            _lastScrollback = _vm.Emulator.Screen.ScrollbackCount;
            ApplySize(Bounds.Size);
        }

        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadPalette();
    }

    private void OnScreenChanged()
    {
        var screen = _vm!.Emulator.Screen;

        // dok korisnik čita istoriju, novi redovi ne pomeraju prikaz
        var grown = screen.ScrollbackCount - _lastScrollback;
        if (_scrollOffset > 0 && grown > 0) _scrollOffset += grown;
        _lastScrollback = screen.ScrollbackCount;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, screen.ScrollbackCount);

        InvalidateVisual();
    }

    // ---------- veličina ----------

    private void MeasureCell()
    {
        var sample = new FormattedText("MMMMMMMMMM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _faces[0], FontSize, Brushes.White);
        _cellW = sample.Width / 10.0;
        _cellH = Math.Ceiling(sample.Height);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 80 * _cellW : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 24 * _cellH : availableSize.Height);

    protected override Size ArrangeOverride(Size finalSize)
    {
        ApplySize(finalSize);
        return finalSize;
    }

    private void ApplySize(Size size)
    {
        if (_vm is null || size.Width <= 0 || size.Height <= 0) return;

        _vm.Resize(Math.Max(1, (int)(size.Width / _cellW)), Math.Max(1, (int)(size.Height / _cellH)));
    }

    // ---------- boje ----------

    private void LoadPalette()
    {
        Color Find(string key, Color fallback) => this.TryFindResource(key, out var value) && value is Color c ? c : fallback;

        _background = Find("TerminalBackgroundColor", _background);
        _foreground = Find("TerminalForegroundColor", _foreground);
        _cursor = Find("TerminalCursorColor", _cursor);
        _selection = Find("TerminalSelectionColor", _selection);

        var fallback = DefaultAnsi();
        _ansi = Enumerable.Range(0, 16).Select(i => Find($"TerminalAnsi{i}Color", fallback[i])).ToArray();

        _brushes.Clear();
        _texts.Clear();
        InvalidateVisual();
    }

    private static Color[] DefaultAnsi() =>
        new[]
        {
            "#1B2128", "#E06C75", "#98C379", "#E5C07B", "#61AFEF", "#C678DD", "#56B6C2", "#ABB2BF",
            "#5C6370", "#FF7B86", "#B5E890", "#F0D08A", "#7DC1FF", "#D895EE", "#6FD0DC", "#E6EAEE",
        }.Select(Color.Parse).ToArray();

    private IBrush BrushOf(Color color)
    {
        var key = (int)color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush)) _brushes[key] = brush = new SolidColorBrush(color).ToImmutable();
        return brush;
    }

    private Color Resolve(int color, bool isForeground, CellAttr attr)
    {
        if (color == TermColor.Default) return isForeground ? _foreground : _background;

        if (TermColor.IsRgb(color)) return Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color);

        var index = color;
        if (isForeground && (attr & CellAttr.Bold) != 0 && index < 8) index += 8;   // podebljano = svetlija boja
        if (index < 16) return _ansi[index];

        var rgb = TermColor.CubeOrGray(index);
        return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    // boje ćelije posle obrnutog prikaza i zatamnjenja
    private (Color Fg, Color Bg) Colors(in Cell cell)
    {
        var fg = Resolve(cell.Fg, true, cell.Attr);
        var bg = Resolve(cell.Bg, false, cell.Attr);
        if ((cell.Attr & CellAttr.Inverse) != 0) (fg, bg) = (bg, fg);
        if ((cell.Attr & CellAttr.Dim) != 0) fg = Blend(fg, bg, 0.45);
        return (fg, bg);
    }

    private static Color Blend(Color a, Color b, double toB) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * toB), (byte)(a.G + (b.G - a.G) * toB), (byte)(a.B + (b.B - a.B) * toB));

    // ---------- crtanje ----------

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(BrushOf(_background), null, new Rect(Bounds.Size));
        if (_vm is null) return;

        var screen = _vm.Emulator.Screen;
        var first = screen.ScrollbackCount - _scrollOffset;
        var cols = screen.Cols;

        if (_texts.Count > 6000) _texts.Clear();

        for (var vy = 0; vy < screen.Rows; vy++)
        {
            var line = screen.GetLine(first + vy);
            var count = Math.Min(line.Length, cols);
            var y = vy * _cellH;

            DrawBackgrounds(context, line, count, y);
            DrawText(context, line, count, y);
            DrawSelection(context, first + vy, count, y);
        }

        if (_scrollOffset == 0 && screen.CursorVisible) DrawCursor(context, screen);
    }

    private void DrawBackgrounds(DrawingContext context, Cell[] line, int count, double y)
    {
        var x = 0;
        while (x < count)
        {
            var bg = Colors(line[x]).Bg;
            var end = x + 1;
            while (end < count && Colors(line[end]).Bg == bg) end++;

            if (bg != _background)
                context.DrawRectangle(BrushOf(bg), null, new Rect(x * _cellW, y, (end - x) * _cellW + 0.5, _cellH));

            x = end;
        }
    }

    private void DrawText(DrawingContext context, Cell[] line, int count, double y)
    {
        var x = 0;
        while (x < count)
        {
            var cell = line[x];
            if ((cell.Attr & CellAttr.Continuation) != 0 || (cell.Attr & CellAttr.Hidden) != 0 || cell.Rune is 0 or ' ' && !HasLines(cell))
            {
                x++;
                continue;
            }

            var (fg, cellBg) = Colors(cell);
            var face = FaceOf(cell.Attr);

            // široki i neobični znakovi se crtaju pojedinačno na tačnoj ćeliji (font ih može imati drugačije širine)
            if (cell.Rune >= 0x2000 || (cell.Attr & CellAttr.Wide) != 0)
            {
                // okviri i blokovi bez fonta: linije se spajaju bez rupa
                if (BoxDrawing.Handles(cell.Rune) && BoxDrawing.TryDraw(context, cell.Rune, new Rect(x * _cellW, y, _cellW, _cellH), BrushOf(fg), fg, cellBg))
                {
                    x++;
                    continue;
                }

                Draw(context, char.ConvertFromUtf32(cell.Rune), fg, face, x, y);
                x++;
                continue;
            }

            _run.Clear();
            var start = x;
            while (x < count)
            {
                var next = line[x];
                if (x > start && (next.Rune >= 0x2000 || FaceOf(next.Attr) != face || Colors(next).Fg != fg || (next.Attr & (CellAttr.Continuation | CellAttr.Wide | CellAttr.Hidden)) != 0))
                    break;

                _run.Append(next.Rune == 0 ? ' ' : (char)next.Rune);
                x++;
            }

            var text = _run.ToString();
            if (text.All(c => c == ' ') && !HasLines(cell)) continue;
            Draw(context, text.TrimEnd(), fg, face, start, y, HasLines(cell) ? text : null);
        }
    }

    private static bool HasLines(Cell cell) => (cell.Attr & (CellAttr.Underline | CellAttr.Strike)) != 0;

    // 0 obično, 1 podebljano, 2 kurziv, 3 oba; bitovi 4 i 8 su podvlačenje i precrtavanje (za ključ keša)
    private static int FaceOf(CellAttr attr) =>
        ((attr & CellAttr.Bold) != 0 ? 1 : 0) | ((attr & CellAttr.Italic) != 0 ? 2 : 0)
        | ((attr & CellAttr.Underline) != 0 ? 4 : 0) | ((attr & CellAttr.Strike) != 0 ? 8 : 0);

    private void Draw(DrawingContext context, string text, Color fg, int face, int col, double y, string? full = null)
    {
        text = full ?? text;
        if (text.Length == 0) return;

        var key = (text, (int)fg.ToUInt32(), face);
        if (!_texts.TryGetValue(key, out var formatted))
        {
            formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _faces[face & 3], FontSize, BrushOf(fg));
            var decorations = new TextDecorationCollection();
            if ((face & 4) != 0) decorations.AddRange(TextDecorations.Underline);
            if ((face & 8) != 0) decorations.AddRange(TextDecorations.Strikethrough);
            if (decorations.Count > 0) formatted.SetTextDecorations(decorations);
            _texts[key] = formatted;
        }

        context.DrawText(formatted, new Point(col * _cellW, y));
    }

    private void DrawSelection(DrawingContext context, int absoluteLine, int count, double y)
    {
        if (_selStart is not { } a || _selEnd is not { } b) return;

        var (from, to) = (a.Line, a.Col).CompareTo((b.Line, b.Col)) <= 0 ? (a, b) : (b, a);
        if (absoluteLine < from.Line || absoluteLine > to.Line) return;

        var c1 = absoluteLine == from.Line ? from.Col : 0;
        var c2 = absoluteLine == to.Line ? to.Col : count - 1;
        if (c2 < c1) return;

        context.DrawRectangle(BrushOf(_selection), null, new Rect(c1 * _cellW, y, (c2 - c1 + 1) * _cellW, _cellH));
    }

    private void DrawCursor(DrawingContext context, TerminalScreen screen)
    {
        var rect = new Rect(screen.CursorX * _cellW, screen.CursorY * _cellH, _cellW, _cellH);
        var blinking = screen.CursorStyle is 1 or 3 or 5;

        if (!IsFocused)
        {
            context.DrawRectangle(null, new Pen(BrushOf(_cursor), 1), rect.Deflate(0.5));
            return;
        }

        if (blinking && !_cursorOn) return;

        switch (screen.CursorStyle)
        {
            case 3 or 4:
                context.DrawRectangle(BrushOf(_cursor), null, new Rect(rect.X, rect.Bottom - 2, rect.Width, 2));
                break;
            case 5 or 6:
                context.DrawRectangle(BrushOf(_cursor), null, new Rect(rect.X, rect.Y, 2, rect.Height));
                break;
            default:
                context.DrawRectangle(BrushOf(_cursor), null, rect);
                ref var cell = ref screen.At(Math.Min(screen.CursorX, screen.Cols - 1), Math.Min(screen.CursorY, screen.Rows - 1));
                if (cell.Rune > ' ' && (cell.Attr & CellAttr.Continuation) == 0)
                    Draw(context, char.ConvertFromUtf32(cell.Rune), _background, FaceOf(cell.Attr), screen.CursorX, rect.Y);
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _cursorOn = true;
        _blink.Start();
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _blink.Stop();
        InvalidateVisual();
    }

    // ---------- tastatura ----------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is null || e.Handled) return;

        var mods = e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta);

        // Alt+Tab, Alt+F1... rešava desktop
        if (DesktopShortcuts.IsReserved(e.Key, mods)) return;

        if (mods == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.C)
        {
            Copy();
            e.Handled = true;
            return;
        }

        if ((mods == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.V) || (mods == KeyModifiers.Shift && e.Key == Key.Insert))
        {
            _ = PasteAsync();
            e.Handled = true;
            return;
        }

        if (mods == KeyModifiers.Shift && e.Key is Key.PageUp or Key.PageDown)
        {
            Scroll(e.Key == Key.PageUp ? _vm.Emulator.Screen.Rows - 1 : -(_vm.Emulator.Screen.Rows - 1));
            e.Handled = true;
            return;
        }

        if (_vm.IsExited)
        {
            if (e.Key == Key.Enter) _vm.RequestClose();
            e.Handled = true;
            return;
        }

        var bytes = KeyEncoder.Encode(e.Key, mods, e.KeySymbol, _vm.Emulator.Screen.AppCursorKeys);
        if (bytes is null) return;

        // Alt+slovo se šalje ovde; isti znak može stići i kao tekst, pa se taj preskače
        if ((mods & KeyModifiers.Alt) != 0 && !string.IsNullOrEmpty(e.KeySymbol))
        {
            _altKeyHandledAt = DateTime.UtcNow;
            _altKeySymbol = e.KeySymbol;
        }

        ToBottom();
        _vm.SendBytes(bytes);
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_vm is null || _vm.IsExited || string.IsNullOrEmpty(e.Text)) return;
        if (_altKeySymbol is not null)
        {
            var swallow = e.Text == _altKeySymbol && DateTime.UtcNow - _altKeyHandledAt < TimeSpan.FromMilliseconds(100);
            _altKeySymbol = null;
            if (swallow) return;
        }

        // kontrolni znakovi se šalju kao tasteri (Ctrl+slovo, Enter...), ne kao tekst
        var text = new string(e.Text.Where(c => c >= ' ' && c != '\x7f').ToArray());
        if (text.Length == 0) return;

        ToBottom();
        _vm.SendText(text);
        e.Handled = true;
    }

    // ---------- miš ----------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_vm is null) return;

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsMiddleButtonPressed)
        {
            if (_primary.Length > 0) _vm.Paste(_primary);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        var cell = CellAt(point.Position);
        if (e.ClickCount == 2) SelectWord(cell);
        else if (e.ClickCount >= 3) SelectLine(cell.Line);
        else
        {
            _selStart = _selEnd = cell;
            _selecting = true;
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_selecting || _vm is null) return;

        _selEnd = CellAt(e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_selecting) return;

        _selecting = false;
        e.Pointer.Capture(null);

        // klik bez prevlačenja ne bira ništa
        if (_selStart == _selEnd) _selStart = _selEnd = null;
        else _primary = SelectedText();
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_vm is null) return;

        Scroll((int)Math.Round(e.Delta.Y * 3));
        e.Handled = true;
    }

    private (int Line, int Col) CellAt(Point p)
    {
        var screen = _vm!.Emulator.Screen;
        var row = Math.Clamp((int)(p.Y / _cellH), 0, screen.Rows - 1);
        var col = Math.Clamp((int)(p.X / _cellW), 0, screen.Cols - 1);
        return (screen.ScrollbackCount - _scrollOffset + row, col);
    }

    private void Scroll(int lines)
    {
        var screen = _vm!.Emulator.Screen;
        _scrollOffset = Math.Clamp(_scrollOffset + lines, 0, screen.ScrollbackCount);
        InvalidateVisual();
    }

    private void ToBottom()
    {
        if (_scrollOffset == 0) return;

        _scrollOffset = 0;
        InvalidateVisual();
    }

    // ---------- izbor i međuspremnik ----------

    private void SelectLine(int line)
    {
        var screen = _vm!.Emulator.Screen;
        _selStart = (line, 0);
        _selEnd = (line, screen.Cols - 1);
        _primary = SelectedText();
    }

    private void SelectWord((int Line, int Col) cell)
    {
        var line = _vm!.Emulator.Screen.GetLine(cell.Line);
        static bool IsWord(Cell c) => c.Rune > ' ' && !char.IsWhiteSpace((char)Math.Min(c.Rune, 0xFFFF)) && c.Rune is not ('"' or '\'' or '`' or '(' or ')' or '[' or ']' or '{' or '}' or '<' or '>' or ',' or ';' or '|');

        var col = Math.Min(cell.Col, line.Length - 1);
        if (!IsWord(line[col]))
        {
            _selStart = _selEnd = (cell.Line, col);
            return;
        }

        var a = col;
        var b = col;
        while (a > 0 && IsWord(line[a - 1])) a--;
        while (b < line.Length - 1 && IsWord(line[b + 1])) b++;
        _selStart = (cell.Line, a);
        _selEnd = (cell.Line, b);
        _primary = SelectedText();
    }

    /// <summary>Izabrani tekst: linije bez završnih razmaka, spojene novim redom.</summary>
    public string SelectedText()
    {
        if (_vm is null || _selStart is not { } a || _selEnd is not { } b) return "";

        var (from, to) = (a.Line, a.Col).CompareTo((b.Line, b.Col)) <= 0 ? (a, b) : (b, a);
        var screen = _vm.Emulator.Screen;
        var sb = new StringBuilder();

        for (var l = from.Line; l <= to.Line; l++)
        {
            var line = screen.GetLine(l);
            var c1 = l == from.Line ? from.Col : 0;
            var c2 = l == to.Line ? Math.Min(to.Col, line.Length - 1) : line.Length - 1;
            var text = new StringBuilder();

            for (var x = c1; x <= c2 && x < line.Length; x++)
            {
                if ((line[x].Attr & CellAttr.Continuation) != 0) continue;
                text.Append(line[x].Rune == 0 ? " " : char.ConvertFromUtf32(line[x].Rune));
            }

            sb.Append(l == to.Line ? text.ToString().TrimEnd() : text.ToString().TrimEnd()).Append(l == to.Line ? "" : "\n");
        }

        return sb.ToString();
    }

    public void Copy()
    {
        var text = SelectedText();
        if (text.Length == 0) return;

        _clipboard = text;
        _primary = text;
        _ = SetSystemClipboardAsync(text);
    }

    public async Task PasteAsync()
    {
        if (_vm is null) return;

        string? text = null;
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) text = await clipboard.TryGetTextAsync();
        }
        catch
        {
            // međuspremnik nedostupan: ide interni
        }

        text ??= _clipboard;
        if (!string.IsNullOrEmpty(text)) _vm.Paste(text);
    }

    private async Task SetSystemClipboardAsync(string text)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
        }
        catch
        {
            // ostaje interni međuspremnik
        }
    }
}
