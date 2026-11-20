using System.Collections.ObjectModel;
using Avalonia.Threading;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Red u spisku kontejnera; objekat ostaje isti dok kontejner postoji, pa izbor u listi ne nestaje pri osvežavanju.</summary>
public sealed class ContainerRow : ViewModelBase
{
    public ContainerRow(ContainerInfo info) => Info = info;

    public ContainerInfo Info { get; private set; }

    public string Id => Info.Id;
    public bool Running => Info.State == "running";

    public StatusKind Kind => StatusKinds.ForContainer(Info.State);
    public bool IsGood => Kind == StatusKind.Good;
    public bool IsWarn => Kind == StatusKind.Warn;
    public bool IsBad => Kind == StatusKind.Bad;

    public void Update(ContainerInfo next)
    {
        if (next == Info) return;

        Info = next;
        Raise(nameof(Info));
        Raise(nameof(Running));
        Raise(nameof(IsGood));
        Raise(nameof(IsWarn));
        Raise(nameof(IsBad));
    }
}

/// <summary>Kontejneri: Docker spisak, start / stop / restart / brisanje i poslednje linije ispisa izabranog kontejnera.</summary>
public sealed class ContainersViewModel : ViewModelBase, IDisposable
{
    private readonly IContainerService _docker;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private ContainerRow? _selected;
    private string _summary = "", _message = "", _problem = "", _logs = "";
    private bool _busy, _loading, _confirmRemove;

    public ContainersViewModel(IContainerService docker)
    {
        _docker = docker;

        Start = new AsyncRelayCommand(() => ActAsync("Pokrećem", "Pokrenut", _docker.StartAsync), () => !_busy && Selected is { Running: false });
        Stop = new AsyncRelayCommand(() => ActAsync("Zaustavljam", "Zaustavljen", _docker.StopAsync), () => !_busy && Selected is { Running: true });
        Restart = new AsyncRelayCommand(() => ActAsync("Restartujem", "Restartovan", _docker.RestartAsync), () => !_busy && Selected is { Running: true });
        Remove = new RelayCommand(() => ConfirmRemove = true, () => !_busy && Selected is not null);
        ConfirmYes = new AsyncRelayCommand(() => ActAsync("Brišem", "Obrisan", _docker.RemoveAsync));
        ConfirmNo = new RelayCommand(() => ConfirmRemove = false);
        Refresh = new AsyncRelayCommand(RefreshAsync);

        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        Ready = RefreshAsync();
    }

    /// <summary>Završeno prvo učitavanje; služi testu.</summary>
    public Task Ready { get; }

    public ObservableCollection<ContainerRow> Containers { get; } = [];

    public AsyncRelayCommand Start { get; }
    public AsyncRelayCommand Stop { get; }
    public AsyncRelayCommand Restart { get; }
    public RelayCommand Remove { get; }
    public AsyncRelayCommand ConfirmYes { get; }
    public RelayCommand ConfirmNo { get; }
    public AsyncRelayCommand Refresh { get; }

    public ContainerRow? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;

            ConfirmRemove = false;
            Logs = "";
            Raise(nameof(HasSelection));
            Raise(nameof(LogsTitle));
            UpdateCommands();
            _ = LoadLogsAsync();
        }
    }

    public bool HasSelection => Selected is not null;
    public string LogsTitle => Selected is { } r ? $"Ispis: {r.Info.Name}" : "";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string Logs { get => _logs; private set => Set(ref _logs, value); }

    public string Message
    {
        get => _message;
        private set
        {
            if (Set(ref _message, value)) Raise(nameof(HasMessage));
        }
    }

    public bool HasMessage => Message.Length > 0;

    public string Problem
    {
        get => _problem;
        private set
        {
            if (Set(ref _problem, value)) Raise(nameof(HasProblem));
        }
    }

    public bool HasProblem => Problem.Length > 0;

    public bool ConfirmRemove
    {
        get => _confirmRemove;
        private set
        {
            if (!Set(ref _confirmRemove, value)) return;

            Raise(nameof(NotConfirming));
            Raise(nameof(RemoveQuestion));
        }
    }

    public bool NotConfirming => !ConfirmRemove;
    public string RemoveQuestion => Selected is { } r ? $"Obrisati kontejner {r.Info.Name}?" : "";

    public void Dispose() => _timer.Stop();

    private void UpdateCommands()
    {
        Start.RaiseCanExecuteChanged();
        Stop.RaiseCanExecuteChanged();
        Restart.RaiseCanExecuteChanged();
        Remove.RaiseCanExecuteChanged();
    }

    public async Task RefreshAsync()
    {
        if (_loading) return;

        _loading = true;
        try
        {
            var list = await _docker.ListAsync();
            Problem = "";
            Sync(list);
            Summary = $"Pokrenuto {list.Count(c => c.State == "running")} od {list.Count}";
            UpdateCommands();
            await LoadLogsAsync();
        }
        catch (Exception e)
        {
            Containers.Clear();
            Summary = "";
            Problem = IsSocketError(e)
                ? "Docker nije pokrenut ili nije instaliran (nema /var/run/docker.sock)."
                : $"Docker: {e.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    // soket ne postoji ili ne prima veze: negde u lancu izuzetaka je SocketException
    private static bool IsSocketError(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
            if (e is System.Net.Sockets.SocketException) return true;

        return false;
    }

    // usklađuje listu sa Dockerom, a postojeći redovi ostaju isti objekti
    private void Sync(IReadOnlyList<ContainerInfo> list)
    {
        for (var i = Containers.Count - 1; i >= 0; i--)
            if (list.All(c => c.Id != Containers[i].Id)) Containers.RemoveAt(i);

        for (var i = 0; i < list.Count; i++)
        {
            var at = Containers.ToList().FindIndex(r => r.Id == list[i].Id);
            if (at < 0)
            {
                Containers.Insert(i, new ContainerRow(list[i]));
            }
            else
            {
                Containers[at].Update(list[i]);
                if (at != i) Containers.Move(at, i);
            }
        }

        // izabrani kontejner je obrisan
        if (Selected is { } s && !Containers.Contains(s)) Selected = null;
    }

    private async Task LoadLogsAsync()
    {
        if (Selected is not { } row) return;

        try
        {
            var text = await _docker.LogsAsync(row.Id);
            if (Selected == row) Logs = text.Length == 0 ? "(nema ispisa)" : text;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // kontejner je u međuvremenu obrisan: sledeće osvežavanje to ispravlja
        }
    }

    private async Task ActAsync(string doing, string done, Func<string, Task> action)
    {
        if (_busy || Selected is not { } row) return;

        var name = row.Info.Name;
        ConfirmRemove = false;
        _busy = true;
        UpdateCommands();
        Message = $"{doing} {name}…";
        try
        {
            await action(row.Id);
            Message = $"{done}: {name}.";
        }
        catch (Exception e)
        {
            Message = e.Message;
        }
        finally
        {
            _busy = false;
        }

        await RefreshAsync();
        UpdateCommands();
    }
}
