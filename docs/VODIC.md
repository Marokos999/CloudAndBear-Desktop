# Vodič: desktop sa prozorima, trakom, pokretačem i tastaturom

Četiri koraka, svaki se prevodi i vidi se rezultat. Kod kucaš ti; sve iz vodiča je prethodno ukucano u praznu kopiju projekta, prevedeno bez grešaka i proveravano headless testom (prevlačenje, promena veličine, fokus, minimizuj, maksimizuj, traka, pokretač, sve prečice i strelice u pokretaču: 52 provere).

> **Stanje 2026-11-20:** na tvoj zahtev ("napiši ti") kod iz koraka 0–4 je već u `src` (kopiran iz proverene kopije), pa ga ne treba kucati. Vodič ostaje kao objašnjenje šta je čemu i redom kojim je građeno.

Svi putevi su relativni na `D:\CloudAndBear Desktop`. Odluke: traka je dole (varijanta A), prozori su Avalonia kontrole unutar jednog pogleda (vidi `docs/DESKTOP.md`).

Pokretanje posle svakog koraka:

```powershell
dotnet run --project src/CloudAndBear.Preview
```

Slika bez prozora (za proveru ili da mi je pošalješ):

```powershell
dotnet run --project tools/CloudAndBear.Snapshot -- snapshots 1280x720
```

Kako je podeljeno: `ViewModels/` drži stanje i logiku (položaj prozora, fokus, pokretač) i ne zna ništa o izgledu; `Views/` samo crta i hvata miša; `Theme/` su stilovi. Izgled se menja bez diranja logike.

## Korak 0: preimenuj MainView u StyleGuideView

Dosadašnji `MainView` je vodič kroz stil; od sada je on jedan od prozora, a koren je novi `DesktopView`. Najlakše: u Rideru ili Visual Studiju F2 na klasu `MainView` (preimenuje i fajlove). Ručno:

1. `Views/MainView.axaml` postaje `Views/StyleGuideView.axaml`, a `Views/MainView.axaml.cs` postaje `Views/StyleGuideView.axaml.cs`.
2. U `StyleGuideView.axaml`: `x:Class="CloudAndBear.Ui.Views.StyleGuideView"`.
3. U `StyleGuideView.axaml.cs`: klasa i konstruktor se zovu `StyleGuideView`.

Prevođenje će pasti dok ne završiš korak 1 (`App.axaml.cs` i `PreviewWindow.axaml` još pominju `MainView`), to je očekivano.

## Korak 1: prozori

Logika (ViewModel-i) ide cela odmah, pa pokretač i sat u koracima 2 i 3 samo dobijaju izgled. Na kraju ovog koraka vidiš dva prozora (Terminal i Stil) koja možeš da prevlačiš i menjaš im veličinu.

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/ViewModelBase.cs`

Osnova svih ViewModel-a: javlja pogledu kad se nešto promeni.

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CloudAndBear.Ui.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/RelayCommand.cs`

Najjednostavnija komanda za dugmad.

```csharp
using System.Windows.Input;

namespace CloudAndBear.Ui.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action execute) => _execute = _ => execute();

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/AppInfo.cs`

Opis jedne aplikacije (naslov, ikona, sadržaj, početna veličina).

```csharp
namespace CloudAndBear.Ui.ViewModels;

/// <summary>Opis aplikacije: naslov, ikona, kako se pravi sadržaj prozora i kolika je početna veličina.</summary>
public sealed record AppInfo(
    string Title,
    string IconData,
    Func<object> CreateContent,
    double Width = 640,
    double Height = 420,
    bool AllowMultiple = false);
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/Icons.cs`

Ikone kao putanje (linije na mreži 16x16). Prave se bez slika: kasnije se menjaju na jednom mestu.

```csharp
namespace CloudAndBear.Ui.ViewModels;

/// <summary>Ikone kao putanje na mreži 16x16 (crta ih Path sa Stroke, bez popune).</summary>
public static class Icons
{
    public const string Terminal = "M2,4 L6,8 L2,12 M8,12 L14,12";

    public const string Network =
        "M2,6 A8,8 0 0 1 14,6 M4.2,8.6 A5,5 0 0 1 11.8,8.6 M6.4,11.2 A2,2 0 0 1 9.6,11.2 M8,13.4 L8,13.5";

    public const string Containers =
        "M8,1.5 L14,4.75 L14,11.25 L8,14.5 L2,11.25 L2,4.75 Z M2,4.75 L8,8 L14,4.75 M8,8 L8,14.5";

    public const string Kubernetes =
        "M8,1.5 L13.6,4.75 L13.6,11.25 L8,14.5 L2.4,11.25 L2.4,4.75 Z M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8";

    public const string System = "M2.5,11.5 A6,6 0 1 1 13.5,11.5 M8,10 L11,6.5";

    public const string Style = "M8,2 C8,2 3.5,7 3.5,10 A4.5,4.5 0 0 0 12.5,10 C12.5,7 8,2 8,2 Z";

    public const string Maximize = "M3.5,3.5 H12.5 V12.5 H3.5 Z";

    public const string Restore = "M5.5,5.5 H12.5 V12.5 H5.5 Z M3.5,10 V3.5 H10";
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/PlaceholderViewModel.cs`

Sadržaj prozora dok prava aplikacija ne postoji.

```csharp
namespace CloudAndBear.Ui.ViewModels;

/// <summary>Sadržaj prozora dok prava aplikacija ne postoji.</summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string title) => Title = title;

    public string Title { get; }
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/StyleGuideViewModel.cs`

```csharp
namespace CloudAndBear.Ui.ViewModels;

public sealed class StyleGuideViewModel : ViewModelBase
{
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/AppEntryViewModel.cs`

Stavka u pokretaču (koristi se u koraku 3).

```csharp
using System.Windows.Input;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Stavka u pokretaču: aplikacija i komanda koja je otvara.</summary>
public sealed class AppEntryViewModel : ViewModelBase
{
    public AppEntryViewModel(AppInfo app, Action<AppInfo> open)
    {
        App = app;
        OpenCommand = new RelayCommand(() => open(app));
    }

    public AppInfo App { get; }
    public string Title => App.Title;
    public string IconData => App.IconData;
    public ICommand OpenCommand { get; }
}
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/WindowViewModel.cs`

Jedan prozor: položaj, veličina, stanje i pravila (pomeranje ostaje unutar ekrana, najmanja veličina, promena veličine sa ivice).

```csharp
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
```

### Nov fajl: `src/CloudAndBear.Ui/ViewModels/DesktopViewModel.cs`

Ceo desktop: lista prozora, fokus i redosled, minimizuj/zatvori, pokretač i sat.

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Ceo desktop: prozori, pokretač i sat na traci.</summary>
public sealed class DesktopViewModel : ViewModelBase
{
    private readonly List<AppEntryViewModel> _entries;
    private int _zCounter;
    private double _workWidth = 1280;
    private double _workHeight = 720;
    private bool _isLauncherOpen;
    private string _launcherQuery = "";
    private string _clockText = "";

    public DesktopViewModel()
    {
        AppInfo[] apps =
        [
            new("Terminal", Icons.Terminal, () => new PlaceholderViewModel("Terminal"), AllowMultiple: true),
            new("Mreža", Icons.Network, () => new PlaceholderViewModel("Mreža")),
            new("Kontejneri", Icons.Containers, () => new PlaceholderViewModel("Kontejneri")),
            new("Kubernetes", Icons.Kubernetes, () => new PlaceholderViewModel("Kubernetes")),
            new("Sistem", Icons.System, () => new PlaceholderViewModel("Sistem")),
            new("Stil", Icons.Style, () => new StyleGuideViewModel(), Width: 760, Height: 520),
        ];

        _entries = apps.Select(a => new AppEntryViewModel(a, Open)).ToList();

        ToggleLauncherCommand = new RelayCommand(() => IsLauncherOpen = !IsLauncherOpen);
        CloseLauncherCommand = new RelayCommand(() => IsLauncherOpen = false);

        UpdateClock();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateClock();
        timer.Start();
    }

    public ObservableCollection<WindowViewModel> Windows { get; } = new();

    public ICommand ToggleLauncherCommand { get; }
    public ICommand CloseLauncherCommand { get; }

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
            if (value) LauncherQuery = "";
        }
    }

    public string LauncherQuery
    {
        get => _launcherQuery;
        set
        {
            if (Set(ref _launcherQuery, value)) Raise(nameof(FilteredApps));
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

    public void OpenFirstMatch()
    {
        var first = FilteredApps.FirstOrDefault();
        if (first is not null) Open(first.App);
    }

    public void Activate(WindowViewModel window)
    {
        IsLauncherOpen = false;
        if (window.IsActive && !window.IsMinimized) return;

        foreach (var w in Windows) w.IsActive = w == window;
        window.IsMinimized = false;
        window.ZIndex = ++_zCounter;
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
        ActivateTopmost();
    }

    /// <summary>Klik na dugme prozora na traci: aktivan se minimizuje, ostali dolaze u prvi plan.</summary>
    public void ToggleFromPanel(WindowViewModel window)
    {
        if (window.IsActive && !window.IsMinimized) Minimize(window);
        else Activate(window);
    }

    private void ActivateTopmost()
    {
        var top = Windows.Where(w => !w.IsMinimized).OrderByDescending(w => w.ZIndex).FirstOrDefault();
        if (top is not null) Activate(top);
    }

    private void UpdateClock() => ClockText = DateTime.Now.ToString("HH:mm");
}
```

Stilovi: ikone i ravna dugmad (bez Fluent okvira), pa sam prozor.

### Nov fajl: `src/CloudAndBear.Ui/Theme/ControlStyles.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Ikone: linije na mreži 16x16, boja teksta -->
  <Style Selector="Path.icon">
    <Setter Property="Width" Value="16" />
    <Setter Property="Height" Value="16" />
    <Setter Property="Stretch" Value="None" />
    <Setter Property="Stroke" Value="{DynamicResource TextBrush}" />
    <Setter Property="StrokeThickness" Value="1.5" />
    <Setter Property="StrokeLineCap" Value="Round" />
    <Setter Property="StrokeJoin" Value="Round" />
  </Style>

  <!-- Ravno dugme (naslovna traka, traka zadataka, pokretač): bez Fluent okvira -->
  <Style Selector="Button.flat">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusS}" />
  </Style>
  <Style Selector="Button.flat:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BorderBrush}" />
  </Style>
  <Style Selector="Button.flat:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BorderBrush}" />
  </Style>
  <Style Selector="Button.flat.close:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource DangerBrush}" />
  </Style>

</Styles>
```

### Nov fajl: `src/CloudAndBear.Ui/Theme/WindowStyles.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Prozor -->
  <Style Selector="Border.window">
    <Setter Property="Background" Value="{DynamicResource SurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusM}" />
    <Setter Property="ClipToBounds" Value="True" />
  </Style>
  <Style Selector="Border.window.active">
    <Setter Property="BorderBrush" Value="{DynamicResource TextMutedBrush}" />
  </Style>
  <Style Selector="Border.window.maximized">
    <Setter Property="CornerRadius" Value="0" />
    <Setter Property="BorderThickness" Value="0" />
  </Style>

  <!-- Naslovna traka: aktivan prozor je svetliji -->
  <Style Selector="Border.titlebar">
    <Setter Property="Height" Value="36" />
    <Setter Property="Background" Value="{DynamicResource SurfaceRaisedBrush}" />
  </Style>
  <Style Selector="Border.window:not(.active) Border.titlebar">
    <Setter Property="Background" Value="{DynamicResource SurfaceBrush}" />
  </Style>
  <Style Selector="TextBlock.windowtitle">
    <Setter Property="FontWeight" Value="SemiBold" />
  </Style>
  <Style Selector="Border.window:not(.active) TextBlock.windowtitle">
    <Setter Property="Foreground" Value="{DynamicResource TextMutedBrush}" />
  </Style>

  <!-- Hvataljke za promenu veličine: nevidljive, ali hvataju miša -->
  <Style Selector="Border.grip">
    <Setter Property="Background" Value="Transparent" />
  </Style>

</Styles>
```

Pogledi. `WindowFrame` je okvir jednog prozora: naslovna traka sa dugmadima i osam nevidljivih hvataljki za promenu veličine. Samo hvata miša, a svaku odluku donosi `WindowViewModel`.

### Nov fajl: `src/CloudAndBear.Ui/Views/PlaceholderView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             x:Class="CloudAndBear.Ui.Views.PlaceholderView"
             x:DataType="vm:PlaceholderViewModel">

  <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
    <TextBlock Classes="headline" Text="{Binding Title}" HorizontalAlignment="Center" />
    <TextBlock Classes="muted" Text="Uskoro." HorizontalAlignment="Center" />
  </StackPanel>
</UserControl>
```

### Nov fajl: `src/CloudAndBear.Ui/Views/PlaceholderView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace CloudAndBear.Ui.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
}
```

### Nov fajl: `src/CloudAndBear.Ui/Views/WindowFrame.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             x:Class="CloudAndBear.Ui.Views.WindowFrame"
             x:DataType="vm:WindowViewModel"
             IsVisible="{Binding IsOnDesktop}">

  <Grid>

    <!-- okvir prozora -->
    <Border Classes="window" Classes.active="{Binding IsActive}" Classes.maximized="{Binding IsMaximized}">
      <Grid RowDefinitions="Auto,*">

        <!-- naslovna traka: prevlačenje, dvoklik = maksimizuj -->
        <Border x:Name="TitleBar" Classes="titlebar"
                PointerPressed="OnTitlePressed"
                PointerMoved="OnPointerMoved"
                PointerReleased="OnPointerReleased"
                DoubleTapped="OnTitleDoubleTapped">
          <Grid ColumnDefinitions="Auto,*,Auto" Margin="12,0,4,0">
            <Path Classes="icon" Data="{Binding IconData}" VerticalAlignment="Center" />
            <TextBlock Grid.Column="1" Classes="windowtitle" Margin="10,0" VerticalAlignment="Center" Text="{Binding Title}" />
            <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="2" VerticalAlignment="Center">
              <Button Classes="flat" Width="32" Height="28" Command="{Binding MinimizeCommand}">
                <Path Classes="icon" Data="M3,12 L13,12" />
              </Button>
              <Button Classes="flat" Width="32" Height="28" Command="{Binding ToggleMaximizeCommand}">
                <Path Classes="icon" Data="{Binding MaximizeIconData}" />
              </Button>
              <Button Classes="flat close" Width="32" Height="28" Command="{Binding CloseCommand}">
                <Path Classes="icon" Data="M3.5,3.5 L12.5,12.5 M12.5,3.5 L3.5,12.5" />
              </Button>
            </StackPanel>
          </Grid>
        </Border>

        <!-- sadržaj aplikacije -->
        <ContentControl Grid.Row="1" Content="{Binding Content}" />
      </Grid>
    </Border>

    <!-- hvataljke za promenu veličine (ivice i uglovi) -->
    <Grid x:Name="Grips" IsVisible="{Binding !IsMaximized}"
          PointerPressed="OnGripPressed"
          PointerMoved="OnPointerMoved"
          PointerReleased="OnPointerReleased">
      <Border Tag="N" Classes="grip" Height="5" Margin="10,0" VerticalAlignment="Top" Cursor="SizeNorthSouth" />
      <Border Tag="S" Classes="grip" Height="5" Margin="10,0" VerticalAlignment="Bottom" Cursor="SizeNorthSouth" />
      <Border Tag="W" Classes="grip" Width="5" Margin="0,10" HorizontalAlignment="Left" Cursor="SizeWestEast" />
      <Border Tag="E" Classes="grip" Width="5" Margin="0,10" HorizontalAlignment="Right" Cursor="SizeWestEast" />
      <Border Tag="NW" Classes="grip" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Top" Cursor="TopLeftCorner" />
      <Border Tag="NE" Classes="grip" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Top" Cursor="TopRightCorner" />
      <Border Tag="SW" Classes="grip" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Bottom" Cursor="BottomLeftCorner" />
      <Border Tag="SE" Classes="grip" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Bottom" Cursor="BottomRightCorner" />
    </Grid>

  </Grid>
</UserControl>
```

### Nov fajl: `src/CloudAndBear.Ui/Views/WindowFrame.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class WindowFrame : UserControl
{
    private bool _dragging;
    private string _edge = "";      // prazno = pomeranje, inače ivica/ugao ("N", "SE", ...)
    private Point _start;
    private double _startX, _startY, _startWidth, _startHeight;

    public WindowFrame()
    {
        InitializeComponent();

        // svaki klik u prozoru ga dovodi u prvi plan
        AddHandler(PointerPressedEvent, (_, _) => Vm?.ActivateCommand.Execute(null),
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private WindowViewModel? Vm => DataContext as WindowViewModel;

    // položaj miša računamo prema nepomičnom ItemsControl-u, ne prema prozoru koji se pomera
    private Visual? Host => this.FindAncestorOfType<ItemsControl>();

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e) => Begin(e, TitleBar, "");

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { Tag: string edge }) Begin(e, Grips, edge);
    }

    private void Begin(PointerPressedEventArgs e, Control owner, string edge)
    {
        if (Vm is not { IsMaximized: false } vm || Host is not { } host) return;
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        _edge = edge;
        _start = e.GetPosition(host);
        _startX = vm.Left;
        _startY = vm.Top;
        _startWidth = vm.Width;
        _startHeight = vm.Height;

        e.Pointer.Capture(owner);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || Vm is not { } vm || Host is not { } host) return;

        var p = e.GetPosition(host);
        double dx = p.X - _start.X, dy = p.Y - _start.Y;

        if (_edge.Length == 0) vm.MoveTo(_startX + dx, _startY + dy);
        else vm.Resize(_edge, _startX, _startY, _startWidth, _startHeight, dx, dy);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;

        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void OnTitleDoubleTapped(object? sender, TappedEventArgs e)
    {
        // dvoklik na dugme u naslovnoj traci nije dvoklik na traku
        if (e.Source is Visual v && v.FindAncestorOfType<Button>() is not null) return;

        Vm?.ToggleMaximizeCommand.Execute(null);
    }
}
```

### Nov fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml`

Koren desktopa. Selektor `ItemsControl > ContentPresenter` gađa samo okvir svakog prozora (ne i unutrašnje kontrole), a Canvas ih postavlja prema ViewModel-u.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             xmlns:views="using:CloudAndBear.Ui.Views"
             mc:Ignorable="d" d:DesignWidth="1280" d:DesignHeight="800"
             x:Class="CloudAndBear.Ui.Views.DesktopView"
             x:DataType="vm:DesktopViewModel"
             Background="{DynamicResource BackgroundBrush}"
             TextElement.Foreground="{DynamicResource TextBrush}"
             TextElement.FontSize="{DynamicResource FontSizeBody}">

  <!-- koja aplikacija (ViewModel) se crta kojim pogledom (View) -->
  <UserControl.DataTemplates>
    <DataTemplate DataType="vm:StyleGuideViewModel">
      <views:StyleGuideView />
    </DataTemplate>
    <DataTemplate DataType="vm:PlaceholderViewModel">
      <views:PlaceholderView />
    </DataTemplate>
  </UserControl.DataTemplates>

  <Grid>

    <!-- radna površina: pozadina i prozori -->
    <Grid x:Name="Workspace" ClipToBounds="True">
      <TextBlock Text="C&amp;B" FontSize="200" FontWeight="Bold" Opacity="0.04"
                 HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False" />

      <ItemsControl x:Name="WindowHost" ItemsSource="{Binding Windows}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <Canvas />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>

        <!-- položaj, veličina i redosled prozora dolaze iz ViewModel-a -->
        <ItemsControl.Styles>
          <Style Selector="ItemsControl > ContentPresenter" x:DataType="vm:WindowViewModel">
            <Setter Property="Canvas.Left" Value="{Binding Left}" />
            <Setter Property="Canvas.Top" Value="{Binding Top}" />
            <Setter Property="Width" Value="{Binding Width}" />
            <Setter Property="Height" Value="{Binding Height}" />
            <Setter Property="ZIndex" Value="{Binding ZIndex}" />
          </Style>
        </ItemsControl.Styles>

        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:WindowViewModel">
            <views:WindowFrame />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Grid>

  </Grid>
</UserControl>
```

### Nov fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml.cs`

Privremeno otvara dva prozora za probu (briše se u koraku 3).

```csharp
using Avalonia.Controls;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class DesktopView : UserControl
{
    public DesktopView()
    {
        InitializeComponent();

        var desktop = new DesktopViewModel();
        DataContext = desktop;

        // maksimizovan prozor popunjava radnu površinu (sve iznad trake)
        Workspace.SizeChanged += (_, e) => desktop.SetWorkArea(e.NewSize.Width, e.NewSize.Height);

        // privremeno, dok nema pokretača: dva prozora za probu
        foreach (var entry in desktop.FilteredApps.Where(a => a.Title is "Terminal" or "Stil"))
            desktop.Open(entry.App);
    }
}
```

Povezivanje: `App` i prozor za pregled sada prikazuju `DesktopView`.

### Zameni ceo fajl: `src/CloudAndBear.Ui/App.axaml`

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CloudAndBear.Ui.App"
             RequestedThemeVariant="Dark">

  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://CloudAndBear.Ui/Theme/Tokens.axaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>

  <Application.Styles>
    <!-- Fluent kontrole (dugme, polje, prekidač...) prate naše boje; vrednosti iz Tokens.axaml (Accent, Background) -->
    <FluentTheme>
      <FluentTheme.Palettes>
        <ColorPaletteResources x:Key="Dark" Accent="#9ECBFF" RegionColor="#101820" ErrorText="#F87171" />
      </FluentTheme.Palettes>
    </FluentTheme>
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/Styles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/ControlStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/WindowStyles.axaml" />
  </Application.Styles>

</Application>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/App.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CloudAndBear.Ui.Hosting;
using CloudAndBear.Ui.Views;

namespace CloudAndBear.Ui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            // PC (razvoj): običan prozor oko istog pogleda koji ide na uređaj
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new PreviewWindow(PreviewWindow.ParseSize(desktop.Args));
                break;

            // uređaj (DRM): nema prozora, jedan pogled preko celog ekrana
            case ISingleViewApplicationLifetime single:
                single.MainView = new DesktopView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Hosting/PreviewWindow.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="using:CloudAndBear.Ui.Views"
        x:Class="CloudAndBear.Ui.Hosting.PreviewWindow"
        Title="CloudAndBear Desktop (pregled)"
        Width="1280" Height="800"
        Background="{DynamicResource BackgroundBrush}">

  <!-- Prozor služi samo za razvoj na PC-u; na uređaju se crta samo DesktopView -->
  <views:DesktopView />
</Window>
```

Pokreni. Treba da vidiš:

- dva prozora, `Stil` ispred (svetlija naslovna traka i okvir) i `Terminal` iza (zatamnjen);
- prevlačenje naslovne trake pomera prozor, a prozor ne može da pobegne sa ekrana;
- ivice i uglovi menjaju veličinu (ne ispod 320x180), a promena sa gornje ili leve ivice drži suprotnu ivicu na mestu;
- klik bilo gde u pozadinskom prozoru ga dovodi ispred;
- dvoklik na naslovnu traku ili dugme `□` maksimizuje, a ponovo vraća (ikona se menja);
- `_` minimizuje (prozor nestaje, a traka na kojoj se vraća dolazi u koraku 2), `X` zatvara.

## Korak 2: traka dole

Traka (varijanta A): dugme pokretača, dugme za svaki otvoreni prozor (aktivan je svetliji i ima liniju akcenta, minimizovan je zatamnjen, klik ga aktivira ili minimizuje) i sistemska oblast sa satom. Mreža i oznaka rasporeda (`SR`) su za sada samo oblik; prave vrednosti dolaze iz servisa kad se pravi sistemski deo.

### Nov fajl: `src/CloudAndBear.Ui/Theme/PanelStyles.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Traka -->
  <Style Selector="Border.panel">
    <Setter Property="Height" Value="48" />
    <Setter Property="Background" Value="{DynamicResource SurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="0,1,0,0" />
  </Style>
  <Style Selector="Border.separator">
    <Setter Property="Width" Value="1" />
    <Setter Property="Height" Value="24" />
    <Setter Property="Margin" Value="8,0" />
    <Setter Property="Background" Value="{DynamicResource BorderBrush}" />
  </Style>

  <!-- Dugme prozora na traci: aktivan je svetliji i ima liniju akcenta, minimizovan je zatamnjen -->
  <Style Selector="Button.task">
    <Setter Property="Height" Value="36" />
    <Setter Property="MinWidth" Value="120" />
  </Style>
  <Style Selector="Button.task.active">
    <Setter Property="Background" Value="{DynamicResource SurfaceRaisedBrush}" />
  </Style>
  <Style Selector="Button.task.minimized">
    <Setter Property="Opacity" Value="0.6" />
  </Style>
  <Style Selector="Border.taskmark">
    <Setter Property="Height" Value="2" />
    <Setter Property="VerticalAlignment" Value="Bottom" />
    <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
    <Setter Property="IsVisible" Value="False" />
  </Style>
  <Style Selector="Button.task.active Border.taskmark">
    <Setter Property="IsVisible" Value="True" />
  </Style>

</Styles>
```

### Nov fajl: `src/CloudAndBear.Ui/Views/PanelView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             x:Class="CloudAndBear.Ui.Views.PanelView"
             x:DataType="vm:DesktopViewModel">

  <Border Classes="panel">
    <Grid ColumnDefinitions="Auto,Auto,*,Auto" Margin="8,0">

      <!-- pokretač -->
      <Button Classes="flat" Width="40" Height="36" VerticalAlignment="Center"
              Command="{Binding ToggleLauncherCommand}">
        <Path Classes="icon" Data="M2,2 H7 V7 H2 Z M9,2 H14 V7 H9 Z M2,9 H7 V14 H2 Z M9,9 H14 V14 H9 Z" />
      </Button>

      <Border Grid.Column="1" Classes="separator" />

      <!-- otvoreni prozori -->
      <ItemsControl Grid.Column="2" ItemsSource="{Binding Windows}" VerticalAlignment="Center">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" Spacing="4" />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:WindowViewModel">
            <Button Classes="flat task"
                    Classes.active="{Binding IsActive}"
                    Classes.minimized="{Binding IsMinimized}"
                    Command="{Binding PanelCommand}">
              <Grid>
                <StackPanel Orientation="Horizontal" Spacing="8" Margin="12,0" VerticalAlignment="Center">
                  <Path Classes="icon" Data="{Binding IconData}" />
                  <TextBlock Text="{Binding Title}" VerticalAlignment="Center" />
                </StackPanel>
                <Border Classes="taskmark" />
              </Grid>
            </Button>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <!-- sistemska oblast (mreža i raspored su za sada samo oblik, prave vrednosti dolaze iz servisa) -->
      <StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="16" Margin="0,0,8,0" VerticalAlignment="Center">
        <Path Classes="icon" Data="M2,6 A8,8 0 0 1 14,6 M4.2,8.6 A5,5 0 0 1 11.8,8.6 M6.4,11.2 A2,2 0 0 1 9.6,11.2 M8,13.4 L8,13.5" />
        <TextBlock Classes="muted" Text="SR" VerticalAlignment="Center" />
        <TextBlock Text="{Binding ClockText}" FontWeight="SemiBold" VerticalAlignment="Center" />
      </StackPanel>

    </Grid>
  </Border>
</UserControl>
```

### Nov fajl: `src/CloudAndBear.Ui/Views/PanelView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace CloudAndBear.Ui.Views;

public partial class PanelView : UserControl
{
    public PanelView() => InitializeComponent();
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml`

Dodata je traka u donjem redu; radna površina je sve iznad nje.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             xmlns:views="using:CloudAndBear.Ui.Views"
             mc:Ignorable="d" d:DesignWidth="1280" d:DesignHeight="800"
             x:Class="CloudAndBear.Ui.Views.DesktopView"
             x:DataType="vm:DesktopViewModel"
             Background="{DynamicResource BackgroundBrush}"
             TextElement.Foreground="{DynamicResource TextBrush}"
             TextElement.FontSize="{DynamicResource FontSizeBody}">

  <!-- koja aplikacija (ViewModel) se crta kojim pogledom (View) -->
  <UserControl.DataTemplates>
    <DataTemplate DataType="vm:StyleGuideViewModel">
      <views:StyleGuideView />
    </DataTemplate>
    <DataTemplate DataType="vm:PlaceholderViewModel">
      <views:PlaceholderView />
    </DataTemplate>
  </UserControl.DataTemplates>

  <Grid RowDefinitions="*,Auto">

    <!-- radna površina: pozadina i prozori -->
    <Grid x:Name="Workspace" ClipToBounds="True">
      <TextBlock Text="C&amp;B" FontSize="200" FontWeight="Bold" Opacity="0.04"
                 HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False" />

      <ItemsControl x:Name="WindowHost" ItemsSource="{Binding Windows}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <Canvas />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>

        <!-- položaj, veličina i redosled prozora dolaze iz ViewModel-a -->
        <ItemsControl.Styles>
          <Style Selector="ItemsControl > ContentPresenter" x:DataType="vm:WindowViewModel">
            <Setter Property="Canvas.Left" Value="{Binding Left}" />
            <Setter Property="Canvas.Top" Value="{Binding Top}" />
            <Setter Property="Width" Value="{Binding Width}" />
            <Setter Property="Height" Value="{Binding Height}" />
            <Setter Property="ZIndex" Value="{Binding ZIndex}" />
          </Style>
        </ItemsControl.Styles>

        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:WindowViewModel">
            <views:WindowFrame />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Grid>

    <!-- traka -->
    <views:PanelView Grid.Row="1" />

  </Grid>
</UserControl>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/App.axaml`

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CloudAndBear.Ui.App"
             RequestedThemeVariant="Dark">

  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://CloudAndBear.Ui/Theme/Tokens.axaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>

  <Application.Styles>
    <!-- Fluent kontrole (dugme, polje, prekidač...) prate naše boje; vrednosti iz Tokens.axaml (Accent, Background) -->
    <FluentTheme>
      <FluentTheme.Palettes>
        <ColorPaletteResources x:Key="Dark" Accent="#9ECBFF" RegionColor="#101820" ErrorText="#F87171" />
      </FluentTheme.Palettes>
    </FluentTheme>
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/Styles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/ControlStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/WindowStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/PanelStyles.axaml" />
  </Application.Styles>

</Application>
```

Pokreni. Treba da vidiš traku sa dugmadima `Terminal` i `Stil`. Klik na dugme aktivnog prozora ga minimizuje, ponovo klik ga vraća; maksimizovan prozor popunjava prostor iznad trake i ne zalazi ispod nje. Sat prikazuje pravo vreme.

## Korak 3: pokretač

Dugme u traci ili Alt+F1 otvara pokretač: pretraga (kuca se odmah), lista aplikacija, Enter otvara prvi rezultat, Escape ili klik van njega zatvara. Probni prozori iz koraka 1 se brišu: desktop počinje prazan.

### Nov fajl: `src/CloudAndBear.Ui/Theme/LauncherStyles.axaml`

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Pokretač -->
  <Style Selector="Border.launcher">
    <Setter Property="Width" Value="340" />
    <Setter Property="Padding" Value="12" />
    <Setter Property="Background" Value="{DynamicResource SurfaceRaisedBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusL}" />
  </Style>
  <Style Selector="Button.entry">
    <Setter Property="Height" Value="40" />
    <Setter Property="HorizontalAlignment" Value="Stretch" />
    <Setter Property="HorizontalContentAlignment" Value="Left" />
  </Style>

</Styles>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml`

Dodat je pokretač (sloj iznad radne površine, traka ostaje klikabilna).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             xmlns:views="using:CloudAndBear.Ui.Views"
             mc:Ignorable="d" d:DesignWidth="1280" d:DesignHeight="800"
             x:Class="CloudAndBear.Ui.Views.DesktopView"
             x:DataType="vm:DesktopViewModel"
             Background="{DynamicResource BackgroundBrush}"
             TextElement.Foreground="{DynamicResource TextBrush}"
             TextElement.FontSize="{DynamicResource FontSizeBody}">

  <!-- koja aplikacija (ViewModel) se crta kojim pogledom (View) -->
  <UserControl.DataTemplates>
    <DataTemplate DataType="vm:StyleGuideViewModel">
      <views:StyleGuideView />
    </DataTemplate>
    <DataTemplate DataType="vm:PlaceholderViewModel">
      <views:PlaceholderView />
    </DataTemplate>
  </UserControl.DataTemplates>

  <Grid RowDefinitions="*,Auto">

    <!-- radna površina: pozadina i prozori -->
    <Grid x:Name="Workspace" ClipToBounds="True">
      <TextBlock Text="C&amp;B" FontSize="200" FontWeight="Bold" Opacity="0.04"
                 HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False" />

      <ItemsControl x:Name="WindowHost" ItemsSource="{Binding Windows}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <Canvas />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>

        <!-- položaj, veličina i redosled prozora dolaze iz ViewModel-a -->
        <ItemsControl.Styles>
          <Style Selector="ItemsControl > ContentPresenter" x:DataType="vm:WindowViewModel">
            <Setter Property="Canvas.Left" Value="{Binding Left}" />
            <Setter Property="Canvas.Top" Value="{Binding Top}" />
            <Setter Property="Width" Value="{Binding Width}" />
            <Setter Property="Height" Value="{Binding Height}" />
            <Setter Property="ZIndex" Value="{Binding ZIndex}" />
          </Style>
        </ItemsControl.Styles>

        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:WindowViewModel">
            <views:WindowFrame />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Grid>

    <!-- pokretač: iznad radne površine, odmah iznad trake (traka ostaje klikabilna) -->
    <Grid IsVisible="{Binding IsLauncherOpen}">
      <!-- klik van pokretača ga zatvara -->
      <Border Background="Transparent" PointerPressed="OnBackdropPressed" />

      <Border Classes="launcher" HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="8">
        <StackPanel Spacing="8">
          <TextBox x:Name="SearchBox" PlaceholderText="Pretraga" Text="{Binding LauncherQuery}"
                   KeyDown="OnSearchKeyDown" />

          <ItemsControl ItemsSource="{Binding FilteredApps}">
            <ItemsControl.ItemTemplate>
              <DataTemplate x:DataType="vm:AppEntryViewModel">
                <Button Classes="flat entry" Command="{Binding OpenCommand}">
                  <StackPanel Orientation="Horizontal" Spacing="12" Margin="12,0" VerticalAlignment="Center">
                    <Path Classes="icon" Data="{Binding IconData}" />
                    <TextBlock Text="{Binding Title}" VerticalAlignment="Center" />
                  </StackPanel>
                </Button>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </Border>
    </Grid>

    <!-- traka -->
    <views:PanelView Grid.Row="1" />

  </Grid>
</UserControl>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml.cs`

Fokus u pretragu, Enter/Escape, i prečica Alt+F1 koja radi bez obzira gde je fokus (na uređaju nema prozora, pa je koren TopLevel).

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class DesktopView : UserControl
{
    private readonly DesktopViewModel _desktop = new();

    public DesktopView()
    {
        InitializeComponent();
        DataContext = _desktop;

        // maksimizovan prozor popunjava radnu površinu (sve iznad trake)
        Workspace.SizeChanged += (_, e) => _desktop.SetWorkArea(e.NewSize.Width, e.NewSize.Height);

        // kad se pokretač otvori, kursor ide u polje za pretragu
        _desktop.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DesktopViewModel.IsLauncherOpen) && _desktop.IsLauncherOpen)
                Dispatcher.UIThread.Post(() => SearchBox.Focus());
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // prečice rade bez obzira gde je fokus (na uređaju nema prozora, koren je TopLevel)
        TopLevel.GetTopLevel(this)?.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.F1, KeyModifiers.Alt),
            Command = _desktop.ToggleLauncherCommand,
        });
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e) =>
        _desktop.CloseLauncherCommand.Execute(null);

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _desktop.OpenFirstMatch();
                e.Handled = true;
                break;
            case Key.Escape:
                _desktop.CloseLauncherCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/App.axaml`

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CloudAndBear.Ui.App"
             RequestedThemeVariant="Dark">

  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://CloudAndBear.Ui/Theme/Tokens.axaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>

  <Application.Styles>
    <!-- Fluent kontrole (dugme, polje, prekidač...) prate naše boje; vrednosti iz Tokens.axaml (Accent, Background) -->
    <FluentTheme>
      <FluentTheme.Palettes>
        <ColorPaletteResources x:Key="Dark" Accent="#9ECBFF" RegionColor="#101820" ErrorText="#F87171" />
      </FluentTheme.Palettes>
    </FluentTheme>
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/Styles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/ControlStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/WindowStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/PanelStyles.axaml" />
    <StyleInclude Source="avares://CloudAndBear.Ui/Theme/LauncherStyles.axaml" />
  </Application.Styles>

</Application>
```

Pokreni. Treba da vidiš prazan desktop sa slabim `C&B` u pozadini. Dugme ili Alt+F1 otvara pokretač; otvori `Stil` i `Terminal`; drugi klik na `Stil` ne pravi drugi prozor (samo ga aktivira), a `Terminal` može da se otvori više puta.

## Korak 4: tastatura

Sve do sada radi mišem; ovde sve dobija i tastaturu (mora da radi i bez miša, a na uređaju je tastatura glavni ulaz). Ništa se ne briše, samo se prepisuju fajlovi iz koraka 1 i 3 (ceo sadržaj, pa lakše nego tražiti izmene).

| Prečica | Šta radi |
|---|---|
| Alt+F1 | otvara i zatvara pokretač |
| Alt+Tab, Alt+Shift+Tab | sledeći i prethodni prozor (kruži po redu otvaranja, minimizovan se vraća). Isto rade Alt+F6 i Alt+Shift+F6: Windows hvata Alt+Tab, pa se u pregledu na PC-u koristi to |
| Alt+F4 ili Alt+W | zatvara aktivan prozor (u pregledu Windows na Alt+F4 zatvara ceo program, pa postoji Alt+W) |
| Alt+Gore | maksimizuje aktivan prozor |
| Alt+Dole | vraća maksimizovan prozor, a običan minimizuje |
| Alt+Shift+strelice | pomera aktivan prozor za 32 px |
| Alt+Ctrl+strelice | menja veličinu aktivnog prozora (donja desna ivica) za 32 px |
| u pokretaču: Gore, Dole, Enter, Escape | izbor stavke (kruži), otvaranje izabrane, zatvaranje |

Fokus: svaki prozor koji postane aktivan (klik, Alt+Tab, pokretač) dobija tastaturu, a Escape u pokretaču je vraća prozoru. To će Terminal koristiti da prima slova. Prečice su vezane za koren (`TopLevel`), pa rade bez obzira gde je fokus.

### Zameni ceo fajl: `src/CloudAndBear.Ui/ViewModels/WindowViewModel.cs`

Dodato: događaj za fokus, pomeranje i promena veličine za zadati pomak.

```csharp
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
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/ViewModels/AppEntryViewModel.cs`

Dodato: izabrana stavka u pokretaču.

```csharp
using System.Windows.Input;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Stavka u pokretaču: aplikacija i komanda koja je otvara.</summary>
public sealed class AppEntryViewModel : ViewModelBase
{
    private bool _isSelected;

    public AppEntryViewModel(AppInfo app, Action<AppInfo> open)
    {
        App = app;
        OpenCommand = new RelayCommand(() => open(app));
    }

    public AppInfo App { get; }
    public string Title => App.Title;
    public string IconData => App.IconData;
    public ICommand OpenCommand { get; }

    /// <summary>Izabrana strelicama; Enter otvara izabranu.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/ViewModels/DesktopViewModel.cs`

Dodato: izbor u pokretaču, Alt+Tab, zatvori/maksimizuj/minimizuj aktivan, pomeranje i veličina tastaturom, vraćanje fokusa.

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;

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

    public DesktopViewModel()
    {
        AppInfo[] apps =
        [
            new("Terminal", Icons.Terminal, () => new PlaceholderViewModel("Terminal"), AllowMultiple: true),
            new("Mreža", Icons.Network, () => new PlaceholderViewModel("Mreža")),
            new("Kontejneri", Icons.Containers, () => new PlaceholderViewModel("Kontejneri")),
            new("Kubernetes", Icons.Kubernetes, () => new PlaceholderViewModel("Kubernetes")),
            new("Sistem", Icons.System, () => new PlaceholderViewModel("Sistem")),
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
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/WindowFrame.axaml`

Telo prozora može da primi fokus.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             x:Class="CloudAndBear.Ui.Views.WindowFrame"
             x:DataType="vm:WindowViewModel"
             IsVisible="{Binding IsOnDesktop}">

  <Grid>

    <!-- okvir prozora -->
    <Border Classes="window" Classes.active="{Binding IsActive}" Classes.maximized="{Binding IsMaximized}">
      <Grid RowDefinitions="Auto,*">

        <!-- naslovna traka: prevlačenje, dvoklik = maksimizuj -->
        <Border x:Name="TitleBar" Classes="titlebar"
                PointerPressed="OnTitlePressed"
                PointerMoved="OnPointerMoved"
                PointerReleased="OnPointerReleased"
                DoubleTapped="OnTitleDoubleTapped">
          <Grid ColumnDefinitions="Auto,*,Auto" Margin="12,0,4,0">
            <Path Classes="icon" Data="{Binding IconData}" VerticalAlignment="Center" />
            <TextBlock Grid.Column="1" Classes="windowtitle" Margin="10,0" VerticalAlignment="Center" Text="{Binding Title}" />
            <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="2" VerticalAlignment="Center">
              <Button Classes="flat" Width="32" Height="28" Command="{Binding MinimizeCommand}">
                <Path Classes="icon" Data="M3,12 L13,12" />
              </Button>
              <Button Classes="flat" Width="32" Height="28" Command="{Binding ToggleMaximizeCommand}">
                <Path Classes="icon" Data="{Binding MaximizeIconData}" />
              </Button>
              <Button Classes="flat close" Width="32" Height="28" Command="{Binding CloseCommand}">
                <Path Classes="icon" Data="M3.5,3.5 L12.5,12.5 M12.5,3.5 L3.5,12.5" />
              </Button>
            </StackPanel>
          </Grid>
        </Border>

        <!-- sadržaj aplikacije -->
        <ContentControl x:Name="Body" Grid.Row="1" Content="{Binding Content}"
                        Focusable="True" FocusAdorner="{x:Null}" />
      </Grid>
    </Border>

    <!-- hvataljke za promenu veličine (ivice i uglovi) -->
    <Grid x:Name="Grips" IsVisible="{Binding !IsMaximized}"
          PointerPressed="OnGripPressed"
          PointerMoved="OnPointerMoved"
          PointerReleased="OnPointerReleased">
      <Border Tag="N" Classes="grip" Height="5" Margin="10,0" VerticalAlignment="Top" Cursor="SizeNorthSouth" />
      <Border Tag="S" Classes="grip" Height="5" Margin="10,0" VerticalAlignment="Bottom" Cursor="SizeNorthSouth" />
      <Border Tag="W" Classes="grip" Width="5" Margin="0,10" HorizontalAlignment="Left" Cursor="SizeWestEast" />
      <Border Tag="E" Classes="grip" Width="5" Margin="0,10" HorizontalAlignment="Right" Cursor="SizeWestEast" />
      <Border Tag="NW" Classes="grip" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Top" Cursor="TopLeftCorner" />
      <Border Tag="NE" Classes="grip" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Top" Cursor="TopRightCorner" />
      <Border Tag="SW" Classes="grip" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Bottom" Cursor="BottomLeftCorner" />
      <Border Tag="SE" Classes="grip" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Bottom" Cursor="BottomRightCorner" />
    </Grid>

  </Grid>
</UserControl>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/WindowFrame.axaml.cs`

Prebacuje tastaturu u sadržaj kad ViewModel zatraži fokus (i čim se novi prozor nacrta).

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class WindowFrame : UserControl
{
    private bool _dragging;
    private string _edge = "";      // prazno = pomeranje, inače ivica/ugao ("N", "SE", ...)
    private Point _start;
    private double _startX, _startY, _startWidth, _startHeight;

    public WindowFrame()
    {
        InitializeComponent();

        // svaki klik u prozoru ga dovodi u prvi plan
        AddHandler(PointerPressedEvent, (_, _) => Vm?.ActivateCommand.Execute(null),
            RoutingStrategies.Tunnel, handledEventsToo: true);

        // novi prozor je aktivan pre nego što se nacrta, pa tastaturu dobija kad je spreman
        Loaded += (_, _) =>
        {
            if (Vm is { IsActive: true }) FocusBody();
        };
    }

    private WindowViewModel? Vm => DataContext as WindowViewModel;

    private WindowViewModel? _subscribed;

    // kad ViewModel zatraži fokus (Alt+Tab, pokretač...), tastatura ide u sadržaj prozora
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribed is not null) _subscribed.FocusRequested -= FocusBody;
        _subscribed = Vm;
        if (_subscribed is not null) _subscribed.FocusRequested += FocusBody;
    }

    private void FocusBody() => Body.Focus();

    // položaj miša računamo prema nepomičnom ItemsControl-u, ne prema prozoru koji se pomera
    private Visual? Host => this.FindAncestorOfType<ItemsControl>();

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e) => Begin(e, TitleBar, "");

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { Tag: string edge }) Begin(e, Grips, edge);
    }

    private void Begin(PointerPressedEventArgs e, Control owner, string edge)
    {
        if (Vm is not { IsMaximized: false } vm || Host is not { } host) return;
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        _edge = edge;
        _start = e.GetPosition(host);
        _startX = vm.Left;
        _startY = vm.Top;
        _startWidth = vm.Width;
        _startHeight = vm.Height;

        e.Pointer.Capture(owner);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || Vm is not { } vm || Host is not { } host) return;

        var p = e.GetPosition(host);
        double dx = p.X - _start.X, dy = p.Y - _start.Y;

        if (_edge.Length == 0) vm.MoveTo(_startX + dx, _startY + dy);
        else vm.Resize(_edge, _startX, _startY, _startWidth, _startHeight, dx, dy);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;

        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void OnTitleDoubleTapped(object? sender, TappedEventArgs e)
    {
        // dvoklik na dugme u naslovnoj traci nije dvoklik na traku
        if (e.Source is Visual v && v.FindAncestorOfType<Button>() is not null) return;

        Vm?.ToggleMaximizeCommand.Execute(null);
    }
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml`

Izabrana stavka u pokretaču se označava.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:CloudAndBear.Ui.ViewModels"
             xmlns:views="using:CloudAndBear.Ui.Views"
             mc:Ignorable="d" d:DesignWidth="1280" d:DesignHeight="800"
             x:Class="CloudAndBear.Ui.Views.DesktopView"
             x:DataType="vm:DesktopViewModel"
             Background="{DynamicResource BackgroundBrush}"
             TextElement.Foreground="{DynamicResource TextBrush}"
             TextElement.FontSize="{DynamicResource FontSizeBody}">

  <!-- koja aplikacija (ViewModel) se crta kojim pogledom (View) -->
  <UserControl.DataTemplates>
    <DataTemplate DataType="vm:StyleGuideViewModel">
      <views:StyleGuideView />
    </DataTemplate>
    <DataTemplate DataType="vm:PlaceholderViewModel">
      <views:PlaceholderView />
    </DataTemplate>
  </UserControl.DataTemplates>

  <Grid RowDefinitions="*,Auto">

    <!-- radna površina: pozadina i prozori -->
    <Grid x:Name="Workspace" ClipToBounds="True">
      <TextBlock Text="C&amp;B" FontSize="200" FontWeight="Bold" Opacity="0.04"
                 HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False" />

      <ItemsControl x:Name="WindowHost" ItemsSource="{Binding Windows}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <Canvas />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>

        <!-- položaj, veličina i redosled prozora dolaze iz ViewModel-a -->
        <ItemsControl.Styles>
          <Style Selector="ItemsControl > ContentPresenter" x:DataType="vm:WindowViewModel">
            <Setter Property="Canvas.Left" Value="{Binding Left}" />
            <Setter Property="Canvas.Top" Value="{Binding Top}" />
            <Setter Property="Width" Value="{Binding Width}" />
            <Setter Property="Height" Value="{Binding Height}" />
            <Setter Property="ZIndex" Value="{Binding ZIndex}" />
          </Style>
        </ItemsControl.Styles>

        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:WindowViewModel">
            <views:WindowFrame />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Grid>

    <!-- pokretač: iznad radne površine, odmah iznad trake (traka ostaje klikabilna) -->
    <Grid IsVisible="{Binding IsLauncherOpen}">
      <!-- klik van pokretača ga zatvara -->
      <Border Background="Transparent" PointerPressed="OnBackdropPressed" />

      <Border Classes="launcher" HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="8">
        <StackPanel Spacing="8">
          <TextBox x:Name="SearchBox" PlaceholderText="Pretraga" Text="{Binding LauncherQuery}"
                   KeyDown="OnSearchKeyDown" />

          <ItemsControl ItemsSource="{Binding FilteredApps}">
            <ItemsControl.ItemTemplate>
              <DataTemplate x:DataType="vm:AppEntryViewModel">
                <Button Classes="flat entry" Classes.selected="{Binding IsSelected}" Command="{Binding OpenCommand}">
                  <StackPanel Orientation="Horizontal" Spacing="12" Margin="12,0" VerticalAlignment="Center">
                    <Path Classes="icon" Data="{Binding IconData}" />
                    <TextBlock Text="{Binding Title}" VerticalAlignment="Center" />
                  </StackPanel>
                </Button>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </Border>
    </Grid>

    <!-- traka -->
    <views:PanelView Grid.Row="1" />

  </Grid>
</UserControl>
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Views/DesktopView.axaml.cs`

Sve prečice na jednom mestu i strelice u pretrazi.

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class DesktopView : UserControl
{
    private readonly DesktopViewModel _desktop = new();

    public DesktopView()
    {
        InitializeComponent();
        DataContext = _desktop;

        // maksimizovan prozor popunjava radnu površinu (sve iznad trake)
        Workspace.SizeChanged += (_, e) => _desktop.SetWorkArea(e.NewSize.Width, e.NewSize.Height);

        // kad se pokretač otvori, kursor ide u polje za pretragu
        _desktop.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DesktopViewModel.IsLauncherOpen) && _desktop.IsLauncherOpen)
                Dispatcher.UIThread.Post(() => SearchBox.Focus());
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // prečice rade bez obzira gde je fokus (na uređaju nema prozora, koren je TopLevel)
        if (TopLevel.GetTopLevel(this) is not { } top) return;

        void Shortcut(Key key, KeyModifiers modifiers, Action action) =>
            top.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(key, modifiers),
                Command = new RelayCommand(action),
            });

        const KeyModifiers Alt = KeyModifiers.Alt;

        Shortcut(Key.F1, Alt, () => _desktop.ToggleLauncherCommand.Execute(null));

        // prozori: Alt+Tab menja prozor; Alt+F6 isto (Windows hvata Alt+Tab pa se u pregledu koristi ovo)
        Shortcut(Key.Tab, Alt, () => _desktop.ActivateNext(1));
        Shortcut(Key.Tab, Alt | KeyModifiers.Shift, () => _desktop.ActivateNext(-1));
        Shortcut(Key.F6, Alt, () => _desktop.ActivateNext(1));
        Shortcut(Key.F6, Alt | KeyModifiers.Shift, () => _desktop.ActivateNext(-1));

        // zatvaranje: Alt+F4; Alt+W isto (u pregledu Windows zatvara ceo program na Alt+F4)
        Shortcut(Key.F4, Alt, _desktop.CloseActive);
        Shortcut(Key.W, Alt, _desktop.CloseActive);

        // veličina i položaj aktivnog prozora
        Shortcut(Key.Up, Alt, _desktop.MaximizeActive);
        Shortcut(Key.Down, Alt, _desktop.RestoreOrMinimizeActive);

        const KeyModifiers Move = KeyModifiers.Alt | KeyModifiers.Shift;
        Shortcut(Key.Left, Move, () => _desktop.MoveActive(-1, 0));
        Shortcut(Key.Right, Move, () => _desktop.MoveActive(1, 0));
        Shortcut(Key.Up, Move, () => _desktop.MoveActive(0, -1));
        Shortcut(Key.Down, Move, () => _desktop.MoveActive(0, 1));

        const KeyModifiers Size = KeyModifiers.Alt | KeyModifiers.Control;
        Shortcut(Key.Left, Size, () => _desktop.ResizeActive(-1, 0));
        Shortcut(Key.Right, Size, () => _desktop.ResizeActive(1, 0));
        Shortcut(Key.Up, Size, () => _desktop.ResizeActive(0, -1));
        Shortcut(Key.Down, Size, () => _desktop.ResizeActive(0, 1));
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e) =>
        _desktop.CloseLauncher();

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _desktop.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                _desktop.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                _desktop.OpenSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                _desktop.CloseLauncher();
                e.Handled = true;
                break;
        }
    }
}
```

### Zameni ceo fajl: `src/CloudAndBear.Ui/Theme/LauncherStyles.axaml`

Stil izabrane stavke.

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Pokretač -->
  <Style Selector="Border.launcher">
    <Setter Property="Width" Value="340" />
    <Setter Property="Padding" Value="12" />
    <Setter Property="Background" Value="{DynamicResource SurfaceRaisedBrush}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusL}" />
  </Style>
  <Style Selector="Button.entry">
    <Setter Property="Height" Value="40" />
    <Setter Property="HorizontalAlignment" Value="Stretch" />
    <Setter Property="HorizontalContentAlignment" Value="Left" />
  </Style>
  <Style Selector="Button.entry.selected">
    <Setter Property="Background" Value="{DynamicResource BorderBrush}" />
  </Style>

</Styles>
```

Pokreni. Treba da radi bez miša: otvori pokretač sa Alt+F1, strelicama izaberi aplikaciju, Enter; otvori još jednu pa menjaj prozore sa Alt+Tab (u pregledu Alt+F6); Alt+Shift+strelice ih pomeraju, Alt+Ctrl+strelice menjaju veličinu, Alt+Gore i Alt+Dole maksimizuju i vraćaju, Alt+W zatvara. Aktivan prozor je uvek onaj sa svetlijom naslovnom trakom i linijom akcenta na traci.

## Šta još ne postoji

- Aplikacije su prazne (`Uskoro.`): Sistem i Mreža prvi, iz logike starog `gui/`; zatim Terminal.
- Isključi / Restartuj / Izađi u pokretaču, obaveštenja, zaključani ekran.
- Senke, animacije i pravi izgled (boje su privremene).
- `CloudAndBear.Device`: DRM domaćin za Debian (ulaz, kursor miša, servisi). Na uređaju se tek tada vidi koliko je sve ovo glatko.

Ako nešto ne radi, pošalji mi tačnu poruku greške i naziv fajla.
