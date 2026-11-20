using System.Globalization;

namespace CloudAndBear.Ui.Services;

/// <summary>Stanje sistema iz /proc i /sys; napajanje preko systemctl-a (polkit pravila dozvoljavaju nalogu cloudandbear).</summary>
public sealed class LinuxSystemService : ISystemService
{
    public string OsName
    {
        get
        {
            var line = Read("/etc/os-release")?.Split('\n').FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
            return line is null ? "Linux" : line["PRETTY_NAME=".Length..].Trim('"');
        }
    }

    public string Kernel => Read("/proc/sys/kernel/osrelease") ?? "?";

    public string Hostname => Environment.MachineName;

    public string CpuModel
    {
        get
        {
            var model = Read("/proc/cpuinfo")?.Split('\n')
                .FirstOrDefault(l => l.StartsWith("model name"))?.Split(':', 2)[1].Trim();
            return $"{model ?? "procesor"}, {Environment.ProcessorCount} jezgara";
        }
    }

    public string TimeZone => TimeZoneInfo.Local.Id;

    public TimeSpan Uptime() =>
        double.TryParse(Read("/proc/uptime")?.Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromMilliseconds(Environment.TickCount64);

    public (long Total, long Idle)? CpuTimes()
    {
        // prva linija: cpu  user nice system idle iowait irq softirq steal ...
        var line = Read("/proc/stat")?.Split('\n')[0];
        if (line is null || !line.StartsWith("cpu ")) return null;

        var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(long.Parse).ToArray();
        return f.Length < 5 ? null : (f.Sum(), f[3] + f[4]);
    }

    public (long Used, long Total)? Memory()
    {
        var text = Read("/proc/meminfo");
        if (text is null) return null;

        long? Kb(string key) => text.Split('\n')
            .Where(l => l.StartsWith(key + ":"))
            .Select(l => (long?)long.Parse(l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]))
            .FirstOrDefault();

        return Kb("MemTotal") is { } total && Kb("MemAvailable") is { } free
            ? ((total - free) * 1024, total * 1024)
            : null;
    }

    public (long Used, long Total)? Disk()
    {
        try
        {
            var d = new DriveInfo("/");
            return (d.TotalSize - d.AvailableFreeSpace, d.TotalSize);
        }
        catch
        {
            return null;
        }
    }

    public (int Percent, string Status)? Battery()
    {
        const string dir = "/sys/class/power_supply";
        if (!Directory.Exists(dir)) return null;

        var bat = Directory.GetDirectories(dir, "BAT*").FirstOrDefault();
        if (bat is null || !int.TryParse(Read(bat + "/capacity"), out var percent)) return null;

        var status = Read(bat + "/status") switch
        {
            "Charging" => "puni se",
            "Discharging" => "prazni se",
            "Full" => "puna",
            "Not charging" => "ne puni se",
            var s => s ?? "?",
        };
        return (percent, status);
    }

    public Task<string?> RestartAsync() => SystemctlAsync("reboot");

    public Task<string?> ShutdownAsync() => SystemctlAsync("poweroff");

    public void ExitToConsole() => Environment.Exit(0);

    private static async Task<string?> SystemctlAsync(string verb)
    {
        var r = await ProcessRunner.RunAsync("systemctl", [verb]);
        return r.Ok ? null : ProcessRunner.FirstLine(r);
    }

    private static string? Read(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return null;
        }
    }
}
