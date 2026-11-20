using System.Globalization;
using System.Text.Json;

namespace CloudAndBear.Ui.Services;

/// <summary>Kubernetes preko kubectl-a, samo čitanje. Kontekst se zadaje po pozivu (--context), pa se kubeconfig nikad ne menja.</summary>
public sealed class KubectlService : IKubeService
{
    private readonly string _kubectl;

    public KubectlService(string kubectl = "kubectl", string? kubeconfigPath = null)
    {
        _kubectl = kubectl;
        KubeconfigPath = kubeconfigPath ?? FindKubeconfig();
    }

    public string? KubeconfigPath { get; }

    // Traži se: KUBECONFIG, pa kubeconfig u stanju aplikacije (/var/lib/cloudandbear/kubeconfig, tamo ga stavlja onaj ko
    // podešava desktop, jer nalog bez prijave ne vidi tuđe kućne foldere), pa ~/.kube/config, pa /home/*/.kube/config
    private static string? FindKubeconfig()
    {
        var fromEnv = Environment.GetEnvironmentVariable("KUBECONFIG")?.Split(Path.PathSeparator).FirstOrDefault(File.Exists);
        if (fromEnv is not null) return fromEnv;

        var state = Environment.GetEnvironmentVariable("STATE_DIRECTORY")?.Split(':')[0] ?? "/var/lib/cloudandbear";
        var candidates = new List<string>
        {
            Path.Combine(state, "kubeconfig"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config"),
        };
        if (Directory.Exists("/home"))
            candidates.AddRange(Directory.GetDirectories("/home").Order().Select(h => Path.Combine(h, ".kube", "config")));
        return candidates.FirstOrDefault(File.Exists);
    }

    private Task<CommandResult> Kubectl(IEnumerable<string> args)
    {
        var env = KubeconfigPath is null ? null : new Dictionary<string, string> { ["KUBECONFIG"] = KubeconfigPath };
        return ProcessRunner.RunAsync(_kubectl, args, 30, env);
    }

    public async Task<(IReadOnlyList<string> Contexts, string? Current, string? Error)> ContextsAsync()
    {
        var all = await Kubectl(["config", "get-contexts", "-o", "name"]);
        if (!all.Ok) return ([], null, ProcessRunner.FirstLine(all));

        var current = await Kubectl(["config", "current-context"]);
        var contexts = all.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (contexts, current.Ok ? current.Output : null, null);
    }

    public async Task<(IReadOnlyList<PodInfo> Pods, string? Error)> PodsAsync(string context)
    {
        var r = await Kubectl(["--context", context, "--request-timeout=10s", "get", "pods", "--all-namespaces", "-o", "json"]);
        if (!r.Ok) return ([], ProcessRunner.FirstLine(r));

        using var doc = JsonDocument.Parse(r.Output);
        var pods = new List<PodInfo>();
        foreach (var item in Items(doc))
        {
            var statuses = Array(item, "status", "containerStatuses");
            var total = statuses.Count > 0 ? statuses.Count : Array(item, "spec", "containers").Count;
            var ready = statuses.Count(s => Get(s, "ready") is { ValueKind: JsonValueKind.True });
            var restarts = statuses.Sum(s => Get(s, "restartCount") is { ValueKind: JsonValueKind.Number } n ? n.GetInt32() : 0);

            // stanje kao u kubectl-u: faza, pa razlog iz kontejnera (CrashLoopBackOff, ImagePullBackOff, Error...), pa Terminating
            var status = Str(item, "status", "reason") ?? Str(item, "status", "phase") ?? "?";
            foreach (var s in statuses)
            {
                var waiting = Str(s, "state", "waiting", "reason");
                var finished = Str(s, "state", "terminated", "reason");
                if (!string.IsNullOrEmpty(waiting)) status = waiting;
                else if (!string.IsNullOrEmpty(finished) && ready < total) status = finished;   // Completed, Error, OOMKilled
            }

            if (Get(item, "metadata", "deletionTimestamp") is not null) status = "Terminating";

            pods.Add(new PodInfo(
                Str(item, "metadata", "namespace") ?? "",
                Str(item, "metadata", "name") ?? "?",
                $"{ready}/{total}",
                status,
                restarts,
                AgeOf(item),
                Str(item, "spec", "nodeName") ?? "—"));
        }

        return (pods.OrderBy(p => p.Namespace, StringComparer.Ordinal).ThenBy(p => p.Name, StringComparer.Ordinal).ToList(), null);
    }

    public async Task<(IReadOnlyList<NodeInfo> Nodes, string? Error)> NodesAsync(string context)
    {
        var r = await Kubectl(["--context", context, "--request-timeout=10s", "get", "nodes", "-o", "json"]);
        if (!r.Ok) return ([], ProcessRunner.FirstLine(r));

        using var doc = JsonDocument.Parse(r.Output);
        var nodes = new List<NodeInfo>();
        foreach (var item in Items(doc))
        {
            var readyCondition = Array(item, "status", "conditions").FirstOrDefault(c => Str(c, "type") == "Ready");
            var status = readyCondition.ValueKind == JsonValueKind.Undefined ? "Unknown" : Str(readyCondition, "status") switch
            {
                "True" => "Ready",
                "False" => "NotReady",
                _ => "Unknown",
            };
            if (Get(item, "spec", "unschedulable") is { ValueKind: JsonValueKind.True }) status += ",SchedulingDisabled";

            var roles = Get(item, "metadata", "labels") is { ValueKind: JsonValueKind.Object } labels
                ? labels.EnumerateObject()
                    .Where(l => l.Name.StartsWith("node-role.kubernetes.io/"))
                    .Select(l => l.Name["node-role.kubernetes.io/".Length..])
                : [];

            nodes.Add(new NodeInfo(
                Str(item, "metadata", "name") ?? "?",
                status,
                roles.Any() ? string.Join(",", roles) : "—",
                Str(item, "status", "nodeInfo", "kubeletVersion") ?? "?",
                AgeOf(item)));
        }

        return (nodes.OrderBy(n => n.Name, StringComparer.Ordinal).ToList(), null);
    }

    // ---- čitanje JSON-a bez izuzetaka kad nešto fali ----

    private static IEnumerable<JsonElement> Items(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array ? items.EnumerateArray() : [];

    private static JsonElement? Get(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next)) return null;
            current = next;
        }

        return current;
    }

    private static string? Str(JsonElement element, params string[] path) =>
        Get(element, path) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static List<JsonElement> Array(JsonElement element, params string[] path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().ToList() : [];

    private static string AgeOf(JsonElement item) =>
        Str(item, "metadata", "creationTimestamp") is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created)
            ? Age(DateTimeOffset.UtcNow - created)
            : "?";

    private static string Age(TimeSpan d) =>
        d.TotalDays >= 2 ? $"{(int)d.TotalDays}d"
        : d.TotalHours >= 1 ? $"{(int)d.TotalHours}h"
        : d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes}m"
        : $"{Math.Max(0, (int)d.TotalSeconds)}s";
}
