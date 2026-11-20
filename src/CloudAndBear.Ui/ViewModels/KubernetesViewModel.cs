using System.Collections.ObjectModel;
using Avalonia.Threading;
using CloudAndBear.Ui.Services;

namespace CloudAndBear.Ui.ViewModels;

public sealed record PodRow(PodInfo Pod) : StatusRow
{
    public override StatusKind Kind => StatusKinds.ForKube(Pod.Status);
}

public sealed record NodeRow(NodeInfo Node) : StatusRow
{
    public override StatusKind Kind => StatusKinds.ForKube(Node.Status.Split(',')[0]);
}

/// <summary>Kubernetes (samo čitanje): konteksti iz kubeconfig-a, čvorovi i podovi izabranog konteksta.</summary>
public sealed class KubernetesViewModel : ViewModelBase, IDisposable
{
    private readonly IKubeService _kube;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private IReadOnlyList<string> _contexts = [];
    private string? _selectedContext;
    private string _summary = "", _problem = "";
    private bool _loading, _restoring;

    public KubernetesViewModel(IKubeService kube)
    {
        _kube = kube;

        Refresh = new AsyncRelayCommand(() => LoadAsync(reloadContexts: true));
        _timer.Tick += async (_, _) => await LoadAsync(reloadContexts: false);
        _timer.Start();
        Ready = LoadAsync(reloadContexts: true);
    }

    /// <summary>Završeno prvo učitavanje (konteksti, čvorovi, podovi); služi testu.</summary>
    public Task Ready { get; }

    public AsyncRelayCommand Refresh { get; }

    public ObservableCollection<NodeRow> Nodes { get; } = [];
    public ObservableCollection<PodRow> Pods { get; } = [];

    public IReadOnlyList<string> Contexts { get => _contexts; private set => Set(ref _contexts, value); }

    /// <summary>Izabrani kontekst; promena odmah učitava čvorove i podove tog klastera.</summary>
    public string? SelectedContext
    {
        get => _selectedContext;
        set
        {
            if (value is null && _restoring) return;
            if (!Set(ref _selectedContext, value)) return;

            Raise(nameof(HasContext));
            Nodes.Clear();
            Pods.Clear();
            Summary = "";
            if (value is not null) _ = LoadAsync(reloadContexts: false);
        }
    }

    public bool HasContext => SelectedContext is not null;
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string KubeconfigText => _kube.KubeconfigPath is { } p ? $"kubeconfig: {p}" : "kubeconfig: nije nađen (~/.kube/config)";

    public string Problem
    {
        get => _problem;
        private set
        {
            if (Set(ref _problem, value)) Raise(nameof(HasProblem));
        }
    }

    public bool HasProblem => Problem.Length > 0;

    public void Dispose() => _timer.Stop();

    public async Task LoadAsync(bool reloadContexts)
    {
        if (_loading) return;

        _loading = true;
        string? loadedFor = null;
        try
        {
            if (reloadContexts && !await LoadContextsAsync()) return;
            if (SelectedContext is not { } context) return;

            loadedFor = context;
            var nodesTask = _kube.NodesAsync(context);
            var podsTask = _kube.PodsAsync(context);
            var (nodes, nodesError) = await nodesTask;
            var (pods, podsError) = await podsTask;

            if (context != SelectedContext) return;   // u međuvremenu je izabran drugi kontekst
            if (nodesError is not null || podsError is not null)
            {
                Problem = nodesError ?? podsError!;
                Nodes.Clear();
                Pods.Clear();
                Summary = "";
                return;
            }

            Problem = "";
            Replace(Nodes, nodes.Select(n => new NodeRow(n)).ToList());
            Replace(Pods, pods.Select(p => new PodRow(p)).ToList());
            var running = pods.Count(p => p.Status == "Running");
            Summary = $"Čvorova: {nodes.Count}  ·  podova: {pods.Count} (radi: {running})";
        }
        finally
        {
            _loading = false;

            // kontekst je promenjen dok se učitavao prethodni: učitaj onaj koji je sada izabran
            if (loadedFor is not null && SelectedContext is not null && SelectedContext != loadedFor)
                _ = LoadAsync(reloadContexts: false);
        }
    }

    private async Task<bool> LoadContextsAsync()
    {
        var (contexts, current, error) = await _kube.ContextsAsync();
        if (error is not null)
        {
            Problem = error;
            Contexts = [];
            return false;
        }

        if (contexts.Count == 0)
        {
            Problem = "U kubeconfig-u nema konteksta.";
            Contexts = [];
            return false;
        }

        var keep = SelectedContext is { } s && contexts.Contains(s) ? s : current ?? contexts[0];
        _restoring = true;
        Contexts = contexts;
        _restoring = false;
        if (keep != SelectedContext) SelectedContext = keep;   // pokreće učitavanje; ovde ga ne dupliramo
        else Raise(nameof(SelectedContext));                   // lista je zamenjena, a izbor ostaje isti
        return true;
    }

    // menja samo ono što se razlikuje, da lista ne "skače" pri svakom osvežavanju
    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (i >= target.Count) target.Add(source[i]);
            else if (!EqualityComparer<T>.Default.Equals(target[i], source[i])) target[i] = source[i];
        }

        while (target.Count > source.Count) target.RemoveAt(target.Count - 1);
    }
}
