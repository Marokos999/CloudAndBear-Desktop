using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CloudAndBear.Ui.Hosting;
using CloudAndBear.Ui.Views;

namespace CloudAndBear.Ui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            // PC (razvoj): običan prozor oko istog pogleda koji ide na uređaj
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new PreviewWindow(PreviewWindow.ParseSize(desktop.Args));
                break;

            // uređaj (DRM): nema prozora, jedan pogled preko celog ekrana
            case ISingleViewApplicationLifetime single:
                single.MainView = new DesktopView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
