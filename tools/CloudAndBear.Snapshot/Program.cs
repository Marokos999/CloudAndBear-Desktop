using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CloudAndBear.Ui;
using CloudAndBear.Ui.Hosting;

// Snimi pogled u PNG bez otvaranja prozora (za proveru izgleda i za review).
// Upotreba: dotnet run --project tools/CloudAndBear.Snapshot -- [folder] [1920x1080 1366x768 ...]
var outDir = args.Length > 0 ? args[0] : "snapshots";
var sizes = args.Skip(1).Select(PreviewWindow.ParseSize).OfType<PixelSize>().ToList();
if (sizes.Count == 0)
{
    sizes.Add(new PixelSize(1920, 1080));
    sizes.Add(new PixelSize(1366, 768));
}

Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHarfBuzz()
    .WithInterFont()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

foreach (var size in sizes)
{
    var window = new PreviewWindow(size);
    window.Show();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

    var path = Path.Combine(outDir, $"main-{size.Width}x{size.Height}.png");
    window.CaptureRenderedFrame()?.Save(path, new PngBitmapEncoderOptions());
    window.Close();

    Console.WriteLine(Path.GetFullPath(path));
}
