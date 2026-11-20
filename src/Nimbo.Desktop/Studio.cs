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
    readonly Shelf shelf = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nimbo"));
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    readonly ContentControl page = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1.2) };
    readonly Button updateButton = new() { Content = "Actualizar", IsVisible = false };
    readonly UpdateManager updater = new(new GithubSource("https://github.com/SebastianDeJesusHdz2/Nimbo", null, false));
    UpdateInfo? update;
    readonly Nimbo.Application.ManuscriptSession session;
    Work? current => session.Current;
    Chapter? chapter;


    static IBrush Accent = Brush.Parse("#A4DACC");
    static IBrush PanelColor = Brush.Parse("#202A32");

    public Studio()
    {
        session = new(shelf);
        Title = "Nimbo"; Width = 1230; Height = 820; MinWidth = 760; MinHeight = 540;
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
    static Button Action(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => action(); return b; }
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
    void Open(Work work, long rev) { session.Open(work, rev); chapter = WorldModel.Ordered(work).FirstOrDefault(); status.Text = "Guardado"; Editor(); }
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
        var theme = new ComboBox { ItemsSource = new[] { "Oscuro", "Claro" }, SelectedIndex = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? 1 : 0 };
        theme.SelectionChanged += (_, _) => Run(() => { File.WriteAllText(Config("tema"), theme.SelectedIndex == 1 ? "Claro" : "Oscuro"); ApplyTheme(); }); body.Children.Add(Label("Apariencia")); body.Children.Add(theme);
        body.Children.Add(Label("Nombre para las obras nuevas")); var author = new TextBox { Text = File.Exists(Config("autor")) ? File.ReadAllText(Config("autor")) : "", Watermark = "Nombre o seudónimo" }; body.Children.Add(author); body.Children.Add(Action("Guardar nombre", () => Run(() => File.WriteAllText(Config("autor"), author.Text ?? ""))));
        body.Children.Add(Label("Motor de publicación")); body.Children.Add(Label(Engine() ?? "XeLaTeX del sistema"));
        body.Children.Add(Action("Seleccionar XeLaTeX…", async () => await Guard(async () =>
        {
            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Seleccionar xelatex o xelatex.exe" })).FirstOrDefault()?.TryGetLocalPath();
            if (path != null) { if (!Path.GetFileNameWithoutExtension(path).Equals("xelatex", StringComparison.OrdinalIgnoreCase)) throw new IOException("Selecciona el ejecutable xelatex."); File.WriteAllText(Config("xelatex"), path); Preferences(); }
        }))); page.Content = Card(body);
    }
    void ApplyTheme()
    {
        var light = File.Exists(Config("tema")) && File.ReadAllText(Config("tema")) == "Claro";
        RequestedThemeVariant = light ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        Background = Brush.Parse(light ? "#F5F3ED" : "#141C23"); PanelColor = Brush.Parse(light ? "#E7ECE9" : "#202A32"); Accent = Brush.Parse(light ? "#23584E" : "#A4DACC");
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





