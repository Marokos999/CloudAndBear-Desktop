using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace CloudAndBear.Ui.Hosting;

/// <summary>Prozor za pregled na PC-u. F11 = ceo ekran. Veličina: --size=1920x1080.</summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow() : this(null)
    {
    }

    public PreviewWindow(PixelSize? size)
    {
        InitializeComponent();

        if (size is { } s)
        {
            Width = s.Width;
            Height = s.Height;
        }
    }

    public static PixelSize? ParseSize(string[]? args)
    {
        var arg = args?.FirstOrDefault(a => a.StartsWith("--size=", StringComparison.Ordinal));
        return arg is null ? null : ParseSize(arg[7..]);
    }

    public static PixelSize? ParseSize(string text)
    {
        var parts = text.Split('x');
        return parts.Length == 2
               && int.TryParse(parts[0], out var w)
               && int.TryParse(parts[1], out var h)
               && w > 0 && h > 0
            ? new PixelSize(w, h)
            : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
