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
    void Glossary(Node? node)
    {
        var work = current!; var body = Column(); var bar = Row(); bar.Children.Add(Action("‹ Manuscrito", Editor));
        bar.Children.Add(Action("Nueva ficha", () => { var n = new Node { ParentId = node?.Id ?? "" }; work.Glossary.Add(n); Changed(); Glossary(n); }));
        bar.Children.Add(Action("Cuadernillo PDF", async () => await Export(true, true))); body.Children.Add(bar); body.Children.Add(Label("Glosario", 28));
        var tree = new TreeView();
        var branches = work.Glossary.ToDictionary(n => n.Id, n => new TreeViewItem { Header = n.Name, Tag = n, IsExpanded = true });
        foreach (var item in work.Glossary)
        {
            var branch = branches[item.Id];
            if (item.ParentId != "" && branches.TryGetValue(item.ParentId, out var parent) && !WorldModel.WouldCycle(work, item.Id, item.ParentId, ""))
                parent.Items.Add(branch);
            else tree.Items.Add(branch);
        }
        if (node != null) tree.SelectedItem = branches[node.Id];
        tree.SelectionChanged += (_, _) => { if (tree.SelectedItem is TreeViewItem { Tag: Node chosen } && chosen != node) Glossary(chosen); };
        var sidebar = Column(); sidebar.Width = 205; sidebar.Children.Add(Label("Índice", 20));
        sidebar.Children.Add(Action("+ Categoría", () => { var n = new Node { Name = "Nueva categoría", NodeKind = "Categoría", ParentId = node?.Id ?? "" }; work.Glossary.Add(n); Changed(); Glossary(n); }));
        sidebar.Children.Add(Action("Ir a raíz", () => Glossary(null))); sidebar.Children.Add(tree);
        if (node != null)
        {
            Field(body, "Nombre", node.Name, v => node.Name = v); Field(body, "Clase", node.NodeKind, v => node.NodeKind = v);
            Field(body, "Trasfondo", node.Background, v => node.Background = v, true); Field(body, "Relaciones", node.Relations, v => node.Relations = v);
            Field(body, "Alias", node.Aliases, v => node.Aliases = v); Field(body, "Edad o época", node.Age, v => node.Age = v); Field(body, "Medidas o extensión", node.Measures, v => node.Measures = v);
            Field(body, "Función narrativa", node.Role, v => node.Role = v);
            body.Children.Add(Action("Elegir ilustración…", async () => await Guard(async () =>
            {
                var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Imagen de la ficha" })).FirstOrDefault()?.TryGetLocalPath();
                if (path != null) { node.ImagePath = shelf.AddImage(path); Changed(); Glossary(node); }
            })));
            if (node.ImagePath != "")
            {
                using var source = File.OpenRead(Path.Combine(shelf.Media, Path.GetFileName(node.ImagePath)));
                var bmp = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(source, 200); var img = new Image { Source = bmp, Height = 140, HorizontalAlignment = HorizontalAlignment.Left };
                img.DetachedFromVisualTree += (_, _) => bmp.Dispose(); body.Children.Add(img);
            }
            body.Children.Add(Label("Ubicación en el índice"));
            var locations = work.Glossary.Where(n => !WorldModel.WouldCycle(work, node.Id, n.Id, node.BaseId)).ToArray();
            var location = new ComboBox { ItemsSource = locations, SelectedItem = locations.FirstOrDefault(n => n.Id == node.ParentId), PlaceholderText = "Raíz" };
            location.SelectionChanged += (_, _) => { if (location.SelectedItem is Node parent) { node.ParentId = parent.Id; Changed(); Glossary(node); } };
            body.Children.Add(location);
            if (node.ParentId != "") body.Children.Add(Action("Mover a raíz", () => { node.ParentId = ""; Changed(); Glossary(node); }));
            var baseOptions = work.Glossary.Where(n => !WorldModel.WouldCycle(work, node.Id, node.ParentId, n.Id)).ToArray();
            body.Children.Add(Label("Modelo del que recibe campos"));
            var bases = new ComboBox { ItemsSource = baseOptions, SelectedItem = baseOptions.FirstOrDefault(n => n.Id == node.BaseId), PlaceholderText = "Elegir ficha base" };
            bases.SelectionChanged += (_, _) => { if (bases.SelectedItem is Node parent) { node.BaseId = parent.Id; Changed(); Glossary(node); } }; body.Children.Add(bases);
            if (node.BaseId != "") body.Children.Add(Action("Quitar ficha base", () => { node.BaseId = ""; Changed(); Glossary(node); }));
            var fields = Row(); var key = new TextBox { Watermark = "Campo heredable", MinWidth = 165 }; var initial = new TextBox { Watermark = "Valor inicial", MinWidth = 140 }; fields.Children.Add(key); fields.Children.Add(initial);
            fields.Children.Add(Action("Añadir campo", () => { if (string.IsNullOrWhiteSpace(key.Text)) return; node.AttributeDefinitions.Add(new() { Name = key.Text.Trim(), DefaultValue = initial.Text ?? "" }); Changed(); Glossary(node); })); body.Children.Add(fields);
            Field(body, "Notas", node.Notes, v => node.Notes = v, true);
            foreach (var field in WorldModel.Effective(work, node))
            {
                Field(body, field.Definition.Name, field.Value, v =>
                {
                    var val = node.Attributes.FirstOrDefault(a => a.DefinitionId == field.Definition.Id);
                    if (val == null) { val = new() { DefinitionId = field.Definition.Id }; node.Attributes.Add(val); }
                    val.Value = v;
                });
            }
        }
        var panel = new DockPanel(); var index = Card(new ScrollViewer { Content = sidebar }); index.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(index, Dock.Left); panel.Children.Add(index); panel.Children.Add(new ScrollViewer { Content = Card(body) }); page.Content = panel;
    }
}

