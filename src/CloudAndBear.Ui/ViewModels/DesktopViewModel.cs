using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Ceo desktop: prozori, pokretač i sat na traci.</summary>
public sealed class DesktopViewModel : ViewModelBase
{
    private const double KeyboardStep = 32;

    private readonly List<AppEntryViewModel> _entries;
    private int _zCounter;
    private double _workWidth = 1280;
    private double _workHeight = 720;
    private bool _isLauncherOpen;
    private string _launcherQuery = "";
    private string _clockText = "";

    public DesktopViewModel(DesktopServices services)
    {
        Services = services;

        AppInfo[] apps =
        [
            new("Terminal", Icons.Terminal, () => new TerminalViewModel(services.Terminal, 90, 26), Width: 820, Height: 520, AllowMultiple: true),
            new("Mreža", Icons.Network, () => new NetworkViewModel(services.Network), Width: 780, Height: 620),
            new("Kontejneri", Icons.Containers, () => new ContainersViewModel(services.Containers), Width: 860, Height: 540),
            new("Kubernetes", Icons.Kubernetes, () => new KubernetesViewModel(services.Kube), Width: 960, Height: 580),
            new("Sistem", Icons.System, () => new SystemViewModel(services.System), Width: 720, Height: 620),
            new("Stil", Icons.Style, () => new StyleGuideViewModel(), Width: 760, Height: 520),
        ];

        _entries = apps.Select(a => new AppEntryViewModel(a, Open)).ToList();
        SelectFirst();

        ToggleLauncherCommand = new RelayCommand(ToggleLauncher);
        CloseLauncherCommand = new RelayCommand(() => CloseLauncher());

        UpdateClock();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateClock();
        timer.Start();
    }

    public DesktopServices Services { get; }

    public ObservableCollection<WindowViewModel> Windows { get; } = new();

    public ICommand ToggleLauncherCommand { get; }
    public ICommand CloseLauncherCommand { get; }

    public WindowViewModel? ActiveWindow => Windows.FirstOrDefault(w => w.IsActive);

    /// <summary>Površina iznad trake: u njoj žive prozori (maksimizovan prozor popunjava baš nju).</summary>
    public double WorkWidth => _workWidth;

    public double WorkHeight => _workHeight;

    public string ClockText
    {
        get => _clockText;
        private set => Set(ref _clockText, value);
    }

    public bool IsLauncherOpen
    {
        get => _isLauncherOpen;
        set
        {
            if (!Set(ref _isLauncherOpen, value)) return;
            if (!value) return;

            LauncherQuery = "";
            SelectFirst();
        }
    }

    public string LauncherQuery
    {
        get => _launcherQuery;
        set
        {
            if (!Set(ref _launcherQuery, value)) return;
            Raise(nameof(FilteredApps));
            SelectFirst();
        }
    }

    public IEnumerable<AppEntryViewModel> FilteredApps =>
        string.IsNullOrWhiteSpace(_launcherQuery)
            ? _entries
            : _entries.Where(e => e.Title.Contains(_launcherQuery.Trim(), StringComparison.CurrentCultureIgnoreCase));

    public void SetWorkArea(double width, double height)
    {
        _workWidth = width;
        _workHeight = height;
        foreach (var window in Windows) window.RefreshBounds();
    }

    // ---- pokretač ----

    private void ToggleLauncher()
    {
        if (IsLauncherOpen) CloseLauncher();
        else IsLauncherOpen = true;
    }

    /// <summary>Zatvara pokretač i vraća tastaturu aktivnom prozoru (osim kad se odmah aktivira drugi).</summary>
    public void CloseLauncher(bool restoreFocus = true)
    {
        if (!IsLauncherOpen) return;

        IsLauncherOpen = false;
        if (restoreFocus) ActiveWindow?.RequestFocus();
    }

    /// <summary>Strelica gore/dole u pokretaču (kruži).</summary>
    public void MoveSelection(int step)
    {
        var list = FilteredApps.ToList();
        if (list.Count == 0) return;

        var index = list.FindIndex(e => e.IsSelected);
        Select(list[((index < 0 ? 0 : index + step) % list.Count + list.Count) % list.Count]);
    }

    /// <summary>Enter u pokretaču: otvara izabranu aplikaciju.</summary>
    public void OpenSelected()
    {
        var entry = FilteredApps.FirstOrDefault(e => e.IsSelected) ?? FilteredApps.FirstOrDefault();
        if (entry is not null) Open(entry.App);
    }

    private void SelectFirst() => Select(FilteredApps.FirstOrDefault());

    private void Select(AppEntryViewModel? selected)
    {
        foreach (var entry in _entries) entry.IsSelected = entry == selected;
    }

    // ---- prozori ----

    public void Open(AppInfo app)
    {
        var existing = Windows.FirstOrDefault(w => w.App == app);
        if (existing is not null && !app.AllowMultiple)
        {
            Activate(existing);
            return;
        }

        // novi prozori se kaskadno pomeraju da se ne poklope
        var offset = 32 * (Windows.Count % 8);
        var window = new WindowViewModel(this, app, 60 + offset, 40 + offset);
        Windows.Add(window);
        Activate(window);
    }

    public void Activate(WindowViewModel window)
    {
        CloseLauncher(restoreFocus: false);
        if (window.IsActive && !window.IsMinimized) return;

        foreach (var w in Windows) w.IsActive = w == window;
        window.IsMinimized = false;
        window.ZIndex = ++_zCounter;
        window.RequestFocus();
    }

    public void Minimize(WindowViewModel window)
    {
        window.IsMinimized = true;
        window.IsActive = false;
        ActivateTopmost();
    }

    public void Close(WindowViewModel window)
    {
        Windows.Remove(window);
        (window.Content as IDisposable)?.Dispose();   // zaustavlja časovnike aplikacije
        ActivateTopmost();
    }

    /// <summary>Klik na dugme prozora na traci: aktivan se minimizuje, ostali dolaze u prvi plan.</summary>
    public void ToggleFromPanel(WindowViewModel window)
    {
        if (window.IsActive && !window.IsMinimized) Minimize(window);
        else Activate(window);
    }

    // ---- tastatura ----

    /// <summary>Alt+Tab: sledeći (1) ili prethodni (-1) prozor po redu otvaranja; minimizovan se vraća.</summary>
    public void ActivateNext(int step)
    {
        if (Windows.Count == 0) return;

        var index = Windows.IndexOf(ActiveWindow!);
        var next = ((index < 0 ? 0 : index + step) % Windows.Count + Windows.Count) % Windows.Count;
        Activate(Windows[next]);
    }

    public void CloseActive()
    {
        if (ActiveWindow is { } window) Close(window);
    }

    public void MaximizeActive()
    {
        if (ActiveWindow is { } window) window.IsMaximized = true;
    }

    /// <summary>Alt+Dole: maksimizovan se vraća, a običan se minimizuje.</summary>
    public void RestoreOrMinimizeActive()
    {
        if (ActiveWindow is not { } window) return;

        if (window.IsMaximized) window.IsMaximized = false;
        else Minimize(window);
    }

    public void MoveActive(int dx, int dy) => ActiveWindow?.MoveBy(dx * KeyboardStep, dy * KeyboardStep);

    public void ResizeActive(int dw, int dh) => ActiveWindow?.ResizeBy(dw * KeyboardStep, dh * KeyboardStep);

    private void ActivateTopmost()
    {
        var top = Windows.Where(w => !w.IsMinimized).OrderByDescending(w => w.ZIndex).FirstOrDefault();
        if (top is not null) Activate(top);
    }

    private void UpdateClock() => ClockText = DateTime.Now.ToString("HH:mm");
}
