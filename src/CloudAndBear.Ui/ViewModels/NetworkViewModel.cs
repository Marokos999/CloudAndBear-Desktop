using Avalonia.Threading;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Red u tabeli veza.</summary>
public sealed record DeviceRow(string Name, string Type, string State, string Connection, string Address)
{
    public string TypeText => Type == "wifi" ? "Wi-Fi" : "žični";

    public string StateText => State.Split(' ')[0] switch
    {
        "connected" => "povezan",
        "connecting" => "povezuje se",
        "disconnected" => "nije povezan",
        "unavailable" => "nedostupan",
        "unmanaged" => "nije pod kontrolom",
        _ => State,
    };
}

/// <summary>Mreža: adapteri i Wi-Fi (spisak mreža, povezivanje, prekid veze).</summary>
public sealed class NetworkViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkService _network;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private IReadOnlyList<DeviceRow> _devices = [];
    private IReadOnlyList<WifiNetwork> _networks = [];
    private WifiNetwork? _selected;
    private string _password = "", _status = "", _problem = "", _wifiDevice = "";
    private bool _busy, _hasWifi, _restoring, _showPassword;

    public NetworkViewModel(INetworkService network)
    {
        _network = network;

        Scan = new AsyncRelayCommand(() => ScanCoreAsync(rescan: true, report: true), () => !Busy && HasWifi);
        Connect = new AsyncRelayCommand(ConnectAsync, () => !Busy && Selected is not null);
        Disconnect = new AsyncRelayCommand(DisconnectAsync, () => !Busy && Selected is { Active: true });

        _timer.Tick += async (_, _) => await RefreshDevicesAsync();
        _timer.Start();
        Ready = LoadAsync();
    }

    /// <summary>Završeno prvo učitavanje (adapteri i skeniranje); služi testu.</summary>
    public Task Ready { get; }

    public AsyncRelayCommand Scan { get; }
    public AsyncRelayCommand Connect { get; }
    public AsyncRelayCommand Disconnect { get; }

    public IReadOnlyList<DeviceRow> Devices { get => _devices; private set => Set(ref _devices, value); }
    public IReadOnlyList<WifiNetwork> Networks { get => _networks; private set => Set(ref _networks, value); }

    /// <summary>Izabrana Wi-Fi mreža; prazna vrednost od liste dok se ona osvežava se ignoriše (izbor se vraća po imenu).</summary>
    public WifiNetwork? Selected
    {
        get => _selected;
        set
        {
            if (value is null && _restoring) return;
            SetSelected(value);
        }
    }

    public string Password { get => _password; set => Set(ref _password, value); }

    /// <summary>Prikaz lozinke slovima umesto tačkica (da se vidi šta je ukucano, npr. srpska slova).</summary>
    public bool ShowPassword { get => _showPassword; set => Set(ref _showPassword, value); }

    public string Status { get => _status; private set => Set(ref _status, value); }

    public string Problem
    {
        get => _problem;
        private set
        {
            if (Set(ref _problem, value)) Raise(nameof(HasProblem));
        }
    }

    public bool HasProblem => Problem.Length > 0;

    public bool HasWifi
    {
        get => _hasWifi;
        private set
        {
            if (Set(ref _hasWifi, value)) UpdateCommands();
        }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value)) UpdateCommands();
        }
    }

    public bool HasSelection => Selected is not null;
    public bool NeedsPassword => Selected is { Security.Length: > 0 };
    public bool CanDisconnect => Selected is { Active: true };
    public string SelectedTitle => Selected is { } n ? $"Mreža: {n.Ssid}" : "";

    public void Dispose() => _timer.Stop();

    private async Task LoadAsync()
    {
        await RefreshDevicesAsync();
        await ScanCoreAsync(rescan: true, report: true);
    }

    private void SetSelected(WifiNetwork? value)
    {
        _selected = value;
        Password = "";
        ShowPassword = false;
        Raise(nameof(Selected));
        Raise(nameof(HasSelection));
        Raise(nameof(NeedsPassword));
        Raise(nameof(CanDisconnect));
        Raise(nameof(SelectedTitle));
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        Scan.RaiseCanExecuteChanged();
        Connect.RaiseCanExecuteChanged();
        Disconnect.RaiseCanExecuteChanged();
    }

    /// <summary>Adapteri i njihove adrese; ide na svakih pet sekundi dok je prozor otvoren.</summary>
    public async Task RefreshDevicesAsync()
    {
        var (devices, error) = await _network.DevicesAsync();
        if (error is not null)
        {
            Problem = error.Contains("not running") ? "NetworkManager nije pokrenut." : error;
            HasWifi = false;
            return;
        }

        Problem = devices.Count == 0 ? "Nema mrežnih adaptera." : "";
        var rows = devices.Select(d => new DeviceRow(d.Name, d.Type, d.State, d.Connection, _network.Ipv4(d.Name))).ToList();
        if (!rows.SequenceEqual(Devices)) Devices = rows;

        var wifi = devices.FirstOrDefault(d => d.Type == "wifi");
        _wifiDevice = wifi?.Name ?? "";
        HasWifi = wifi is not null;
        if (wifi is null && Networks.Count == 0 && !Busy) Status = "Nema Wi-Fi adaptera.";
    }

    private async Task ScanCoreAsync(bool rescan, bool report)
    {
        if (Busy) return;
        Busy = true;
        if (report) Status = "Skeniram Wi-Fi mreže…";
        try
        {
            var (list, error) = await _network.ScanAsync(rescan);
            if (error is not null)
            {
                if (report) Status = error;
                return;
            }

            var keep = Selected?.Ssid;
            _restoring = true;
            Networks = list;
            _restoring = false;
            SetSelected(list.FirstOrDefault(n => n.Ssid == keep));   // lista je zamenjena: izbor se vraća po imenu
            if (report) Status = list.Count == 0 ? "Nema mreža u dometu." : $"Nađeno mreža: {list.Count}.";
        }
        finally
        {
            _restoring = false;
            Busy = false;
        }
    }

    private async Task ConnectAsync()
    {
        if (Busy || Selected is not { } net) return;

        var password = Password;
        Busy = true;
        Status = $"Povezujem se na {net.Ssid}…";
        try
        {
            var r = await _network.ConnectAsync(net.Ssid, password);
            Status = r.Ok ? $"Povezano na {net.Ssid}." : _network.Explain(r);
        }
        finally
        {
            Busy = false;
        }

        await RefreshDevicesAsync();
        await RefreshAfterChangeAsync();
    }

    private async Task DisconnectAsync()
    {
        if (Busy || _wifiDevice.Length == 0) return;

        Busy = true;
        Status = "Prekidam vezu…";
        try
        {
            var r = await _network.DisconnectAsync(_wifiDevice);
            Status = r.Ok ? "Veza je prekinuta." : _network.Explain(r);
        }
        finally
        {
            Busy = false;
        }

        await RefreshDevicesAsync();
        await RefreshAfterChangeAsync();
    }

    // posle povezivanja ili prekida: spisak bez novog skeniranja, da oznaka "povezan" bude tačna (poruka ostaje)
    private Task RefreshAfterChangeAsync() => ScanCoreAsync(rescan: false, report: false);
}
