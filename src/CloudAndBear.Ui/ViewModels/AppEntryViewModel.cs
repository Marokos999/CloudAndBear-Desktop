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
