using CloudAndBear.Ui.Terminal;

namespace CloudAndBear.Ui.Services;

/// <summary>Sve što desktop traži od sistema, na jednom mestu.</summary>
public sealed record DesktopServices(
    ISystemService System,
    INetworkService Network,
    ITerminalService Terminal,
    IContainerService Containers,
    IKubeService Kube)
{
    /// <summary>Na Linuxu pravi servisi (uređaj), drugde izmišljeni (pregled na PC-u).</summary>
    public static DesktopServices Create() =>
        OperatingSystem.IsLinux()
            ? new DesktopServices(new LinuxSystemService(), new NetworkManagerService(), new PtyTerminalService(),
                new DockerEngineService(), new KubectlService())
            : new DesktopServices(new FakeSystemService(), new FakeNetworkService(), new FakeTerminalService(),
                new FakeContainerService(), new FakeKubeService());
}
