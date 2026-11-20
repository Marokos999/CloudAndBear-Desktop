using Avalonia;
using Avalonia.Media;

namespace CloudAndBear.Ui.Views;

/// <summary>
/// Okviri (U+2500–U+257F) i blokovi (U+2580–U+259F) se crtaju linijama i pravougaonicima, a ne glifovima fonta:
/// tako se spajaju bez rupa između ćelija (tmux, mc, htop, vim).
/// </summary>
internal static class BoxDrawing
{
    // (levo, desno, gore, dole): 0 nema, 1 tanko, 2 debelo; dvostruke linije (U+2550..) crta font
    private static readonly Dictionary<int, (byte L, byte R, byte U, byte D)> Lines = new()
    {
        [0x2500] = (1, 1, 0, 0), [0x2501] = (2, 2, 0, 0), [0x2502] = (0, 0, 1, 1), [0x2503] = (0, 0, 2, 2),
        [0x250C] = (0, 1, 0, 1), [0x250F] = (0, 2, 0, 2), [0x2510] = (1, 0, 0, 1), [0x2513] = (2, 0, 0, 2),
        [0x2514] = (0, 1, 1, 0), [0x2517] = (0, 2, 2, 0), [0x2518] = (1, 0, 1, 0), [0x251B] = (2, 0, 2, 0),
        [0x251C] = (0, 1, 1, 1), [0x2523] = (0, 2, 2, 2), [0x2524] = (1, 0, 1, 1), [0x252B] = (2, 0, 2, 2),
        [0x252C] = (1, 1, 0, 1), [0x2533] = (2, 2, 0, 2), [0x2534] = (1, 1, 1, 0), [0x253B] = (2, 2, 2, 0),
        [0x253C] = (1, 1, 1, 1), [0x254B] = (2, 2, 2, 2),
        [0x256D] = (0, 1, 0, 1), [0x256E] = (1, 0, 0, 1), [0x256F] = (1, 0, 1, 0), [0x2570] = (0, 1, 1, 0),
    };

    // četvrtine bloka: bit 1 gore levo, 2 gore desno, 4 dole levo, 8 dole desno
    private static readonly Dictionary<int, int> Quadrants = new()
    {
        [0x2596] = 4, [0x2597] = 8, [0x2598] = 1, [0x2599] = 1 | 4 | 8, [0x259A] = 1 | 8,
        [0x259B] = 1 | 2 | 4, [0x259C] = 1 | 2 | 8, [0x259D] = 2, [0x259E] = 2 | 4, [0x259F] = 2 | 4 | 8,
    };

    public static bool Handles(int rune) =>
        rune is >= 0x2580 and <= 0x259F || Lines.ContainsKey(rune);

    /// <summary>Crta znak u ćeliju (x0,y0)-(x1,y1); vraća false ako ga ne zna (tada ga crta font).</summary>
    public static bool TryDraw(DrawingContext context, int rune, Rect cell, IBrush foreground, Color fg, Color bg)
    {
        // ivice na celim pikselima: susedne ćelije se spajaju bez šava
        double x0 = Math.Round(cell.X), x1 = Math.Round(cell.Right), y0 = Math.Round(cell.Y), y1 = Math.Round(cell.Bottom);
        double w = x1 - x0, h = y1 - y0;

        if (Lines.TryGetValue(rune, out var line))
        {
            var light = Math.Max(1, Math.Round(h / 16));
            var cx = Math.Round(x0 + w / 2);
            var cy = Math.Round(y0 + h / 2);

            void Seg(byte weight, double fromX, double fromY, double toX, double toY, bool horizontal, bool centerAtEnd)
            {
                if (weight != 0)
                    Stroke(context, foreground, fromX, fromY, toX, toY, horizontal, weight == 2 ? light * 2 : light, centerAtEnd);
            }

            // svaki krak ide od ivice ćelije do sredine i prelazi je za pola debljine, da uglovi budu puni
            Seg(line.L, x0, cy, cx, cy, true, true);
            Seg(line.R, cx, cy, x1, cy, true, false);
            Seg(line.U, cx, y0, cx, cy, false, true);
            Seg(line.D, cx, cy, cx, y1, false, false);
            return true;
        }

        if (Quadrants.TryGetValue(rune, out var mask))
        {
            var mx = Math.Round(x0 + w / 2);
            var my = Math.Round(y0 + h / 2);
            if ((mask & 1) != 0) context.DrawRectangle(foreground, null, new Rect(x0, y0, mx - x0, my - y0));
            if ((mask & 2) != 0) context.DrawRectangle(foreground, null, new Rect(mx, y0, x1 - mx, my - y0));
            if ((mask & 4) != 0) context.DrawRectangle(foreground, null, new Rect(x0, my, mx - x0, y1 - my));
            if ((mask & 8) != 0) context.DrawRectangle(foreground, null, new Rect(mx, my, x1 - mx, y1 - my));
            return true;
        }

        switch (rune)
        {
            case 0x2580: Fill(context, foreground, x0, y0, w, Math.Round(h / 2)); return true;
            case >= 0x2581 and <= 0x2587:
            {
                var part = Math.Round(h * (rune - 0x2580) / 8);
                Fill(context, foreground, x0, y1 - part, w, part);
                return true;
            }
            case 0x2588: Fill(context, foreground, x0, y0, w, h); return true;
            case >= 0x2589 and <= 0x258F:
            {
                var part = Math.Round(w * (0x2590 - rune) / 8);
                Fill(context, foreground, x0, y0, part, h);
                return true;
            }
            case 0x2590: Fill(context, foreground, x0 + Math.Round(w / 2), y0, w - Math.Round(w / 2), h); return true;
            case 0x2591 or 0x2592 or 0x2593:
            {
                var alpha = (rune - 0x2590) * 0.25;
                var mixed = Color.FromRgb((byte)(bg.R + (fg.R - bg.R) * alpha), (byte)(bg.G + (fg.G - bg.G) * alpha), (byte)(bg.B + (fg.B - bg.B) * alpha));
                Fill(context, new SolidColorBrush(mixed), x0, y0, w, h);
                return true;
            }
            case 0x2594: Fill(context, foreground, x0, y0, w, Math.Max(1, Math.Round(h / 8))); return true;
            case 0x2595: Fill(context, foreground, x1 - Math.Max(1, Math.Round(w / 8)), y0, Math.Max(1, Math.Round(w / 8)), h); return true;
        }

        return false;
    }

    private static void Fill(DrawingContext context, IBrush brush, double x, double y, double w, double h)
    {
        if (w > 0 && h > 0) context.DrawRectangle(brush, null, new Rect(x, y, w, h));
    }

    // linija debljine t od (fromX,fromY) do (toX,toY); kraj kod sredine se produžava za pola debljine da se spoji
    private static void Stroke(DrawingContext context, IBrush brush, double fromX, double fromY, double toX, double toY, bool horizontal, double t, bool centerAtEnd)
    {
        var half = Math.Floor(t / 2);
        if (horizontal)
        {
            double x = Math.Min(fromX, toX), w = Math.Abs(toX - fromX) + half;
            if (!centerAtEnd) x -= half;
            Fill(context, brush, x, fromY - half, w, t);
        }
        else
        {
            double y = Math.Min(fromY, toY), h = Math.Abs(toY - fromY) + half;
            if (!centerAtEnd) y -= half;
            Fill(context, brush, fromX - half, y, t, h);
        }
    }
}
