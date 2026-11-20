using System.Windows.Input;
using Avalonia.Threading;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Sistem: opterećenje (procesor, memorija, disk, baterija), podaci o računaru i napajanje.</summary>
public sealed class SystemViewModel : ViewModelBase, IDisposable
{
    private readonly ISystemService _system;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private (long Total, long Idle)? _lastCpu;
    private double _cpuPercent, _memoryPercent, _diskPercent;
    private string _cpuText = "", _memoryText = "", _diskText = "", _batteryText = "", _uptimeText = "";
    private string? _pending;
    private string _message = "";

    public SystemViewModel(ISystemService system)
    {
        _system = system;

        Restart = new RelayCommand(() => Pending = "restart");
        Shutdown = new RelayCommand(() => Pending = "shutdown");
        ExitToConsole = new RelayCommand(system.ExitToConsole);
        Confirm = new AsyncRelayCommand(RunPendingAsync);
        Cancel = new RelayCommand(() => Pending = null);

        Refresh();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    // podaci o računaru
    public string OsName => _system.OsName;
    public string Kernel => _system.Kernel;
    public string Hostname => _system.Hostname;
    public string Cpu => _system.CpuModel;
    public string TimeZone => _system.TimeZone;

    // opterećenje
    public double CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }
    public string CpuText { get => _cpuText; private set => Set(ref _cpuText, value); }
    public double MemoryPercent { get => _memoryPercent; private set => Set(ref _memoryPercent, value); }
    public string MemoryText { get => _memoryText; private set => Set(ref _memoryText, value); }
    public double DiskPercent { get => _diskPercent; private set => Set(ref _diskPercent, value); }
    public string DiskText { get => _diskText; private set => Set(ref _diskText, value); }
    public string BatteryText { get => _batteryText; private set => Set(ref _batteryText, value); }
    public string UptimeText { get => _uptimeText; private set => Set(ref _uptimeText, value); }

    // napajanje
    public ICommand Restart { get; }
    public ICommand Shutdown { get; }
    public ICommand ExitToConsole { get; }
    public AsyncRelayCommand Confirm { get; }
    public ICommand Cancel { get; }

    /// <summary>Radnja koja čeka potvrdu ("restart" ili "shutdown"), inače null.</summary>
    public string? Pending
    {
        get => _pending;
        private set
        {
            if (!Set(ref _pending, value)) return;
            Raise(nameof(IsPending));
            Raise(nameof(Question));
        }
    }

    public bool IsPending => Pending is not null;
    public string Question => Pending == "restart" ? "Restartovati računar?" : "Isključiti računar?";

    public string Message
    {
        get => _message;
        private set
        {
            if (Set(ref _message, value)) Raise(nameof(HasMessage));
        }
    }

    public bool HasMessage => Message.Length > 0;

    public void Refresh()
    {
        // procesor: procenat zauzeća je razlika dva uzorka
        var cpu = _system.CpuTimes();
        if (cpu is { } now && _lastCpu is { } before && now.Total > before.Total)
            CpuPercent = 100.0 * (1 - (double)(now.Idle - before.Idle) / (now.Total - before.Total));
        _lastCpu = cpu;
        CpuText = cpu is null ? "nema podatka" : $"{CpuPercent:0} %";

        if (_system.Memory() is { } mem)
        {
            MemoryPercent = 100.0 * mem.Used / mem.Total;
            MemoryText = $"{Gb(mem.Used)} / {Gb(mem.Total)}";
        }
        else MemoryText = "nema podatka";

        if (_system.Disk() is { } disk)
        {
            DiskPercent = 100.0 * disk.Used / disk.Total;
            DiskText = $"{Gb(disk.Used)} / {Gb(disk.Total)}";
        }
        else DiskText = "nema podatka";

        BatteryText = _system.Battery() is { } bat ? $"{bat.Percent} %  ·  {bat.Status}" : "nema baterije";

        var up = _system.Uptime();
        UptimeText = $"{(int)up.TotalHours} h {up.Minutes} min";
    }

    public void Dispose() => _timer.Stop();

    private async Task RunPendingAsync()
    {
        var restart = Pending == "restart";
        Pending = null;
        Message = await (restart ? _system.RestartAsync() : _system.ShutdownAsync()) ?? "";
    }

    private static string Gb(long bytes) => $"{bytes / 1073741824.0:0.0} GB";
}
