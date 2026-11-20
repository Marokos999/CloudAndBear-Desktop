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

    private void FocusBody()
    {
        Body.Focus();

        // sadržaj koji prima tastaturu (terminal) preuzima fokus od okvira
        if (Body.GetVisualDescendants().OfType<IWindowFocusTarget>().FirstOrDefault() is Control target)
            target.Focus();
    }

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
