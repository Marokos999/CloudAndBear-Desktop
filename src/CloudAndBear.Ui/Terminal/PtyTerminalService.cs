using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace CloudAndBear.Ui.Terminal;

/// <summary>
/// Prava ljuska na pseudo-terminalu (Linux). .NET ne može da napravi proces sa kontrolnim terminalom, pa se
/// pseudo-terminal otvara ovde (posix_openpt), a ljuska se pokreće preko <c>sh -c 'exec setsid -c ...'</c> sa
/// slave uređajem kao stdin/stdout/stderr: <c>setsid -c</c> ga zatim čini kontrolnim terminalom nove sesije.
/// </summary>
public sealed class PtyTerminalService : ITerminalService
{
    public ITerminalSession Create(int cols, int rows) => new PtySession(cols, rows);
}

internal sealed class PtySession : ITerminalSession
{
    private const int ORdwr = 2;
    private const int ONoctty = 0x100;
    private const int FSetFd = 2;
    private const int FdCloexec = 1;
    private const ulong TiocSwinsz = 0x5414;
    private const short PollIn = 0x1;
    private const short PollErr = 0x8;
    private const short PollHup = 0x10;
    private const int Eintr = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort Rows, Cols, XPixel, YPixel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events, Revents;
    }

    [DllImport("libc", SetLastError = true)] private static extern int posix_openpt(int flags);
    [DllImport("libc", SetLastError = true)] private static extern int grantpt(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int unlockpt(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int ptsname_r(int fd, byte[] buf, nint size);
    [DllImport("libc", SetLastError = true)] private static extern int fcntl(int fd, int cmd, int arg);
    [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, ulong request, ref WinSize ws);
    [DllImport("libc", SetLastError = true)] private static extern int poll([In, Out] PollFd[] fds, ulong n, int timeout);
    [DllImport("libc", SetLastError = true)] private static extern nint read(int fd, byte[] buf, nint count);
    [DllImport("libc", SetLastError = true)] private static extern nint write(int fd, byte[] buf, nint count);
    [DllImport("libc", SetLastError = true)] private static extern int close(int fd);

    private readonly int _master;
    private readonly Process _process;
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Thread _reader;
    private readonly Thread _writer;
    private volatile bool _disposed;
    private int _exitReported;

    public PtySession(int cols, int rows)
    {
        _master = posix_openpt(ORdwr | ONoctty);
        if (_master < 0) throw new IOException($"posix_openpt nije uspeo (errno {Marshal.GetLastPInvokeError()})");

        try
        {
            fcntl(_master, FSetFd, FdCloexec);   // ljuska ne sme da nasledi master
            if (grantpt(_master) != 0 || unlockpt(_master) != 0)
                throw new IOException($"grantpt/unlockpt nije uspeo (errno {Marshal.GetLastPInvokeError()})");

            var name = new byte[256];
            if (ptsname_r(_master, name, name.Length) != 0)
                throw new IOException($"ptsname_r nije uspeo (errno {Marshal.GetLastPInvokeError()})");
            var slave = Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0));

            Resize(cols, rows);
            _process = StartShell(slave);
        }
        catch
        {
            close(_master);
            throw;
        }

        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "pty-read" };
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "pty-write" };
    }

    public event Action<byte[]>? Output;

    public event Action<int>? Exited;

    public void Start()
    {
        _reader.Start();
        _writer.Start();
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (!_disposed && data.Length > 0) _input.Writer.TryWrite(data.ToArray());
    }

    public void Resize(int cols, int rows)
    {
        var size = new WinSize { Rows = (ushort)Math.Clamp(rows, 1, 1000), Cols = (ushort)Math.Clamp(cols, 1, 1000) };
        ioctl(_master, TiocSwinsz, ref size);   // jezgro šalje SIGWINCH programu
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _input.Writer.TryComplete();
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { /* već je završio */ }

        // petlja čitanja prestaje najkasnije za 200 ms (poll), tek tada se zatvara master
        if (_reader.IsAlive) _reader.Join(1000);
        close(_master);
        _process.Dispose();
    }

    private static Process StartShell(string slave)
    {
        var shell = ResolveShell();
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home) || !Directory.Exists(home)) home = "/";

        var psi = new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            WorkingDirectory = home,
        };

        // $0 = slave uređaj, $@ = ljuska; stdin/stdout/stderr postaju slave, a setsid -c ga čini kontrolnim terminalom
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("exec setsid -c \"$@\" <\"$0\" >\"$0\" 2>&1");
        psi.ArgumentList.Add(slave);
        psi.ArgumentList.Add(shell);
        psi.ArgumentList.Add("-l");

        psi.Environment["TERM"] = "xterm-256color";
        psi.Environment["COLORTERM"] = "truecolor";
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LANG"))) psi.Environment["LANG"] = "C.UTF-8";

        return Process.Start(psi) ?? throw new IOException("ljuska se nije pokrenula");
    }

    private static string ResolveShell()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shell) && File.Exists(shell)) return shell;

        try
        {
            // /etc/passwd: ime:x:uid:gid:opis:home:ljuska
            var line = File.ReadLines("/etc/passwd").FirstOrDefault(l => l.StartsWith(Environment.UserName + ":"));
            var fromPasswd = line?.Split(':').LastOrDefault();
            if (!string.IsNullOrEmpty(fromPasswd) && File.Exists(fromPasswd)) return fromPasswd;
        }
        catch
        {
            // nema /etc/passwd: ide /bin/bash
        }

        return File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
    }

    private void ReadLoop()
    {
        var buffer = new byte[16384];
        var fds = new PollFd[1];
        fds[0].Fd = _master;
        fds[0].Events = PollIn;

        while (!_disposed)
        {
            fds[0].Revents = 0;
            var ready = poll(fds, 1, 200);
            if (ready < 0)
            {
                if (Marshal.GetLastPInvokeError() == Eintr) continue;
                break;
            }

            if (ready == 0) continue;

            if ((fds[0].Revents & (PollIn | PollHup | PollErr)) == 0) continue;

            var n = read(_master, buffer, buffer.Length);
            if (n > 0)
            {
                Output?.Invoke(buffer.AsSpan(0, (int)n).ToArray());
                continue;
            }

            if (n < 0 && Marshal.GetLastPInvokeError() == Eintr) continue;
            break;   // EIO: ljuska je zatvorila terminal
        }

        ReportExit();
    }

    private void WriteLoop()
    {
        var reader = _input.Reader;
        while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (reader.TryRead(out var chunk))
            {
                var offset = 0;
                while (offset < chunk.Length && !_disposed)
                {
                    var part = offset == 0 ? chunk : chunk[offset..];
                    var written = write(_master, part, part.Length);
                    if (written < 0)
                    {
                        if (Marshal.GetLastPInvokeError() == Eintr) continue;
                        return;
                    }

                    offset += (int)written;
                }
            }
        }
    }

    private void ReportExit()
    {
        if (Interlocked.Exchange(ref _exitReported, 1) != 0 || _disposed) return;

        var code = 0;
        try
        {
            if (_process.WaitForExit(1500)) code = _process.ExitCode;
        }
        catch
        {
            // proces je već oslobođen
        }

        Exited?.Invoke(code);
    }
}
