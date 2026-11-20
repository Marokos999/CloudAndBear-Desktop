namespace CloudAndBear.Ui.ViewModels;

/// <summary>Sadržaj prozora dok prava aplikacija ne postoji.</summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string title) => Title = title;

    public string Title { get; }
}
