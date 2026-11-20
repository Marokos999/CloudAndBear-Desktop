namespace CloudAndBear.Ui.Input;

/// <summary>Izabrani raspored tastature. Na uređaju ga čita i menja ulaz (Alt+Shift), a traka ga prikazuje.</summary>
public interface IKeyboardLayoutService
{
    KeyboardLayout Current { get; }

    /// <summary>Raspored se promenio (može stići sa niti ulaza).</summary>
    event Action? Changed;

    /// <summary>Sledeći raspored po redu.</summary>
    void Next();
}

/// <summary>
/// Raspored sa pamćenjem izbora u fajl (stanje aplikacije: /var/lib/cloudandbear/layout). Bez fajla se ne pamti ništa
/// (pregled na PC-u). Čitanje je bezbedno sa bilo koje niti.
/// </summary>
public sealed class KeyboardLayoutService : IKeyboardLayoutService
{
    private readonly string? _stateFile;
    private volatile KeyboardLayout _current;

    public KeyboardLayoutService(string? stateFile = null)
    {
        _stateFile = stateFile;
        _current = Load();
    }

    /// <summary>Gde uređaj pamti raspored: systemd (StateDirectory=) javlja folder u STATE_DIRECTORY, inače /var/lib/cloudandbear.</summary>
    public static string DefaultStateFile() =>
        Path.Combine(Environment.GetEnvironmentVariable("STATE_DIRECTORY")?.Split(':')[0] ?? "/var/lib/cloudandbear", "layout");

    public KeyboardLayout Current => _current;

    public event Action? Changed;

    public void Next()
    {
        var all = KeyboardLayout.All;
        var index = 0;
        for (var i = 0; i < all.Count; i++)
            if (ReferenceEquals(all[i], _current)) index = i;

        _current = all[(index + 1) % all.Count];
        Save();
        Changed?.Invoke();
    }

    private KeyboardLayout Load()
    {
        if (_stateFile is null) return KeyboardLayout.Us;

        try
        {
            var name = File.ReadAllText(_stateFile).Trim();
            return KeyboardLayout.All.FirstOrDefault(l => l.Name == name) ?? KeyboardLayout.Us;
        }
        catch
        {
            return KeyboardLayout.Us;
        }
    }

    private void Save()
    {
        if (_stateFile is null) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
            File.WriteAllText(_stateFile, _current.Name);
        }
        catch
        {
            // nema prava ili mesta: raspored važi do izlaska iz aplikacije
        }
    }
}
