using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CloudAndBear.Ui.Services;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

public partial class DesktopView : UserControl
{
    private readonly DesktopViewModel _desktop;

    // na PC-u izmišljeni podaci, na Linuxu pravi (vidi DesktopServices.Create)
    public DesktopView() : this(DesktopServices.Create())
    {
    }

    public DesktopView(DesktopServices services)
    {
        _desktop = new DesktopViewModel(services);
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

        // spisak prečica je u DesktopShortcuts (terminal ga koristi da ih ne proguta)
        foreach (var shortcut in DesktopShortcuts.All)
        {
            var run = shortcut.Run;
            top.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(shortcut.Key, shortcut.Modifiers),
                Command = new RelayCommand(() => run(_desktop)),
            });
        }

        // na uređaju (nije prozor) crtamo sopstveni kursor, jer ga Avalonia ne crta
        if (top is not Window)
        {
            top.AddHandler(PointerMovedEvent, (_, moved) =>
            {
                var p = moved.GetPosition(this);
                Canvas.SetLeft(MouseCursor, p.X);
                Canvas.SetTop(MouseCursor, p.Y);
                MouseCursor.IsVisible = true;
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
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
