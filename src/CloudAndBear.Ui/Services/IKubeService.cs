namespace CloudAndBear.Ui.Services;

public sealed record PodInfo(string Namespace, string Name, string Ready, string Status, int Restarts, string Age, string Node);

public sealed record NodeInfo(string Name, string Status, string Roles, string Version, string Age);

/// <summary>
/// Kubernetes, samo čitanje. Na uređaju je to kubectl (<see cref="KubectlService"/>), a u pregledu na PC-u izmišljeni
/// klasteri (<see cref="FakeKubeService"/>). Kontekst se zadaje po pozivu, pa se kubeconfig nikad ne menja.
/// </summary>
public interface IKubeService
{
    /// <summary>Kubeconfig koji se koristi, ili null kad nije nađen.</summary>
    string? KubeconfigPath { get; }

    Task<(IReadOnlyList<string> Contexts, string? Current, string? Error)> ContextsAsync();

    Task<(IReadOnlyList<PodInfo> Pods, string? Error)> PodsAsync(string context);

    Task<(IReadOnlyList<NodeInfo> Nodes, string? Error)> NodesAsync(string context);
}
