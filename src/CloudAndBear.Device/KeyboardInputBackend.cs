using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LinuxFramebuffer.Input;
using Avalonia.Threading;
using CloudAndBear.Ui.Input;
using Microsoft.Win32.SafeHandles;

namespace CloudAndBear.Device;

/// <summary>
/// Unos na uređaju: miš i touchpad radi <see cref="PointerInput"/> (libinput), a tastaturu čitamo sami iz /dev/input
/// (LibInputBackend tastaturu ne obrađuje). Tastature se traže na svake sekunde, pa one priključene posle pokretanja
/// (USB, Bluetooth) rade čim se pojave, a nestale se zaboravljaju.
/// </summary>
public sealed class KeyboardInputBackend : IInputBackend
{
    private readonly PointerInput _pointer = new();
    private readonly HashSet<string> _readers = [];   // putanje tastatura koje se trenutno čitaju (pristup samo pod lock-om)
    private readonly KeyboardDevice _ownKeyboard = new();
    private readonly KeyTranslator _translator;
    private Action<RawInputEventArgs>? _onInput;
    private IInputRoot? _root;

    public KeyboardInputBackend(IKeyboardLayoutService layouts)
    {
        // promena rasporeda ide na UI nit (menja traku)
        _translator = new KeyTranslator(layouts, () => Dispatcher.UIThread.Post(layouts.Next));
    }

    // fokus (polje za unos) drži Avalonijin zajednički uređaj tastature: samo preko njega slova stižu u TextBox;
    // sopstveni uređaj služi kao rezerva dok zajednički ne postoji
    // (Instance nije javno dostupan, pa se čita refleksijom)
    private static readonly System.Reflection.PropertyInfo? SharedKeyboard =
        typeof(KeyboardDevice).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

    private KeyboardDevice Keyboard => SharedKeyboard?.GetValue(null) as KeyboardDevice ?? _ownKeyboard;

    public void Initialize(IScreenInfoProvider info, Action<RawInputEventArgs> onInput)
    {
        _onInput = onInput;
        _pointer.Initialize(info, onInput);
        SilenceTerminal();

        new Thread(WatchKeyboards) { IsBackground = true, Name = "keyboards" }.Start();
    }

    public void SetInputRoot(IInputRoot root)
    {
        _root = root;
        _pointer.SetInputRoot(root);
    }

    // ---- konzola iza desktopa ne sme da dobija tastere ----

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(IntPtr fd, ulong request, ref int arg);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(IntPtr fd, ulong request, IntPtr arg);

    private const ulong KDGKBMODE = 0x4B44;   // pročitaj režim tastature terminala
    private const ulong KDSKBMODE = 0x4B45;   // postavi režim tastature terminala
    private const int K_OFF = 4;              // terminal ne obrađuje tastere

    private static SafeFileHandle? _tty;
    private static int _oldMode;

    // virtuelni terminal prestaje da obrađuje tastere (inače bi isli i u shell iza desktopa); pri izlasku se vraća.
    // Običan nalog sme to samo na svom (kontrolnom) terminalu, pa se prvo proba /dev/tty (servis ga dobija na tty1),
    // a tek onda /dev/tty0, koji može samo root.
    private static void SilenceTerminal()
    {
        foreach (var path in new[] { "/dev/tty", "/dev/tty0" })
        {
            try
            {
                var tty = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite);
                var h = tty.DangerousGetHandle();
                if (ioctl(h, KDGKBMODE, ref _oldMode) == 0 && ioctl(h, KDSKBMODE, (IntPtr)K_OFF) == 0)
                {
                    _tty = tty;
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreTerminal();
                    AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreTerminal();
                    return;
                }

                tty.Dispose();   // nije virtuelni terminal (npr. ssh -t) ili nema prava: sledeći pokušaj
            }
            catch
            {
                // nema tog uređaja ili nema prava: sledeći pokušaj
            }
        }

        // nijedan nije uspeo: tasteri će i dalje ići u konzolu
    }

    private static void RestoreTerminal()
    {
        if (_tty is null || _tty.IsClosed) return;

        ioctl(_tty.DangerousGetHandle(), KDSKBMODE, (IntPtr)_oldMode);
        _tty.Dispose();
    }

    // ---- čitanje tastature ----

    // tastatura ima taster A (KEY_A = 30); čita se iz sysfs-a, bez ioctl poziva
    private static bool IsKeyboard(string path)
    {
        try
        {
            var words = File.ReadAllText($"/sys/class/input/{Path.GetFileName(path)}/device/capabilities/key")
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (Convert.ToUInt64(words[^1], 16) & (1UL << 30)) != 0;
        }
        catch
        {
            return false;
        }
    }

    // Na svake sekunde traži nove tastature i za svaku pokreće čitanje. Čitanje se završi kad uređaj nestane
    // (isključen) ili kad ga nije moguće otvoriti (udev još nije dao prava); u oba slučaja putanja se zaboravlja,
    // pa se uređaj koji se vrati, ili dobije prava, uzima ponovo.
    private void WatchKeyboards()
    {
        while (true)
        {
            try
            {
                foreach (var path in Directory.GetFiles("/dev/input", "event*"))
                {
                    lock (_readers)
                    {
                        if (_readers.Contains(path)) continue;
                    }

                    if (!IsKeyboard(path)) continue;

                    lock (_readers) _readers.Add(path);
                    new Thread(() =>
                    {
                        Read(path);
                        lock (_readers) _readers.Remove(path);
                        lock (_translator) _translator.ReleaseAll();
                    }) { IsBackground = true, Name = "kbd-" + Path.GetFileName(path) }.Start();
                }
            }
            catch
            {
                // /dev/input se ne čita: pokušaj ponovo
            }

            Thread.Sleep(1000);
        }
    }

    private void Read(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0);
            var ev = new byte[24];   // struct input_event: 16 bajtova vreme, 2 tip, 2 kod, 4 vrednost
            while (file.Read(ev, 0, ev.Length) == ev.Length)
            {
                if (BitConverter.ToUInt16(ev, 16) == 1)   // EV_KEY
                    OnKey(BitConverter.ToUInt16(ev, 18), BitConverter.ToInt32(ev, 20));
            }
        }
        catch
        {
            // uređaj nestao ili nema pravo čitanja: čitanje se završava
        }
    }

    private void OnKey(int code, int value)
    {
        var root = _root;
        if (root is null) return;

        KeyResult? result;
        lock (_translator) result = _translator.OnKey(code, value);
        if (result is not { } r) return;

        var time = (ulong)Environment.TickCount64;
        var type = r.Down ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp;

        Dispatcher.UIThread.Post(() =>
        {
            var keyboard = Keyboard;
            _onInput?.Invoke(new RawKeyEventArgs(keyboard, time, root, type, r.Key, r.Modifiers, r.Physical, r.KeySymbol, KeyDeviceType.Keyboard));
            if (r.Text is not null)
                _onInput?.Invoke(new RawTextInputEventArgs(keyboard, time, root, r.Text));
        });
    }
}
