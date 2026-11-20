using Avalonia.Input;

namespace CloudAndBear.Ui.Terminal;

/// <summary>Pretvara tastere u bajtove koje terminal šalje programu (xterm oblik).</summary>
public static class KeyEncoder
{
    /// <summary>
    /// Bajtovi za taster, ili null kad taster nije poseban (slova i znakovi stižu kao tekst preko TextInput-a).
    /// </summary>
    public static byte[]? Encode(Key key, KeyModifiers modifiers, string? keySymbol, bool appCursorKeys)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);

        // xterm: 1 + Shift(1) + Alt(2) + Ctrl(4) + Meta(8)
        var m = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0) + (modifiers.HasFlag(KeyModifiers.Meta) ? 8 : 0);

        switch (key)
        {
            case Key.Enter: return alt ? "\x1b\r"u8.ToArray() : "\r"u8.ToArray();
            case Key.Tab: return shift ? "\x1b[Z"u8.ToArray() : "\t"u8.ToArray();
            case Key.Back: return ctrl ? [0x08] : alt ? [0x1B, 0x7F] : [0x7F];
            case Key.Escape: return [0x1B];

            case Key.Up: return Cursor('A', m, appCursorKeys);
            case Key.Down: return Cursor('B', m, appCursorKeys);
            case Key.Right: return Cursor('C', m, appCursorKeys);
            case Key.Left: return Cursor('D', m, appCursorKeys);
            case Key.Home: return Cursor('H', m, appCursorKeys);
            case Key.End: return Cursor('F', m, appCursorKeys);

            case Key.Insert: return Tilde(2, m);
            case Key.Delete: return Tilde(3, m);
            case Key.PageUp: return Tilde(5, m);
            case Key.PageDown: return Tilde(6, m);

            case Key.F1: return Function('P', m);
            case Key.F2: return Function('Q', m);
            case Key.F3: return Function('R', m);
            case Key.F4: return Function('S', m);
            case Key.F5: return Tilde(15, m);
            case Key.F6: return Tilde(17, m);
            case Key.F7: return Tilde(18, m);
            case Key.F8: return Tilde(19, m);
            case Key.F9: return Tilde(20, m);
            case Key.F10: return Tilde(21, m);
            case Key.F11: return Tilde(23, m);
            case Key.F12: return Tilde(24, m);
        }

        if (ctrl)
        {
            byte? control = key switch
            {
                >= Key.A and <= Key.Z => (byte)(key - Key.A + 1),
                Key.Space => 0,
                Key.OemOpenBrackets => 0x1B,
                Key.OemBackslash or Key.OemPipe => 0x1C,
                Key.OemCloseBrackets => 0x1D,
                _ => null,
            };

            if (control is { } c) return alt ? [0x1B, c] : [c];
        }

        // Alt + znak: ESC pa znak (readline: Alt+B, Alt+F, Alt+D ...)
        if (alt && !ctrl && !string.IsNullOrEmpty(keySymbol))
            return [0x1B, .. System.Text.Encoding.UTF8.GetBytes(keySymbol)];

        return null;
    }

    private static byte[] Cursor(char final, int m, bool app) =>
        m > 1
            ? Ascii($"\x1b[1;{m}{final}")
            : Ascii(app ? $"\x1bO{final}" : $"\x1b[{final}");

    private static byte[] Function(char final, int m) =>
        m > 1 ? Ascii($"\x1b[1;{m}{final}") : Ascii($"\x1bO{final}");

    private static byte[] Tilde(int code, int m) =>
        m > 1 ? Ascii($"\x1b[{code};{m}~") : Ascii($"\x1b[{code}~");

    private static byte[] Ascii(string s) => System.Text.Encoding.ASCII.GetBytes(s);
}
