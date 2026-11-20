using Avalonia;
using Avalonia.LinuxFramebuffer;
using CloudAndBear.Ui;
using CloudAndBear.Ui.Input;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Device;

/// <summary>
/// Desktop na uređaju (Debian): crta preko DRM/KMS direktno na ekran, bez X11 i Waylanda. Isti pogledi kao u
/// pregledu na PC-u (<c>CloudAndBear.Ui</c>), ovde samo ulaz (tastatura, miš), izbor grafičke kartice i servisi koji
/// čitaju pravi sistem.
/// <para>Opcije: <c>--card=/dev/dri/card1</c> (inače se bira ona na kojoj je ekran), <c>--scale=1.5</c>.</para>
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("CloudAndBear.Device radi samo na Linuxu (DRM). Na PC-u koristi CloudAndBear.Preview.");
            return 1;
        }

        SilenceConsole();

        // raspored tastature je zajednički: ulaz ga menja (Alt+Shift), traka ga prikazuje
        var layouts = new KeyboardLayoutService(KeyboardLayoutService.DefaultStateFile());
        App.Services = DesktopServices.Create(layouts);

        var card = Option(args, "--card") ?? FindCard();
        var scale = double.TryParse(Option(args, "--scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0 ? s : 1.0;

        return BuildAvaloniaApp().StartLinuxDrm(args, card: card, options: new DrmOutputOptions { Scaling = scale },
            inputBackend: new KeyboardInputBackend(layouts));
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHarfBuzz()
            .WithInterFont()
            .LogToTrace();

    private static string? Option(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];

    // Kartica na kojoj je priključen ekran: laptop ima dve grafike (npr. card0 bez ekrana i card1 sa eDP-1), a Avalonia
    // bi uzela prvu. Unutrašnji ekran (eDP, LVDS) ima prednost nad spoljnim.
    private static string? FindCard()
    {
        try
        {
            var connected = Directory.GetDirectories("/sys/class/drm", "card*-*")
                .Where(IsConnected)
                .OrderBy(d => d.Contains("eDP") || d.Contains("LVDS") ? 0 : 1)
                .ThenBy(d => d, StringComparer.Ordinal)
                .FirstOrDefault();

            if (connected is null) return null;

            var name = Path.GetFileName(connected);   // npr. card1-eDP-1
            return "/dev/dri/" + name[..name.IndexOf('-')];
        }
        catch
        {
            return null;
        }
    }

    private static bool IsConnected(string dir)
    {
        try
        {
            return File.ReadAllText(Path.Combine(dir, "status")).Trim() == "connected";
        }
        catch
        {
            return false;
        }
    }

    // tastatura ne sme da odjekuje u konzolu iza ekrana: poseban zadatak čita i odbacuje sve što stigne
    private static void SilenceConsole()
    {
        new Thread(() =>
        {
            try
            {
                Console.CursorVisible = false;
                while (true) Console.ReadKey(true);
            }
            catch
            {
                // nema konzole (npr. servis bez terminala)
            }
        })
        {
            IsBackground = true,
            Name = "console",
        }.Start();
    }
}
