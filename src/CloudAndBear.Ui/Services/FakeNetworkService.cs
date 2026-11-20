namespace CloudAndBear.Ui.Services;

/// <summary>
/// Izmišljena mreža za pregled na PC-u: jedan žični adapter bez kabla i jedan Wi-Fi sa nekoliko mreža.
/// "Kuca-5G" je sačuvana (povezuje se bez lozinke), a lozinka "pogresna" uvek daje grešku.
/// </summary>
public sealed class FakeNetworkService : INetworkService
{
    private string? _active = "Kuca-5G";

    private readonly (string Ssid, int Signal, string Security)[] _air =
    [
        ("Kuca-5G", 82, "WPA2"),
        ("Komsija", 61, "WPA2"),
        ("Kafic Free WiFi", 44, ""),
        ("Lab-DevOps", 38, "WPA2"),
        ("Telekom_A1B2", 29, "WPA2"),
    ];

    /// <summary>Koliko traje svaka radnja (da se vidi stanje "povezujem se"); u testu nula.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(700);

    public async Task<(IReadOnlyList<NetDevice> Devices, string? Error)> DevicesAsync()
    {
        await Task.Delay(Delay / 4);
        IReadOnlyList<NetDevice> devices =
        [
            new("eth0", "ethernet", "unavailable", ""),
            _active is null
                ? new("wlan0", "wifi", "disconnected", "")
                : new("wlan0", "wifi", "connected", _active),
        ];
        return (devices, null);
    }

    public async Task<(IReadOnlyList<WifiNetwork> Networks, string? Error)> ScanAsync(bool rescan)
    {
        await Task.Delay(rescan ? Delay : Delay / 4);
        IReadOnlyList<WifiNetwork> networks = _air
            .Select(n => new WifiNetwork(n.Ssid, n.Signal, n.Security, n.Ssid == _active))
            .OrderByDescending(n => n.Active)
            .ThenByDescending(n => n.Signal)
            .ToList();
        return (networks, null);
    }

    public async Task<CommandResult> ConnectAsync(string ssid, string? password)
    {
        await Task.Delay(Delay);

        var net = _air.FirstOrDefault(n => n.Ssid == ssid);
        if (net.Ssid is null) return new CommandResult(10, "", $"Error: No network with SSID '{ssid}' found.");

        var needsPassword = net.Security.Length > 0 && ssid != "Kuca-5G";
        if ((needsPassword && string.IsNullOrEmpty(password)) || password == "pogresna")
            return new CommandResult(4, "", "Error: Connection activation failed: Secrets were required, but not provided.");

        _active = ssid;
        return new CommandResult(0, $"Device 'wlan0' successfully activated with '{ssid}'.", "");
    }

    public async Task<CommandResult> DisconnectAsync(string device)
    {
        await Task.Delay(Delay / 2);
        _active = null;
        return new CommandResult(0, $"Device '{device}' successfully disconnected.", "");
    }

    public string Ipv4(string device) => device switch
    {
        "wlan0" when _active is not null => "192.168.1.5",
        _ => "—",
    };

    public string Explain(CommandResult r)
    {
        var all = r.Error + "\n" + r.Output;
        if (all.Contains("Secrets were required")) return "Lozinka nedostaje ili nije tačna.";
        if (all.Contains("No network with SSID")) return "Mreža nije u dometu.";
        return r.Error.Length > 0 ? r.Error : "nepoznata greška";
    }
}
