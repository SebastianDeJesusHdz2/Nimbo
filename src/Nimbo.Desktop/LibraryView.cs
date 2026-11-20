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
    void Library()
    {
        if (!TrySave()) return; session.Close(); chapter = null;
        var root = new DockPanel(); var top = Column(); var heading = Label("Nimbo", 38); heading.Foreground = Accent; top.Children.Add(heading);
        top.Children.Add(Label("Biblioteca", 23));
        var create = Row(); var format = new ComboBox { ItemsSource = Format.All, SelectedIndex = 0, MinWidth = 140 };
        var name = new TextBox { Watermark = "Título de la nueva obra", MinWidth = 230 };
        create.Children.Add(format); create.Children.Add(name);
        create.Children.Add(Action("Crear obra", () => Run(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return; }
            var work = ((Format)format.SelectedItem!).Create(name.Text); if (File.Exists(Config("autor"))) work.Author = File.ReadAllText(Config("autor"));
            var rev = shelf.Save(work, 0); Open(work, rev);
        })));
        create.Children.Add(Action("Importar biblioteca…", async () => await Guard(async () =>
        {
            var folder = (await StorageProvider.OpenFolderPickerAsync(new() { Title = "Carpeta de LumenWriter" })).FirstOrDefault()?.TryGetLocalPath();
            if (folder == null) return;
            var count = await Task.Run(() => Legacy.Import(shelf, folder)); Library(); status.Text = $"{count} obras importadas.";
        })));
        top.Children.Add(create); top.Children.Add(Action("Preferencias", Preferences)); var search = new TextBox { Watermark = "Buscar en la biblioteca", MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 300 }; top.Children.Add(search);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var books = new WrapPanel { Margin = new Thickness(0, 16) };
        var entries = shelf.List(); var groups = new ComboBox { ItemsSource = new[] { "Todas las colecciones" }.Concat(entries.Select(e => e.Collection).Where(g => g != "").Distinct()).ToArray(), SelectedIndex = 0 }; top.Children.Add(groups);
        void Filter()
        {
            books.Children.Clear();
            foreach (var entry in entries.Where(e => e.Title.Contains(search.Text ?? "", StringComparison.CurrentCultureIgnoreCase) && (groups.SelectedIndex == 0 || e.Collection == groups.SelectedItem as string)))
            {
                var book = new BookCover(entry, shelf.Media, () => Run(() => { var item = shelf.Open(entry.Id); Open(item.Work, item.Revision); }));
                books.Children.Add(book);
            }
        }
        groups.SelectionChanged += (_, _) => Filter(); search.TextChanged += (_, _) => Filter(); Filter(); root.Children.Add(new ScrollViewer { Content = books }); page.Content = root;
    }
}
