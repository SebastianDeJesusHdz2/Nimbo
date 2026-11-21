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
        if(args is ["--importar-biblioteca",var source]){
            var shelf=new Nimbo.Storage.Shelf(Nimbo.Storage.UserLibrary.OpenDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
            Nimbo.Storage.Legacy.Import(shelf,source);return 0;
        }
        if(args is ["--edicion-local",var library,var destination,var engine]){
            var scratch=Directory.CreateTempSubdirectory("nimbo-edicion-");
            try{
                var shelf=new Nimbo.Storage.Shelf(scratch.FullName);
                if(Nimbo.Storage.Legacy.Import(shelf,library)!=1)throw new InvalidDataException("La edición necesita una biblioteca con una obra.");
                var work=shelf.Open(shelf.List().Single().Id).Work;
                Nimbo.Publishing.ReadingEdition.Write(work,shelf.Media,destination,engine).GetAwaiter().GetResult();
                return 0;
            }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();scratch.Delete(true);}
        }
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
            var studio=new Studio(scratch);
            if(verifyEditors){studio.WindowState=WindowState.Normal;studio.Width=1600;studio.Height=960;}
            desktop.MainWindow=studio;
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



