using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Media;
using Nimbo.Core;
using Nimbo.Publishing;
using System.Diagnostics;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    internal bool VerifyEditorialViews()
    {
        var cv = Format.All.Single(f => f.Name == "CV"); var test = EditorialTemplate.For(cv)[1].Create(cv, "CV de prueba");
        test.Chapters[0].PlainText = "Un **perfil** con énfasis."; test.Glossary.Add(new() { Name = "Ejemplo" });
        Open(test, shelf.Save(test, 0));
        bool Visible(string text) => page.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text);
        void Layout() { UpdateLayout(); }
        Layout(); Details(); Layout(); if (!Visible("Datos profesionales")) return false;
        ReadSection(chapter); Layout(); if (!Visible("CV de prueba")) return false;
        Glossary(test.Glossary[0]); Layout();
        Preferences(); Layout(); return Visible("Fondo de la biblioteca");
    }
    double WritingSize=>int.TryParse(ReadPref("cuerpo","18"),out var size)?Math.Clamp(size,14,28):18;
    FontFamily WritingFamily=>new(ReadPref("letra","Literaria") switch{
        "Sin serifas"=>"Arial, Liberation Sans, sans-serif",
        "Monoespaciada"=>"Consolas, Liberation Mono, monospace",
        _=>"Georgia, Liberation Serif, serif"
    });
    string ReadPref(string key, string fallback) => File.Exists(Config(key)) ? File.ReadAllText(Config(key)) : fallback;
    static ComboBox Choice(StackPanel parent, string label, string[] values, string current, Action<string> select)
    {
        parent.Children.Add(Label(label));
        var box = new ComboBox { ItemsSource = values, SelectedIndex = Math.Max(0, Array.IndexOf(values, current)), HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) => { if (box.SelectedItem is string value) select(value); };
        parent.Children.Add(box); return box;
    }
    static readonly string[] Tones = ["Océano", "Bosque", "Vino", "Grafito"];
    static readonly string[] Inks = ["Por formato", "Petróleo", "Vino", "Oliva", "Grafito"];
    void WritingPreferences(StackPanel panel)
    {
        Choice(panel, "Paleta de la interfaz", Tones, ReadPref("paleta", "Océano"), v => Run(() => { File.WriteAllText(Config("paleta"), v); ApplyTheme(); Preferences(); }));
        Choice(panel, "Tipografía de escritura", ["Literaria", "Sin serifas", "Monoespaciada"], ReadPref("letra", "Literaria"), v => Run(() => File.WriteAllText(Config("letra"), v)));
        Choice(panel, "Tamaño del texto", ["16", "18", "20", "22", "24"], ReadPref("cuerpo", "18"), v => Run(() => File.WriteAllText(Config("cuerpo"), v)));
        Choice(panel, "Ancho de escritura", ["Estrecho", "Medio", "Amplio"], ReadPref("ancho", "Medio"), v => Run(() => File.WriteAllText(Config("ancho"), v)));
    }
    void PrintSettings(StackPanel panel, Work work)
    {
        panel.Children.Add(Display("Diseño de publicación", 22));
        var format = Format.All.FirstOrDefault(f => f.Name == work.Type);
        if (format != null)
        {
            var options = EditorialTemplate.For(format);
            Choice(panel, "Estilo de la plantilla", options.Select(t => t.Name).ToArray(), work.Template, v => { work.Template = v; Changed(); });
        }
        if (work.Type != "Presentación") Choice(panel, "Tamaño de página", ["A4", "Carta", "A5"], work.Publication.Paper, v => { work.Publication.Paper = v; Changed(); });
        Choice(panel, "Tinta de títulos", Inks, work.PrintAccent == "" ? Inks[0] : work.PrintAccent, v => { work.PrintAccent = v; Changed(); });
        if (work.Type != "Presentación")
        {
            Choice(panel, "Interlineado impreso", ["Normal", "Abierto", "Amplio"], work.PrintSpacing, v => { work.PrintSpacing = v; Changed(); });
            Choice(panel, "Sangría de párrafo", ["Automática", "Sin sangría", "5 mm", "8 mm"], work.PrintIndent, v => { work.PrintIndent = v; Changed(); });
        }
        if (work.IsNovel)
        {
            var toc = new CheckBox { Content = "Incluir índice", IsChecked = work.Publication.Contents };
            toc.IsCheckedChanged += (_, _) => { work.Publication.Contents = toc.IsChecked == true; Changed(); }; panel.Children.Add(toc);
        }
        panel.Children.Add(Action("Vista previa de impresión", async () => await Preview()));
    }
    bool composing;
    async Task Preview(bool glossary = false) => await Guard(async () =>
    {
        if (composing || !TrySave()) return;
        composing = true;
        try
        {
            var work = session.Snapshot(); var engine = Engine();
            var dir = Directory.CreateDirectory(Path.Combine(shelf.Root, "Vistas previas"));
            foreach (var old in dir.EnumerateFiles("nimbo-*.pdf").Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7)))
            {
                try { old.Delete(); } catch (IOException) { }
            }
            var path = Path.Combine(dir.FullName, "nimbo-" + Guid.NewGuid().ToString("N") + ".pdf");
            status.Text = "Preparando vista previa…";
            await Task.Run(() => Publisher.Export(work, shelf.Media, path, true, glossary, engine));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); status.Text = "Vista previa abierta";
        }
        finally { composing = false; }
    });
    void ResumeProfile(StackPanel body, Work work)
    {
        var profile = Column(7); profile.Children.Add(Display("Datos profesionales", 24));
        foreach (var key in new[] { "Nombre profesional", "Profesión", "Correo", "Teléfono", "Ciudad", "Portafolio", "Perfil profesional en línea" })
            Field(profile, key, work.Contact.GetValueOrDefault(key, ""), v => work.Contact[key] = v);
        body.Children.Add(Card(profile));
    }
    void ResumeEntries(StackPanel body, Chapter c)
    {
        var list = Column(12); list.Children.Add(Display("Entradas de la sección", 22));
        foreach (var entry in c.Entries)
        {
            var card = Column(6);
            Field(card, "Puesto, título o logro", entry.Heading, v => entry.Heading = v);
            Field(card, "Organización o institución", entry.Organization, v => entry.Organization = v);
            Field(card, "Fechas", entry.Period, v => entry.Period = v);
            Field(card, "Descripción y resultados", entry.Description, v => entry.Description = v, true);
            var remove = Action("Quitar entrada", () => { }); bool confirm = false;
            remove.Click += (_, _) => { if (!confirm) { confirm = true; remove.Content = "Confirmar eliminación"; return; } c.Entries.Remove(entry); Changed(); Editor(); };
            card.Children.Add(remove); list.Children.Add(Card(card));
        }
        list.Children.Add(Action("+ Añadir entrada", () => { c.Entries.Add(new()); Changed(); Editor(); })); body.Children.Add(list);
    }
}



