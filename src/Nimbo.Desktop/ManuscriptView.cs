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

public sealed partial class Studio
{
    void Editor()
    {
        var work = current!; var root = new DockPanel(); var top = new WrapPanel{Margin=new Thickness(0,0,0,12)}; top.Children.Add(Action("‹ Biblioteca", Library));
        top.Children.Add(Action("Obra", Details)); top.Children.Add(Action("Glosario", () => Glossary(null)));
        top.Children.Add(Action("PDF", async () => await Export(true))); top.Children.Add(Action("LaTeX", async () => await Export(false)));
        top.Children.Add(Action("Guardar", () => TrySave())); top.Children.Add(Action("Guardar copia", () => Run(() =>
        {
            var clone = session.SaveCopy(); Open(clone, session.Revision);
        }))); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var left = Column(); left.Width = 196; left.Children.Add(Display(work.Title, 25)); left.Children.Add(Label(work.Type));
        var outline = new ListBox { ItemsSource = WorldModel.Ordered(work).ToArray(), SelectedItem = chapter, MaxHeight = 360 };
        outline.SelectionChanged += (_, _) => { if (outline.SelectedItem is Chapter c && c != chapter) { chapter = c; Editor(); } }; left.Children.Add(outline);
        void Add(string level, string title, string parent)
        {
            chapter = new() { Title = title, Level = level, ParentId = parent, Order = work.Chapters.Count }; work.Chapters.Add(chapter); Changed(); Editor();
        }
        var section = Format.All.FirstOrDefault(f => f.Name == work.Type)?.Section ?? "Capítulo";
        left.Children.Add(Action("+ " + section, () => Add("Chapter", section + " " + (work.Chapters.Count + 1), chapter?.Level == "Arc" ? chapter.Id : "")));
        if (work.IsNovel)
        {
            left.Children.Add(Action("+ Arco", () => Add("Arc", "Nuevo arco", "")));
            left.Children.Add(Action("+ Subcapítulo", () => { if (chapter != null) Add("Subchapter", "Nuevo subcapítulo", chapter.Id); }));
        }
        if (chapter != null)
        {
            var ordering = Row(); void Move(int direction)
            {
                var siblings = work.Chapters.Where(c => c.ParentId == chapter.ParentId).OrderBy(c => c.Order).ToList(); int index = siblings.IndexOf(chapter), target = index + direction;
                if (target < 0 || target >= siblings.Count) return;
                (siblings[index], siblings[target]) = (siblings[target], siblings[index]); for (int i = 0; i < siblings.Count; i++) siblings[i].Order = i; Changed(); Editor();
            }
            ordering.Children.Add(Action("↑", () => Move(-1))); ordering.Children.Add(Action("↓", () => Move(1))); left.Children.Add(ordering);
        }
        var side = Card(new ScrollViewer{Content=left}); side.Margin = new Thickness(0, 0, 16, 10); side.IsVisible=!work.FocusWriting;top.Children.Insert(1,Action(work.FocusWriting?"Ver índice":"Concentración",()=>{work.FocusWriting=!work.FocusWriting;Changed();Editor();}));DockPanel.SetDock(side, Dock.Left); root.Children.Add(side);
        var body = Column();body.MaxWidth=860;body.HorizontalAlignment=HorizontalAlignment.Stretch;
        if (chapter is { } c)
        {
            Field(body, work.Type == "Presentación" ? "Título de diapositiva" : "Título de sección", c.Title, v => c.Title = v);
            if (work.Type is "Apuntes" or "Acta" or "CV") Field(body, work.Type switch { "Apuntes" => "Preguntas y conceptos", "CV" => "Periodo y ubicación", _ => "Responsable y fecha" }, c.Detail, v => c.Detail = v);
            if (work.Type == "Presentación")
            {
                var options = new[] { "Portada", "Texto", "Texto e imagen", "Plano y leyenda", "Moodboard y paleta", "Moodboard y landmark", "Imagen protagonista", "Dos columnas" };
                body.Children.Add(Label("Composición de diapositiva"));
                var layouts = new ComboBox { ItemsSource = options, SelectedItem = options.Contains(c.Layout) ? c.Layout : "Texto" };
                layouts.SelectionChanged += (_, _) => { if (layouts.SelectedItem is string choice) { c.Layout = choice; Changed(); } };
                body.Children.Add(layouts); Field(body, "Texto secundario", c.Summary, v => c.Summary = v, true);
            }
            var input = Field(body, "Texto", c.PlainText, v => c.PlainText = v, true); input.FontSize = 18; input.MinHeight = 340;input.FontFamily=new FontFamily("Georgia, Liberation Serif, serif");input.Padding=new Thickness(22);input.LineHeight=30;
            var bar = Row();
            void Wrap(string marker)
            {
                int start = Math.Min(input.SelectionStart, input.SelectionEnd), end = Math.Max(input.SelectionStart, input.SelectionEnd);
                var text = input.Text ?? ""; input.Text = text[..start] + marker + text[start..end] + marker + text[end..]; input.Focus(); input.SelectionStart = start + marker.Length; input.SelectionEnd = end + marker.Length;
            }
            bar.Children.Add(Action("Negrita", () => Wrap("**"))); bar.Children.Add(Action("Cursiva", () => Wrap("*")));
            void Insert(string text)
            {
                int start = Math.Min(input.SelectionStart, input.SelectionEnd), end = Math.Max(input.SelectionStart, input.SelectionEnd);
                var old = input.Text ?? ""; input.Text = old[..start] + text + old[end..]; input.Focus(); input.CaretIndex = start + text.Length;
            }
            var formats = Row(); formats.Children.Add(Action("Subtítulo", () => Insert("\n## "))); formats.Children.Add(Action("Lista", () => Insert("\n- ")));
            formats.Children.Add(Action("Tabla", () => Insert("\n| Columna 1 | Columna 2 |\n| Dato | Dato |\n"))); body.Children.Remove(input);body.Children.Add(formats);
            bar.Children.Add(Action("Añadir imagen…", async () => await Guard(async () =>
            {
                var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Imagen PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
                if (path == null) return; c.Images.Add(shelf.AddImage(path)); if (work.Type != "Presentación") Insert(Environment.NewLine + Illustrations.At(c.Images.Count - 1) + Environment.NewLine); Changed(); Editor();
            }))); body.Children.Add(bar);body.Children.Add(input);
            if (c.Images.Count > 0 && work.Type != "Presentación") { body.Children.Add(Label("Disposición de las ilustraciones")); var layout = new ComboBox { ItemsSource = ImageLayouts, SelectedItem = ImageLayouts.Contains(c.Layout) ? c.Layout : ImageLayouts[0] }; layout.SelectionChanged += (_, _) => { if (layout.SelectedItem is string choice) { c.Layout = choice; Changed(); } }; body.Children.Add(layout); }
            foreach (var (key, index) in c.Images.Select((key, i) => (key, i)).ToArray())
            {
                var row = Row(); var imagePath = Path.Combine(shelf.Media, Path.GetFileName(key));
                if (File.Exists(imagePath)) { using var stream = File.OpenRead(imagePath); var bmp = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, 160); var preview = new Image { Source = bmp, Height = 78 }; preview.DetachedFromVisualTree += (_, _) => bmp.Dispose(); row.Children.Add(preview); }
                row.Children.Add(Label("Imagen " + (index + 1))); row.Children.Add(Action("Quitar imagen", () => { Illustrations.Remove(c, index); Changed(); Editor(); })); body.Children.Add(row);
            }
            if (work.Type == "Apuntes") Field(body, "Síntesis", c.Summary, v => c.Summary = v, true);
        }
        root.Children.Add(new ScrollViewer { Content = Card(body) }); page.Content = root;
    }
    void Details()
    {
        var work = current!; var body = Column();body.MaxWidth=860;body.HorizontalAlignment=HorizontalAlignment.Stretch; body.Children.Add(Action("‹ Manuscrito", Editor)); body.Children.Add(Label("Ficha de la obra", 28));
        body.Children.Add(Action("Elegir portada…", async () => await Guard(async () =>
        {
            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return; work.CoverImage = shelf.AddImage(path); Changed(); Details();
        })));
        if (work.CoverImage != "")
        {
            using var source = File.OpenRead(Path.Combine(shelf.Media, Path.GetFileName(work.CoverImage)));
            var bmp = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(source, 220); var img = new Image { Source = bmp, Height = 150, HorizontalAlignment = HorizontalAlignment.Left };
            img.DetachedFromVisualTree += (_, _) => bmp.Dispose(); body.Children.Add(img);
        }
        Field(body, "Título", work.Title, v => work.Title = v); Field(body, "Autor", work.Author, v => work.Author = v); Field(body, "Subtítulo", work.Subtitle, v => work.Subtitle = v); Field(body, "Colección", work.CollectionId, v => work.CollectionId = v); Field(body, "Descripción", work.Description, v => work.Description = v, true);
        if (work.Editions.Count > 0)
        {
            body.Children.Add(Label("Edición anterior", 21));
            foreach (var edition in work.Editions)
            {
                body.Children.Add(Action("Guardar copia de " + edition.Name, async () => await Guard(async () =>
                {
                    var destination = await StorageProvider.SaveFilePickerAsync(new() { Title = "Guardar edición anterior", SuggestedFileName = Path.GetFileName(edition.Name) });
                    if (destination?.TryGetLocalPath() is not string path) return;
                    var source = Path.Combine(shelf.Media, Path.GetFileName(edition.Key));
                    var temp = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.Copy(source, temp); File.Move(temp, path, true); } finally { File.Delete(temp); }
                    status.Text = "Edición anterior guardada";
                })));
            }
        }
        page.Content = new ScrollViewer { Content = Card(body) };
    }
}





