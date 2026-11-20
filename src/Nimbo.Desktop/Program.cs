using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Styling;
using Velopack;

namespace Nimbo.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run();
        return AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
public sealed class App : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant=ThemeVariant.Dark;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow=new Studio();
            if(desktop.Args?.Contains("--verify-ui")==true){
                var window=desktop.MainWindow;
                window.Opened+=(_,_)=>Dispatcher.UIThread.Post(()=>{
                    var rendered=window.GetVisualDescendants().OfType<Button>().Any(b=>Equals(b.Content,"Crear obra"));
                    desktop.Shutdown(rendered?0:3);
                },DispatcherPriority.ApplicationIdle);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}

