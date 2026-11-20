using System.Text;

namespace CloudAndBear.Ui.Terminal;

/// <summary>
/// Mala izmišljena ljuska za pregled na PC-u (nema pseudo-terminala): prima tastere, izvršava nekoliko komandi
/// i piše u boji, da se izgled terminala dizajnira bez Debiana. Komande: help, ls, colors, kubectl get pods, unicode,
/// echo, date, whoami, hostname, clear, exit.
/// </summary>
public sealed class FakeTerminalService : ITerminalService
{
    public ITerminalSession Create(int cols, int rows) => new FakeTerminalSession();
}

internal sealed class FakeTerminalSession : ITerminalSession
{
    private const string Prompt = "\x1b[1;32mmarko@cloudandbear\x1b[0m:\x1b[1;34m~\x1b[0m$ ";

    private readonly StringBuilder _line = new();
    private int _escape;   // 0 nema, 1 posle ESC, 2 unutar CSI/SS3 niza (preskače se)
    private bool _exited;

    public event Action<byte[]>? Output;

    public event Action<int>? Exited;

    public void Start()
    {
        Emit("\x1b[1;36mCloudAndBear\x1b[0m  (izmišljeni terminal za pregled na PC-u)\r\n");
        Emit("Ukucaj \x1b[33mhelp\x1b[0m za spisak komandi.\r\n\r\n" + Prompt);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (_exited) return;
            Input(b);
        }
    }

    public void Resize(int cols, int rows)
    {
    }

    public void Dispose() => _exited = true;

    private void Input(byte b)
    {
        if (_escape == 1)
        {
            _escape = b is (byte)'[' or (byte)'O' ? 2 : 0;
            return;
        }

        if (_escape == 2)
        {
            if (b is >= 0x40 and <= 0x7E) _escape = 0;
            return;
        }

        switch (b)
        {
            case 0x1B: _escape = 1; break;
            case (byte)'\r':
                Emit("\r\n");
                Run(_line.ToString().Trim());
                _line.Clear();
                if (!_exited) Emit(Prompt);
                break;
            case 0x7F or 0x08:
                if (_line.Length > 0)
                {
                    _line.Length--;
                    Emit("\b \b");
                }

                break;
            case 0x03:
                _line.Clear();
                Emit("^C\r\n" + Prompt);
                break;
            case 0x0C:
                Emit("\x1b[H\x1b[2J" + Prompt + _line);
                break;
            case 0x04 when _line.Length == 0:
                Emit("logout\r\n");
                Exit();
                break;
            case >= 0x20:
                // znak (UTF-8 bajtovi stižu pojedinačno, pa se gomilaju u tekst)
                _utf8.Add(b);
                TryFlushText();
                break;
        }
    }

    private readonly List<byte> _utf8 = new();

    private void TryFlushText()
    {
        var text = Encoding.UTF8.GetString(_utf8.ToArray());
        if (text.Contains('\uFFFD') && _utf8.Count < 4) return;   // nepotpun znak, čekaj ostatak

        _utf8.Clear();
        _line.Append(text);
        Emit(text);
    }

    private void Exit()
    {
        _exited = true;
        Exited?.Invoke(0);
    }

    private void Run(string command)
    {
        if (command.Length == 0) return;

        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0])
        {
            case "help":
                Emit("Komande: \x1b[33mhelp ls colors unicode date whoami hostname clear exit\x1b[0m i \x1b[33mkubectl get pods\x1b[0m\r\n");
                break;
            case "ls":
                Emit("\x1b[1;34mDokumenta\x1b[0m  \x1b[1;34mSlike\x1b[0m  \x1b[1;34mkube\x1b[0m  \x1b[1;32mdeploy.sh\x1b[0m  \x1b[0mnotes.txt\x1b[0m  \x1b[36mlatest -> releases/2026-11-20\x1b[0m\r\n");
                break;
            case "colors":
                Colors();
                break;
            case "unicode":
                Emit("Čćšđž  ČĆŠĐŽ  ┌─┬─┐ │ │ │ └─┴─┘  █▓▒░  ✓ ✗ →  日本語\r\n");
                break;
            case "kubectl" when command == "kubectl get pods":
                Emit("\x1b[1mNAME                     READY   STATUS             RESTARTS   AGE\x1b[0m\r\n");
                Emit("api-7d9f6c5b8d-x2k4p     1/1     \x1b[32mRunning\x1b[0m            0          3d\r\n");
                Emit("web-5c7b9d6f4-lm9qz      1/1     \x1b[32mRunning\x1b[0m            0          3d\r\n");
                Emit("worker-6b5c8d7f9-qw2rt   0/1     \x1b[31mCrashLoopBackOff\x1b[0m   12         1h\r\n");
                Emit("cache-0                  1/1     \x1b[33mPending\x1b[0m            0          4m\r\n");
                break;
            case "date":
                Emit(DateTime.Now.ToString("ddd dd MMM yyyy HH:mm:ss") + "\r\n");
                break;
            case "whoami":
                Emit("marko\r\n");
                break;
            case "hostname":
                Emit("cloudandbear\r\n");
                break;
            case "echo":
                Emit((parts.Length > 1 ? parts[1] : "") + "\r\n");
                break;
            case "clear":
                Emit("\x1b[H\x1b[2J");
                break;
            case "exit":
                Emit("logout\r\n");
                Exit();
                break;
            default:
                Emit($"bash: {parts[0]}: komanda nije pronađena\r\n");
                break;
        }
    }

    private void Colors()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 16; i++)
            sb.Append($"\x1b[48;5;{i}m  \x1b[0m").Append(i == 7 ? "\r\n" : "");
        sb.Append("\r\n");
        for (var i = 16; i < 232; i += 6)
        {
            for (var j = 0; j < 6; j++) sb.Append($"\x1b[48;5;{i + j}m ");
            sb.Append("\x1b[0m");
        }

        sb.Append("\r\n");
        for (var i = 0; i < 64; i++) sb.Append($"\x1b[48;2;{i * 4};{255 - i * 4};{128}m \x1b[0m");
        sb.Append("\r\n\x1b[1mdebljina\x1b[0m \x1b[3mkurziv\x1b[0m \x1b[4mpodvučeno\x1b[0m \x1b[7mobrnuto\x1b[0m \x1b[9mprecrtano\x1b[0m \x1b[2mzatamnjeno\x1b[0m\r\n");
        Emit(sb.ToString());
    }

    private void Emit(string text) => Output?.Invoke(Encoding.UTF8.GetBytes(text));
}
