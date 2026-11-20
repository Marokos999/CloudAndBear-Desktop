namespace CloudAndBear.Ui.Services;

public sealed record ContainerInfo(string Id, string Name, string Image, string State, string Status, string Ports);

/// <summary>
/// Docker kontejneri. Na uređaju je to Docker Engine API (<see cref="DockerEngineService"/>), a u pregledu na PC-u
/// izmišljeni kontejneri (<see cref="FakeContainerService"/>). Greške dolaze kao izuzeci: <see cref="InvalidOperationException"/>
/// sa porukom Dockera, a kad Docker ne radi <see cref="HttpRequestException"/> sa <see cref="System.Net.Sockets.SocketException"/> unutra.
/// </summary>
public interface IContainerService
{
    Task<IReadOnlyList<ContainerInfo>> ListAsync();

    Task StartAsync(string id);

    Task StopAsync(string id);

    Task RestartAsync(string id);

    Task RemoveAsync(string id);

    /// <summary>Poslednjih nekoliko linija ispisa kontejnera, bez boja.</summary>
    Task<string> LogsAsync(string id, int lines = 8);
}
