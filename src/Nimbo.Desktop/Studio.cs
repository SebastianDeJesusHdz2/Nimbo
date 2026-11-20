using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Nimbo.Core;
using Nimbo.Storage;
using Nimbo.Publishing;
using Velopack;
using Velopack.Sources;

namespace Nimbo.Desktop;

public sealed partial class Studio : Window
{
    readonly Shelf shelf;
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    readonly ContentControl page = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1.2) };
    readonly Button updateButton = new() { Content = "Actualizar", IsVisible = false };
    readonly UpdateManager updater = new(new GithubSource("https://github.com/SebastianDeJesusHdz2/Nimbo", null, true));
    UpdateInfo? update;
    readonly Nimbo.Application.ManuscriptSession session;
    Work? current => session.Current;
    Chapter? chapter;


    static IBrush Accent = Brush.Parse("#A4DACC");
    static IBrush PanelColor = Brush.Parse("#202A32");

    public Studio(string? testLibrary=null)
    {
        shelf=new Shelf(testLibrary??UserLibrary.OpenDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        session = new(shelf);
        Title = "Nimbo"; Width = 1280; Height = 850;WindowState=WindowState.Maximized; MinWidth = 760; MinHeight = 540;
        Background = Brush.Parse("#141C23");
        var frame = new DockPanel { Margin = new Thickness(24) };
        var footer = Row(); footer.Children.Add(status); footer.Children.Add(updateButton);
        DockPanel.SetDock(footer, Dock.Bottom); frame.Children.Add(footer);
        frame.Children.Add(page); Content = frame;
        timer.Tick += (_, _) => { timer.Stop(); TrySave(); };
        Closing += (_, e) => { if (!TrySave()) e.Cancel = true; };
        KeyDown += (_, e) => { if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { TrySave(); e.Handled = true; } };
        Opened += async (_, _) =>
        {
            if (!updater.IsInstalled) return;
            try { update = await updater.CheckForUpdatesAsync(); updateButton.IsVisible = update != null; } catch (HttpRequestException) { } catch (Exception ex) { status.Text = "Actualización: " + ex.Message; }
        };
        updateButton.Click += async (_, _) => await Guard(async () =>
        {
            if (update == null) return;
            updateButton.IsEnabled = false;
            try { await updater.DownloadUpdatesAsync(update); if (TrySave()) updater.ApplyUpdatesAndRestart(update.TargetFullRelease); }
            finally { updateButton.IsEnabled = true; }
        });
        ApplyTheme(); Library();
    }
    static StackPanel Column(double spacing = 12) => new() { Spacing = spacing };
    static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6) };
    static TextBlock Label(string text, double size = 14) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
    static Button Action(string text, Action action) { var b = new Button { Content = text, CornerRadius=new CornerRadius(6),Padding=new Thickness(12,7) }; b.Click += (_, _) => action(); return b; }
    static Border Card(Control child) => new() { Background = PanelColor, CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Child = child };
    async Task Guard(Func<Task> action) { try { await action(); } catch (Exception ex) { status.Text = ex.Message; } }
    void Run(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }
    void Changed() { session.MarkChanged(); status.Text = "Cambios pendientes"; timer.Stop(); timer.Start(); }
    bool TrySave()
    {
        if (!session.HasChanges) return true;
        try { session.Save(); status.Text = "Guardado"; return true; }
        catch (Exception ex) { status.Text = ex.Message; return false; }
    }
    void Open(Work work, long rev) { session.Open(work, rev); var last=work.LastChapterId;try{if(File.Exists(SectionPath(work)))last=File.ReadAllText(SectionPath(work));}catch(IOException){} chapter=work.Chapters.FirstOrDefault(c=>c.Id==last)??WorldModel.Ordered(work).FirstOrDefault(); status.Text = "Guardado"; Editor(); }
    TextBox Field(StackPanel host, string caption, string value, Action<string> assign, bool multi = false)
    {
        host.Children.Add(Label(caption));
        var box = new TextBox { Text = value, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 130 : 32 };
        box.TextChanged += (_, _) => { if ((box.Text ?? "") == value) return; value = box.Text ?? ""; assign(value); Changed(); };
        host.Children.Add(box); return box;
    }
    static readonly string[] ImageLayouts = ["Centrada", "Página completa", "Ajustar a la izquierda", "Ajustar a la derecha"];
    string Config(string name) => Path.Combine(shelf.Root, name + ".txt");
    string? Engine()
    {
        if (File.Exists(Config("xelatex"))) return File.ReadAllText(Config("xelatex"));
        return Environment.GetEnvironmentVariable("NIMBO_XELATEX");
    }
    void Preferences()
    {
        var body = Column(); body.Children.Add(Action("‹ Biblioteca", Library)); body.Children.Add(Label("Preferencias", 28));
        var sections=new TabControl();var design=Column();var identity=Column();var publishing=Column();
        var theme = new ComboBox { ItemsSource = new[] { "Oscuro", "Claro" }, SelectedIndex = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? 1 : 0 };
        theme.SelectionChanged += (_, _) => Run(() => { File.WriteAllText(Config("tema"), theme.SelectedIndex == 1 ? "Claro" : "Oscuro"); ApplyTheme();Preferences(); }); design.Children.Add(Label("Apariencia")); design.Children.Add(theme);
        WritingPreferences(design);body.MaxWidth=900;body.Margin=new Thickness(24);
        identity.Children.Add(Label("Nombre para las obras nuevas")); var author = new TextBox { Text = File.Exists(Config("autor")) ? File.ReadAllText(Config("autor")) : "", Watermark = "Nombre o seudónimo" }; identity.Children.Add(author); identity.Children.Add(Action("Guardar nombre", () => Run(() => File.WriteAllText(Config("autor"), author.Text ?? ""))));
        design.Children.Add(Label("Fondo de la biblioteca"));
        var backgrounds=Row();backgrounds.Children.Add(Action("Elegir imagen…",async()=>await Guard(async()=>{
            var f=(await StorageProvider.OpenFilePickerAsync(new(){Title="Fondo PNG o JPEG"})).FirstOrDefault()?.TryGetLocalPath();
            if(f!=null){File.WriteAllText(Config("fondo"),shelf.AddImage(f));Library();}
        })));if(File.Exists(Config("fondo")))backgrounds.Children.Add(Action("Quitar fondo",()=>Run(()=>{File.Delete(Config("fondo"));Library();})));design.Children.Add(backgrounds);
        publishing.Children.Add(Label("Motor de publicación")); publishing.Children.Add(Label(Engine() ?? "XeLaTeX del sistema"));
        publishing.Children.Add(Action("Seleccionar XeLaTeX…", async () => await Guard(async () =>
        {
            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Seleccionar xelatex o xelatex.exe" })).FirstOrDefault()?.TryGetLocalPath();
            if (path != null) { if (!Path.GetFileNameWithoutExtension(path).Equals("xelatex", StringComparison.OrdinalIgnoreCase)) throw new IOException("Selecciona el ejecutable xelatex."); File.WriteAllText(Config("xelatex"), path); Preferences(); }
        }))); sections.ItemsSource=new[]{new TabItem{Header="Apariencia y escritura",Content=Card(design)},new TabItem{Header="Autor",Content=Card(identity)},new TabItem{Header="Publicación",Content=Card(publishing)}};sections.SelectedIndex=0;body.Children.Add(sections);page.Content = new ScrollViewer{Content=Card(body)};
    }
    void ApplyTheme()
    {
        var light = File.Exists(Config("tema")) && File.ReadAllText(Config("tema")) == "Claro";
        var tone=ReadPref("paleta","Océano");
        RequestedThemeVariant = light ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        Background = Brush.Parse(light ? "#F5F3ED" : tone switch {"Bosque"=>"#12221F","Vino"=>"#251921","Grafito"=>"#202124",_=>"#141C23"}); PanelColor = Brush.Parse(light ? (tone switch {"Vino"=>"#F1E4E9","Bosque"=>"#E3EEE6","Grafito"=>"#E5E6E8",_=>"#E5EDF0"}) : tone switch {"Bosque"=>"#20362F","Vino"=>"#3B2631","Grafito"=>"#303236",_=>"#202A32"}); Accent = Brush.Parse((tone,light) switch {("Vino",true)=>"#863955",("Vino",false)=>"#EDADC3",("Bosque",false)=>"#A5D5A6",("Grafito",false)=>"#C7CEDC",(_,true)=>"#23584E",_=>"#A4DACC"});
        var tint=(Accent as ISolidColorBrush)!.Color;
        Resources["SystemAccentColor"]=tint;
        foreach(var key in new[]{"SystemAccentColorLight1","SystemAccentColorLight2","SystemAccentColorLight3","SystemAccentColorDark1","SystemAccentColorDark2","SystemAccentColorDark3"})Resources[key]=tint;
    }
    async Task Export(bool pdf, bool glossary = false) => await Guard(async () =>
    {
        if (!TrySave()) return;
        var snapshot = session.Snapshot();
        var ext = pdf ? "pdf" : "zip"; var file = await StorageProvider.SaveFilePickerAsync(new() { Title = pdf ? "Exportar PDF" : "Proyecto LaTeX", DefaultExtension = ext, SuggestedFileName = (glossary ? "Glosario - " : "") + snapshot.Title + "." + ext });
        if (file?.TryGetLocalPath() is not string path) return;
        status.Text = "Componiendo…"; await Task.Run(() => Publisher.Export(snapshot, shelf.Media, path, pdf, glossary, Engine())); status.Text = "Exportado · " + Path.GetFileName(path);
    });
}













