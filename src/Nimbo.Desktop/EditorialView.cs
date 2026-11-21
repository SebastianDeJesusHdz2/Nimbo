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
        void Layout() { Avalonia.Threading.Dispatcher.UIThread.RunJobs();UpdateLayout(); }
        void Capture(string name){
            if(Environment.GetEnvironmentVariable("NIMBO_VIEW_SAMPLES") is not string folder || Content is not Control view)return;
            using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Math.Ceiling(view.Bounds.Width),(int)Math.Ceiling(view.Bounds.Height)));
            bitmap.Render(view);bitmap.Save(Path.Combine(folder,name+".png"));
        }
        bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line=0){
            Console.Error.WriteLine($"Fallo en la comprobación editorial (línea {line}, ventana {Bounds.Size}, área {page.Bounds.Size}).");
            Capture("Diagnostico");return false;
        }
        Layout(); Details(); Layout(); if (!Visible("Datos profesionales")) return Fail();
        ReadSection(chapter); Layout(); if (!Visible("CV de prueba")) return Fail();
        var novel=Format.All.First(f=>f.Name=="Novela").Create("Los jardines de invierno");
        novel.Chapters[0].Title="La casa de cristal";
        novel.Chapters[0].PlainText="A las seis, la escarcha había borrado el nombre de todas las calles. Inés reconoció la suya por el único balcón donde aún quedaban geranios.";
        var town=new Node{Name="Puerto de Lumbre",NodeKind="Lugar"};
        var botanist=new Node{Name="Inés Salgado",NodeKind="Personaje",Role="Botánica municipal",Background="Custodia el último invernadero de la ciudad.",RelatedIds=[town.Id]};
        novel.Chapters[0].AuthorMemo="Volver al sonido del cristal cuando Inés abra el herbario.";
        novel.Chapters[0].PlainText+="\n\nHabía prometido dejar la llave en portería. Ahora la llevaba escondida en el puño, tan fría que le había dibujado cuatro dientes en la palma. Desde la acera de enfrente, la casa parecía una lámpara apagada.\n\nEmpujó la verja. Al otro lado del cristal alguien había regado las plantas. Bajo cada maceta quedaba un círculo de agua todavía temblando.\n\n—Has tardado —dijo una voz desde el fondo.";
        novel.Chapters.Add(new(){Title="El herbario",Order=1});novel.Chapters.Add(new(){Title="Cartas desde el puerto",Order=2});
        town.Background="Puerto de canales estrechos al pie de los invernaderos.";botanist.Age="32 años";botanist.Pinned=true;
        novel.Glossary.AddRange([town,botanist,new Node{Name="Casa de cristal",NodeKind="Lugar",ParentId=town.Id,Background="Invernadero orientado al este."}]);Open(novel,shelf.Save(novel,0));Layout();
        if(!Visible("Índice del manuscrito"))return Fail();Capture("Manuscrito");
        static TextBox? NoteIn(Control root)=>root.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b=>Avalonia.Automation.AutomationProperties.GetName(b)=="Notas de la sección");
        TextBox? OpenPocket(out Flyout? popup){
            popup=null;
            var button=page.GetVisualDescendants().OfType<Button>().FirstOrDefault(b=>b.IsEffectivelyVisible && Equals(b.Content,"Cuaderno"));
            if(button?.Flyout is not Flyout flyout)return null;
            flyout.ShowAt(button);Layout();popup=flyout;
            return flyout.Content is Control content?NoteIn(content):null;
        }
        Flyout? open=null;var note=NoteIn(page)??OpenPocket(out open);
        if(note==null)return Fail();note.Text="Nota de revisión guardada";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        open?.Hide();
        if(!TrySave()||shelf.Open(novel.Id).Work.Chapters[0].AuthorMemo!="Nota de revisión guardada")return Fail();
        var sample=Path.Combine(shelf.Root,"Muestra.jpg");
        using(var image=Avalonia.Platform.AssetLoader.Open(new Uri("avares://Nimbo.Desktop/Art/observatorio.jpg")))
        using(var file=File.Create(sample))image.CopyTo(file);
        var illustrated=novel.Chapters[1];illustrated.Images.Add(shelf.AddImage(sample));illustrated.Title="El observatorio";
        illustrated.PlainText="Desde el puente, Inés distinguió la cúpula entre los cipreses.\n"+Illustrations.At(0)+"\nLas ventanas reflejaban la última luz del puerto.";
        chapter=illustrated;Changed();Editor();Layout();
        var blocks=page.GetVisualDescendants().OfType<TextBox>().Where(b=>Avalonia.Automation.AutomationProperties.GetName(b)=="Texto del manuscrito").ToArray();
        if(blocks.Length!=2)return Fail();
        blocks[1].Text+=" En la terraza aguardaba una carta.";
        var placement=page.GetVisualDescendants().OfType<ComboBox>().Single(b=>b.Items.Contains("De la sección"));
        placement.SelectedItem="Lámina ilustrada";Layout();Capture("Escritura-ilustrada");
        if(!TrySave())return Fail();
        var restored=shelf.Open(novel.Id).Work.Chapters[1];
        if(!restored.PlainText.Contains(Illustrations.At(0))||!restored.PlainText.EndsWith("una carta.")||Illustrations.Layout(restored,0)!="Lámina ilustrada")return Fail();
        illustrated.Images.Add(illustrated.Images[0]);illustrated.ImageLayouts[0]=Illustrations.Gallery;illustrated.ImageLayouts[1]=Illustrations.Gallery;
        illustrated.PlainText=illustrated.PlainText.Replace(Illustrations.At(0),Illustrations.At(0)+Illustrations.At(1));
        illustrated.ImageCaptions[1]="El puerto";Changed();Editor();Layout();
        if(Illustrations.Rows(illustrated).Single().Count!=2 || !page.GetVisualDescendants().OfType<TextBox>().Any(t=>t.Text=="El puerto"))return Fail();
        Capture("Galería");ReadSection(illustrated);Layout();if(!Visible("El puerto"))return Fail();
        chapter=novel.Chapters[0];
        Glossary(botanist,2);Layout();if(!Visible("Relaciones"))return Fail();
        Glossary(botanist);Layout();Capture("Ficha");
        Glossary(null);Layout();Capture("Glosario");
        var search=page.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Watermark=="Buscar nombre, alias o clase");
        search.Text="Salgado";Avalonia.Threading.Dispatcher.UIThread.RunJobs();Layout();
        if(page.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Puerto de Lumbre"))return Fail();
        search.Text="";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        page.Width=680;Editor();Layout();
        if(OpenPocket(out open)?.Text!="Nota de revisión guardada")return Fail();
        open?.Hide();Layout();
        page.Width=double.NaN;
        if(!TrySave() || shelf.Open(novel.Id).Work.Glossary[1].RelatedIds.Single()!=town.Id)return Fail();
        File.WriteAllText(Config("tema"),"Claro");ApplyTheme();Glossary(botanist);Layout();Capture("Ficha-clara");
        if(!Visible(botanist.Name))return Fail();
        Preferences(); Layout();if(!Visible("Fondo de la biblioteca"))return Fail();
        File.WriteAllText(Config("tema"),"Oscuro");ApplyTheme();
        foreach(var name in new[]{"Bajo el hielo","Manual de mareas","Cantos del sur","Las manos del reloj","La memoria del muelle","Voces del camino","La carta de Aro","Cuaderno de campo"}){
            var book=Format.All.First(f=>f.Name=="Novela").Create(name);
            book.CoverColor=palette.Swatches[name.Length%palette.Swatches.Length];book.Author="Inés Salgado";
            shelf.Save(book,0);
        }
        Library();Layout();Capture("Estantería");
        return page.GetVisualDescendants().OfType<BookCover>().Count()>=10 || Fail();
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
        var strip=new WrapPanel();
        foreach(var hex in palette.Swatches){
            var sample=new Border{Width=42,Height=23,Background=Brush.Parse(hex),CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,3,0)};
            ToolTip.SetTip(sample,hex);strip.Children.Add(sample);
        }
        panel.Children.Add(strip);panel.Children.Add(Label("Adobe Color · "+palette.Source,12));
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



