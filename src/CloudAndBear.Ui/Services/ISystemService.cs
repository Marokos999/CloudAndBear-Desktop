namespace CloudAndBear.Ui.Services;

/// <summary>
/// Stanje sistema i napajanje. Na uređaju čita Linux (<see cref="LinuxSystemService"/>), a u pregledu na PC-u
/// daje izmišljene podatke (<see cref="FakeSystemService"/>), da se izgled može dizajnirati bez Debiana.
/// Svaka metoda vraća null kad podatak ne postoji (npr. nema baterije).
/// </summary>
public interface ISystemService
{
    string OsName { get; }
    string Kernel { get; }
    string Hostname { get; }
    string CpuModel { get; }
    string TimeZone { get; }

    TimeSpan Uptime();

    /// <summary>(ukupno, mirovanje): procenat zauzeća procesora se dobija iz razlike dva uzorka.</summary>
    (long Total, long Idle)? CpuTimes();

    /// <summary>Zauzeta i ukupna memorija u bajtovima.</summary>
    (long Used, long Total)? Memory();

    /// <summary>Zauzet i ukupan prostor glavnog diska u bajtovima.</summary>
    (long Used, long Total)? Disk();

    (int Percent, string Status)? Battery();

    /// <summary>Vraća tekst greške, a null kad je naredba prihvaćena.</summary>
    Task<string?> RestartAsync();

    Task<string?> ShutdownAsync();

    /// <summary>Zatvara desktop i vraća računar na konzolu.</summary>
    void ExitToConsole();
}
