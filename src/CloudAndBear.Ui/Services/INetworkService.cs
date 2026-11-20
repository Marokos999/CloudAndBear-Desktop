namespace CloudAndBear.Ui.Services;

/// <summary>Mrežni adapter (žični ili Wi-Fi) kako ga javlja NetworkManager.</summary>
public sealed record NetDevice(string Name, string Type, string State, string Connection);

/// <summary>Wi-Fi mreža u dometu; Signal je 0..100.</summary>
public sealed record WifiNetwork(string Ssid, int Signal, string Security, bool Active)
{
    public string SignalText => $"{Signal} %";
    public string SecurityText => Security.Length == 0 ? "otvorena" : Security;
    public string ActiveText => Active ? "povezan" : "";
}

/// <summary>
/// Mreža: adapteri i Wi-Fi. Na uređaju to je NetworkManager (<see cref="NetworkManagerService"/>), a u pregledu
/// na PC-u izmišljeni adapteri i mreže (<see cref="FakeNetworkService"/>).
/// </summary>
public interface INetworkService
{
    Task<(IReadOnlyList<NetDevice> Devices, string? Error)> DevicesAsync();

    Task<(IReadOnlyList<WifiNetwork> Networks, string? Error)> ScanAsync(bool rescan);

    /// <summary>Prazna lozinka znači "koristi sačuvanu vezu" (ili otvorenu mrežu).</summary>
    Task<CommandResult> ConnectAsync(string ssid, string? password);

    Task<CommandResult> DisconnectAsync(string device);

    /// <summary>Prva IPv4 adresa adaptera, ili "—" kad je nema.</summary>
    string Ipv4(string device);

    /// <summary>Poruka greške na srpskom za slučajeve koje korisnik zaista vidi.</summary>
    string Explain(CommandResult result);
}
