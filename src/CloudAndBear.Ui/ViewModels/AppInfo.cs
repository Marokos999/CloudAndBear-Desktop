namespace CloudAndBear.Ui.ViewModels;

/// <summary>Opis aplikacije: naslov, ikona, kako se pravi sadržaj prozora i kolika je početna veličina.</summary>
public sealed record AppInfo(
    string Title,
    string IconData,
    Func<object> CreateContent,
    double Width = 640,
    double Height = 420,
    bool AllowMultiple = false);
