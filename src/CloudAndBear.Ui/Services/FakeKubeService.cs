namespace CloudAndBear.Ui.Services;

/// <summary>
/// Izmišljeni klasteri za pregled na PC-u: kind-dev (jedan čvor, nekoliko podova, neki ne rade), staging-aks (zdrav)
/// i prod-eks (ne odgovara, da se vidi greška).
/// </summary>
public sealed class FakeKubeService : IKubeService
{
    private static readonly IReadOnlyList<PodInfo> DevPods =
    [
        new("default", "api-7d9f6c5b8d-x2k4p", "1/1", "Running", 0, "3d", "kind-control-plane"),
        new("default", "web-5c7b9d6f4-lm9qz", "1/1", "Running", 0, "3d", "kind-control-plane"),
        new("default", "worker-6b5c8d7f9-qw2rt", "0/1", "CrashLoopBackOff", 12, "1h", "kind-control-plane"),
        new("default", "cache-0", "0/1", "Pending", 0, "4m", "—"),
        new("default", "migrate-29174-bk5m2", "0/1", "Completed", 0, "2d", "kind-control-plane"),
        new("kube-system", "coredns-668d6bf9bc-4hq7x", "1/1", "Running", 0, "9d", "kind-control-plane"),
        new("kube-system", "etcd-kind-control-plane", "1/1", "Running", 0, "9d", "kind-control-plane"),
        new("kube-system", "kube-apiserver-kind-control-plane", "1/1", "Running", 0, "9d", "kind-control-plane"),
        new("monitoring", "prometheus-0", "2/2", "Running", 1, "6d", "kind-control-plane"),
        new("monitoring", "grafana-84c7d6b5f9-7xzpl", "0/1", "ImagePullBackOff", 0, "20m", "kind-control-plane"),
    ];

    private static readonly IReadOnlyList<PodInfo> StagingPods =
    [
        new("default", "api-6f8d9c7b5-2mdkq", "1/1", "Running", 0, "5d", "aks-nodepool1-0"),
        new("default", "api-6f8d9c7b5-9tzvr", "1/1", "Running", 0, "5d", "aks-nodepool1-1"),
        new("kube-system", "coredns-5d78c9869d-lt4bz", "1/1", "Running", 0, "30d", "aks-nodepool1-0"),
    ];

    /// <summary>Koliko traje svaki poziv; u testu nula.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(400);

    public string? KubeconfigPath => "/var/lib/cloudandbear/kubeconfig (izmišljeno)";

    public async Task<(IReadOnlyList<string> Contexts, string? Current, string? Error)> ContextsAsync()
    {
        await Wait();
        return (["kind-dev", "prod-eks", "staging-aks"], "kind-dev", null);
    }

    public async Task<(IReadOnlyList<PodInfo> Pods, string? Error)> PodsAsync(string context)
    {
        await Wait();
        return context switch
        {
            "kind-dev" => (DevPods, null),
            "staging-aks" => (StagingPods, null),
            _ => ([], Unreachable(context)),
        };
    }

    public async Task<(IReadOnlyList<NodeInfo> Nodes, string? Error)> NodesAsync(string context)
    {
        await Wait();
        return context switch
        {
            "kind-dev" => ([new NodeInfo("kind-control-plane", "Ready", "control-plane", "v1.32.2", "9d")], null),
            "staging-aks" =>
            ([
                new NodeInfo("aks-nodepool1-0", "Ready", "agent", "v1.31.4", "30d"),
                new NodeInfo("aks-nodepool1-1", "Ready,SchedulingDisabled", "agent", "v1.31.4", "30d"),
                new NodeInfo("aks-nodepool1-2", "NotReady", "agent", "v1.31.4", "2d"),
            ], null),
            _ => ([], Unreachable(context)),
        };
    }

    private static string Unreachable(string context) =>
        $"Unable to connect to the server: dial tcp 10.20.30.40:443: i/o timeout (kontekst {context})";

    private Task Wait() => Delay > TimeSpan.Zero ? Task.Delay(Delay) : Task.CompletedTask;
}
