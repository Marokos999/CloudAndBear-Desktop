using System.Net.Sockets;

namespace CloudAndBear.Ui.Services;

/// <summary>Izmišljeni kontejneri za pregled na PC-u: start, stop, restart i brisanje menjaju stanje.</summary>
public sealed class FakeContainerService : IContainerService
{
    private readonly List<ContainerInfo> _containers =
    [
        new("3f2a9c1d7b0e", "nginx", "nginx:1.27", "running", "Up 3 hours", "8080->80/tcp"),
        new("a81c04e5d2f6", "postgres", "postgres:16", "running", "Up 3 hours (healthy)", "5432/tcp"),
        new("c5d7e90b3a14", "redis", "redis:7-alpine", "exited", "Exited (0) 2 hours ago", ""),
        new("0b9e6f2a8c31", "worker", "registry.local/worker:2026.11", "restarting", "Restarting (1) 5 seconds ago", ""),
        new("7e1d3c5a9f20", "registry", "registry:2", "created", "Created", "5000->5000/tcp"),
    ];

    /// <summary>Koliko traje svaka radnja (da se vidi "Zaustavljam..."); u testu nula.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Kad je false, ponaša se kao da Docker ne radi (nema soketa).</summary>
    public bool Available { get; set; } = true;

    public async Task<IReadOnlyList<ContainerInfo>> ListAsync()
    {
        await Wait(4);
        return _containers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task StartAsync(string id)
    {
        await Wait();
        Replace(id, c => c.State == "running" ? c : c with { State = "running", Status = "Up Less than a second" });
    }

    public async Task StopAsync(string id)
    {
        await Wait();
        Replace(id, c => c with { State = "exited", Status = "Exited (0) Less than a second ago" });
    }

    public async Task RestartAsync(string id)
    {
        await Wait();
        Replace(id, c => c with { State = "running", Status = "Up Less than a second" });
    }

    public async Task RemoveAsync(string id)
    {
        await Wait();
        var c = Find(id);
        if (c.State == "running") throw new InvalidOperationException("You cannot remove a running container. Stop the container before attempting removal or force remove");
        _containers.Remove(c);
    }

    public async Task<string> LogsAsync(string id, int lines = 8)
    {
        await Wait(4);
        var c = Find(id);
        return c.State == "running"
            ? $"2026-11-21 08:00:01 [info] {c.Name} started\n" + (c.Ports.Length > 0 ? $"2026-11-21 08:00:02 [info] listening on :{c.Ports.Split("->")[^1].Split('/')[0]}\n" : "") + "2026-11-21 08:12:44 [info] request GET /healthz 200 3ms"
            : "";
    }

    private async Task Wait(int divisor = 1)
    {
        if (!Available) throw new HttpRequestException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay / divisor);
    }

    private ContainerInfo Find(string id) =>
        _containers.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException($"No such container: {id}");

    private void Replace(string id, Func<ContainerInfo, ContainerInfo> change)
    {
        var old = Find(id);
        _containers[_containers.IndexOf(old)] = change(old);
    }
}
