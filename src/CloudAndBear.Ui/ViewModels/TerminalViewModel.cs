using System.Collections.Concurrent;
using System.Text;
using Avalonia.Threading;
using CloudAndBear.Ui.Terminal;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Terminal: veza između sesije (ljuska), emulatora (ekran) i kontrole koja crta.</summary>
public sealed class TerminalViewModel : ViewModelBase, IDisposable, IClosableContent
{
    private const int FlushBudget = 256 * 1024;   // koliko bajtova emulator obradi odjednom, pa se vraća UI-ju

    private readonly ITerminalSession _session;
    private readonly ConcurrentQueue<byte[]> _pending = new();
    private int _flushScheduled;
    private bool _disposed;
    private bool _isExited;
    private string _title = "";

    public TerminalViewModel(ITerminalService service, int cols = 80, int rows = 24)
    {
        Emulator = new TerminalEmulator(cols, rows);
        Emulator.Response += bytes => _session?.Write(bytes);
        Emulator.TitleChanged += title => Title = title;

        _session = service.Create(cols, rows);
        _session.Output += OnOutput;
        _session.Exited += OnExited;
        _session.Start();
    }

    public TerminalEmulator Emulator { get; }

    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    /// <summary>Ljuska je završila; Enter zatvara prozor.</summary>
    public bool IsExited
    {
        get => _isExited;
        private set => Set(ref _isExited, value);
    }

    /// <summary>Ekran se promenio: kontrola se ponovo crta.</summary>
    public event Action? ScreenChanged;

    public event Action? CloseRequested;

    public void RequestClose() => CloseRequested?.Invoke();

    public void Resize(int cols, int rows)
    {
        if (cols < 1 || rows < 1) return;
        if (cols == Emulator.Screen.Cols && rows == Emulator.Screen.Rows) return;

        Emulator.Resize(cols, rows);
        _session.Resize(cols, rows);
        ScreenChanged?.Invoke();
    }

    public void SendText(string text) => SendBytes(Encoding.UTF8.GetBytes(text));

    public void SendBytes(byte[] data)
    {
        if (!_isExited) _session.Write(data);
    }

    /// <summary>Lepljenje: nove linije postaju Enter, a ako program traži (bracketed paste) tekst se uokviruje.</summary>
    public void Paste(string text)
    {
        if (text.Length == 0) return;

        text = text.Replace("\r\n", "\r").Replace('\n', '\r');
        if (Emulator.Screen.BracketedPaste) text = "\x1b[200~" + text.Replace("\x1b", "") + "\x1b[201~";
        SendText(text);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }

    // sesija piše sa svoje niti: bajtovi se redaju, a emulator ih obrađuje na UI niti
    private void OnOutput(byte[] data)
    {
        if (_disposed) return;

        _pending.Enqueue(data);
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0) Dispatcher.UIThread.Post(Flush);
    }

    private void OnExited(int code) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;

        Flush();
        Emulator.Feed($"\r\n\x1b[2m[proces je završen, kod {code}; Enter zatvara prozor]\x1b[0m\r\n");
        IsExited = true;
        ScreenChanged?.Invoke();
    });

    /// <summary>Obrađuje sve što je stiglo (ili prvih 256 KB, pa se vraća na red). Javna zbog testova.</summary>
    public void Flush()
    {
        var processed = 0;
        while (processed < FlushBudget && _pending.TryDequeue(out var chunk))
        {
            Emulator.Feed(chunk);
            processed += chunk.Length;
        }

        Interlocked.Exchange(ref _flushScheduled, 0);
        if (!_pending.IsEmpty && Interlocked.Exchange(ref _flushScheduled, 1) == 0)
            Dispatcher.UIThread.Post(Flush);

        ScreenChanged?.Invoke();
    }
}
