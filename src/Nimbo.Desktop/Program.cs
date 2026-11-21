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
        RequestedThemeVariant = ThemeVariant.Dark;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var verifyEditors=desktop.Args?.Contains("--verify-editors")==true;
            var scratch=verifyEditors?Directory.CreateTempSubdirectory("nimbo-views-").FullName:null;
            var studio=new Studio(scratch);desktop.MainWindow=studio;
            if(verifyEditors)studio.Opened+=(_,_)=>Dispatcher.UIThread.Post(()=>desktop.Shutdown(studio.VerifyEditorialViews()?0:4),DispatcherPriority.ApplicationIdle);
            if(scratch!=null)desktop.Exit+=(_,_)=>{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(scratch,true);};
            if (desktop.Args?.Contains("--verify-ui") == true)
            {
                var window = desktop.MainWindow;
                window.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
                {
                    var rendered = window.GetVisualDescendants().OfType<Button>().Any(b => b.Name=="NavPreferencias");
                    if(rendered && Environment.GetEnvironmentVariable("NIMBO_UI_CAPTURE") is string path && window.Content is Control content){
                        var size=new PixelSize((int)Math.Ceiling(content.Bounds.Width),(int)Math.Ceiling(content.Bounds.Height));
                        using var image=new Avalonia.Media.Imaging.RenderTargetBitmap(size);
                        image.Render(content);image.Save(path);
                    }
                    desktop.Shutdown(rendered ? 0 : 3);
                }, DispatcherPriority.ApplicationIdle);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}



