using CloudAndBear.Ui.Input;
using CloudAndBear.Ui.Terminal;

namespace CloudAndBear.Ui.Services;

/// <summary>Sve što desktop traži od sistema, na jednom mestu.</summary>
public sealed record DesktopServices(
    ISystemService System,
    INetworkService Network,
    ITerminalService Terminal,
    IContainerService Containers,
    IKubeService Kube,
    IKeyboardLayoutService Keyboard)
{
    /// <summary>
    /// Na Linuxu pravi servisi (uređaj), drugde izmišljeni (pregled na PC-u). Raspored tastature deli sa ulazom
    /// uređaja: <c>CloudAndBear.Device</c> daje svoj, a bez njega se raspored ne pamti.
    /// </summary>
    public static DesktopServices Create(IKeyboardLayoutService? keyboard = null)
    {
        keyboard ??= new KeyboardLayoutService();

        return OperatingSystem.IsLinux()
            ? new DesktopServices(new LinuxSystemService(), new NetworkManagerService(), new PtyTerminalService(),
                new DockerEngineService(), new KubectlService(), keyboard)
            : new DesktopServices(new FakeSystemService(), new FakeNetworkService(), new FakeTerminalService(),
                new FakeContainerService(), new FakeKubeService(), keyboard);
    }
}
