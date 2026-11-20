namespace CloudAndBear.Ui.Services;

/// <summary>Izmišljeni podaci za pregled na PC-u. Procesor se menja po talasu da se vidi kako traka radi.</summary>
public sealed class FakeSystemService : ISystemService
{
    private const long Gb = 1073741824;

    private readonly DateTime _started = DateTime.Now.AddHours(-3.4);
    private long _total;
    private long _idle;
    private int _tick;

    public string OsName => "Debian GNU/Linux 13 (trixie)";
    public string Kernel => "6.12.57+deb13-amd64";
    public string Hostname => "cloudandbear";
    public string CpuModel => "13th Gen Intel(R) Core(TM) i9-13900H, 20 jezgara";
    public string TimeZone => "Europe/Belgrade";

    /// <summary>Poslednja tražena radnja napajanja ("restart", "shutdown", "console") za proveru u testu.</summary>
    public string? LastAction { get; private set; }

    public TimeSpan Uptime() => DateTime.Now - _started;

    public (long Total, long Idle)? CpuTimes()
    {
        // svaki poziv je jedan uzorak: opterećenje ide između 15 % i 65 %
        var load = 0.4 + 0.25 * Math.Sin(_tick++ / 2.5);
        _total += 1000;
        _idle += (long)(1000 * (1 - load));
        return (_total, _idle);
    }

    public (long Used, long Total)? Memory() => ((long)(6.4 * Gb), (long)(15.4 * Gb));

    public (long Used, long Total)? Disk() => (187 * Gb, 476 * Gb);

    public (int Percent, string Status)? Battery() => (78, "prazni se");

    public Task<string?> RestartAsync()
    {
        LastAction = "restart";
        return Task.FromResult<string?>(null);
    }

    public Task<string?> ShutdownAsync()
    {
        LastAction = "shutdown";
        return Task.FromResult<string?>(null);
    }

    public void ExitToConsole() => LastAction = "console";
}
