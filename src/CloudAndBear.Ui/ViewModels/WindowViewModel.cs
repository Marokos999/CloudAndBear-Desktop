using System.Windows.Input;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Jedan prozor na desktopu: položaj, veličina i stanje.</summary>
public sealed class WindowViewModel : ViewModelBase
{
    public const double TitleBarHeight = 36;
    public const double MinWidth = 320;
    public const double MinHeight = 180;

    private readonly DesktopViewModel _desktop;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isMaximized;
    private bool _isMinimized;
    private bool _isActive;
    private int _zIndex;

    public WindowViewModel(DesktopViewModel desktop, AppInfo app, double x, double y)
    {
        _desktop = desktop;
        App = app;
        Content = app.CreateContent();
        _x = x;
        _y = y;
        _width = app.Width;
        _height = app.Height;

        ActivateCommand = new RelayCommand(() => desktop.Activate(this));
        MinimizeCommand = new RelayCommand(() => desktop.Minimize(this));
        ToggleMaximizeCommand = new RelayCommand(() => IsMaximized = !IsMaximized);
        CloseCommand = new RelayCommand(() => desktop.Close(this));
        PanelCommand = new RelayCommand(() => desktop.ToggleFromPanel(this));

        // aplikacija može sama da zatraži zatvaranje (terminal kad ljuska završi)
        if (Content is IClosableContent closable) closable.CloseRequested += () => desktop.Close(this);
    }

    public AppInfo App { get; }
    public string Title => App.Title;
    public string IconData => App.IconData;
    public object Content { get; }

    public ICommand ActivateCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand ToggleMaximizeCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand PanelCommand { get; }

    // stvarni položaj: maksimizovan prozor prati radnu površinu
    public double Left => IsMaximized ? 0 : _x;
    public double Top => IsMaximized ? 0 : _y;
    public double Width => IsMaximized ? _desktop.WorkWidth : _width;
    public double Height => IsMaximized ? _desktop.WorkHeight : _height;

    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (!Set(ref _isMaximized, value)) return;
            Raise(nameof(MaximizeIconData));
            RefreshBounds();
        }
    }

    public bool IsMinimized
    {
        get => _isMinimized;
        internal set
        {
            if (!Set(ref _isMinimized, value)) return;
            Raise(nameof(IsOnDesktop));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        internal set => Set(ref _isActive, value);
    }

    public int ZIndex
    {
        get => _zIndex;
        internal set => Set(ref _zIndex, value);
    }

    public bool IsOnDesktop => !IsMinimized;

    public string MaximizeIconData => IsMaximized ? Icons.Restore : Icons.Maximize;

    /// <summary>Pogled sluša ovo i prebacuje tastaturu u sadržaj prozora.</summary>
    public event Action? FocusRequested;

    internal void RequestFocus() => FocusRequested?.Invoke();

    /// <summary>Pomeranje tastaturom: relativno od trenutnog položaja.</summary>
    public void MoveBy(double dx, double dy) => MoveTo(_x + dx, _y + dy);

    /// <summary>Promena veličine tastaturom: donja desna ivica se pomera.</summary>
    public void ResizeBy(double dw, double dh) => Resize("SE", _x, _y, _width, _height, dw, dh);

    public void MoveTo(double x, double y)
    {
        if (IsMaximized) return;

        // naslovna traka uvek ostaje dohvatljiva
        var minX = 80 - _width;
        _x = Math.Clamp(x, minX, Math.Max(minX, _desktop.WorkWidth - 80));
        _y = Math.Clamp(y, 0, Math.Max(0, _desktop.WorkHeight - TitleBarHeight));
        RefreshBounds();
    }

    /// <summary>Promena veličine sa ivice ili ugla ("N", "SE", ...), računa se od početnih mera.</summary>
    public void Resize(string edge, double startX, double startY, double startWidth, double startHeight, double dx, double dy)
    {
        if (IsMaximized) return;

        double x = startX, y = startY, w = startWidth, h = startHeight;

        if (edge.Contains('E')) w = Math.Max(MinWidth, startWidth + dx);
        if (edge.Contains('S')) h = Math.Max(MinHeight, startHeight + dy);

        if (edge.Contains('W'))
        {
            var right = startX + startWidth;
            x = Math.Min(startX + dx, right - MinWidth);
            w = right - x;
        }

        if (edge.Contains('N'))
        {
            var bottom = startY + startHeight;
            y = Math.Clamp(startY + dy, 0, bottom - MinHeight);
            h = bottom - y;
        }

        _x = x;
        _y = y;
        _width = w;
        _height = h;
        RefreshBounds();
    }

    internal void RefreshBounds()
    {
        Raise(nameof(Left));
        Raise(nameof(Top));
        Raise(nameof(Width));
        Raise(nameof(Height));
    }
}
