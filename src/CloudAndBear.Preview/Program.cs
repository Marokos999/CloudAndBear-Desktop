using Avalonia;
using CloudAndBear.Ui;

namespace CloudAndBear.Preview;

internal static class Program
{
    // pregled na PC-u: dotnet run --project src/CloudAndBear.Preview -- --size=1920x1080
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // isti font i isto oblikovanje teksta kao na uređaju (Inter + HarfBuzz); koristi ga i IDE previewer
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseHarfBuzz()
            .WithInterFont()
            .LogToTrace();
}
