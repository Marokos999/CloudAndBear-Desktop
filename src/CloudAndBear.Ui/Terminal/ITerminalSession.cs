namespace CloudAndBear.Ui.Terminal;

/// <summary>
/// Jedan terminal sa programom na drugoj strani. Na Linuxu je to ljuska na pseudo-terminalu
/// (<see cref="PtyTerminalService"/>), a u pregledu na PC-u mala izmišljena ljuska (<see cref="FakeTerminalService"/>).
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>Izlaz programa; stiže sa tuđe niti, pa ga primalac sam prebacuje na UI.</summary>
    event Action<byte[]>? Output;

    /// <summary>Program je završio (kod izlaza); stiže sa tuđe niti.</summary>
    event Action<int>? Exited;

    /// <summary>Počinje da šalje izlaz. Poziva se tek kad su događaji povezani, da se ništa ne izgubi.</summary>
    void Start();

    /// <summary>Unos za program (tasteri, nalepljen tekst, odgovori emulatora).</summary>
    void Write(ReadOnlySpan<byte> data);

    void Resize(int cols, int rows);
}

public interface ITerminalService
{
    ITerminalSession Create(int cols, int rows);
}
