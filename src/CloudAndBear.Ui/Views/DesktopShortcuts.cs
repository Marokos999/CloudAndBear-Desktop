using Avalonia.Input;
using CloudAndBear.Ui.ViewModels;

namespace CloudAndBear.Ui.Views;

/// <summary>
/// Prečice desktopa na jednom mestu. <see cref="DesktopView"/> ih vezuje za koren, a terminal ih preskače
/// (<see cref="IsReserved"/>) da ne bi progutao Alt+Tab i slične.
/// </summary>
public static class DesktopShortcuts
{
    private const KeyModifiers Alt = KeyModifiers.Alt;
    private const KeyModifiers Move = KeyModifiers.Alt | KeyModifiers.Shift;
    private const KeyModifiers Size = KeyModifiers.Alt | KeyModifiers.Control;

    public sealed record Shortcut(Key Key, KeyModifiers Modifiers, Action<DesktopViewModel> Run);

    public static IReadOnlyList<Shortcut> All { get; } =
    [
        new(Key.F1, Alt, d => d.ToggleLauncherCommand.Execute(null)),

        // izlaz u konzolu kad ništa drugo ne radi (na uređaju zatvara desktop, servis vraća prijavu na tty1)
        new(Key.Q, KeyModifiers.Control | Alt, d => d.Services.System.ExitToConsole()),

        // prozori: Alt+Tab menja prozor; Alt+F6 isto (Windows hvata Alt+Tab pa se u pregledu koristi ovo)
        new(Key.Tab, Alt, d => d.ActivateNext(1)),
        new(Key.Tab, Alt | KeyModifiers.Shift, d => d.ActivateNext(-1)),
        new(Key.F6, Alt, d => d.ActivateNext(1)),
        new(Key.F6, Alt | KeyModifiers.Shift, d => d.ActivateNext(-1)),

        // zatvaranje: Alt+F4; Alt+W isto (u pregledu Windows zatvara ceo program na Alt+F4)
        new(Key.F4, Alt, d => d.CloseActive()),
        new(Key.W, Alt, d => d.CloseActive()),

        // veličina i položaj aktivnog prozora
        new(Key.Up, Alt, d => d.MaximizeActive()),
        new(Key.Down, Alt, d => d.RestoreOrMinimizeActive()),

        new(Key.Left, Move, d => d.MoveActive(-1, 0)),
        new(Key.Right, Move, d => d.MoveActive(1, 0)),
        new(Key.Up, Move, d => d.MoveActive(0, -1)),
        new(Key.Down, Move, d => d.MoveActive(0, 1)),

        new(Key.Left, Size, d => d.ResizeActive(-1, 0)),
        new(Key.Right, Size, d => d.ResizeActive(1, 0)),
        new(Key.Up, Size, d => d.ResizeActive(0, -1)),
        new(Key.Down, Size, d => d.ResizeActive(0, 1)),
    ];

    public static bool IsReserved(Key key, KeyModifiers modifiers)
    {
        // Num Lock i slično ne smeju da menjaju odgovor
        modifiers &= KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta;
        return All.Any(s => s.Key == key && s.Modifiers == modifiers);
    }
}
