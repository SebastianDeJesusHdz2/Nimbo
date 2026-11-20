using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nimbo.Core;
using System.Text.RegularExpressions;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    void ReadSection(Chapter? selected)
    {
        var work = current!;
        var sections = WorldModel.Ordered(work).ToList();
        var section = selected ?? sections.FirstOrDefault();
        var root = new DockPanel(); var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        top.Children.Add(Action("‹ Editar", () => { chapter = section; Editor(); }));
        top.Children.Add(Action("Vista previa de impresión", async () => await Preview()));
        var picker = new ComboBox { ItemsSource = sections, SelectedItem = section, MaxWidth = 350 };
        picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is Chapter next) ReadSection(next); };
        top.Children.Add(picker); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var paper = Column(12); paper.MaxWidth = 780; paper.Margin = new Thickness(28, 16);
        paper.Children.Add(Label(work.Title, 15)); if (section != null)
        {
            paper.Children.Add(Display(section.Title, 32));
            if (section.Detail != "") paper.Children.Add(Label(section.Detail));
            void Illustration(string key)
            {
                var path = Path.Combine(shelf.Media, Path.GetFileName(key));
                try
                {
                    using var file = File.OpenRead(path); var bitmap = Bitmap.DecodeToWidth(file, 780);
                    var img = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxHeight = 460 };
                    img.DetachedFromVisualTree += (_, _) => bitmap.Dispose(); paper.Children.Add(img);
                }
                catch (Exception e) when (e is IOException or ArgumentException) { paper.Children.Add(Label("Ilustración no disponible")); }
            }
            void Prose(string text)
            {
                foreach (var line in text.ReplaceLineEndings("\n").Split('\n').Where(l => l.Trim().Length > 0))
                {
                    if (line.StartsWith("## ")) { paper.Children.Add(Display(line[3..], 24)); continue; }
                    if(line.Trim().StartsWith('|') && line.Trim().EndsWith('|') && line.Trim().Length>2){
                        var cells=line.Trim().Trim('|').Split('|');
                        var row=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",cells.Select(_=>"*")))};
                        for(int col=0;col<cells.Length;col++){
                            var cell=new Border{BorderThickness=new Thickness(0,0,0,1),BorderBrush=Accent,Padding=new Thickness(5),Child=Label(cells[col].Trim())};
                            Grid.SetColumn(cell,col);row.Children.Add(cell);
                        }
                        paper.Children.Add(row);continue;
                    }
                    var p = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = WritingSize, LineHeight = WritingSize*1.6, FontFamily=WritingFamily };
                    foreach (var piece in Regex.Split(line.StartsWith("- ") ? "•  " + line[2..] : line, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
                    {
                        if (piece.Length > 4 && piece.StartsWith("**") && piece.EndsWith("**")) p.Inlines!.Add(new Run(piece[2..^2]) { FontWeight = FontWeight.Bold });
                        else if (piece.Length > 2 && piece.StartsWith('*') && piece.EndsWith('*')) p.Inlines!.Add(new Run(piece[1..^1]) { FontStyle = FontStyle.Italic });
                        else p.Inlines!.Add(new Run(piece));
                    }
                    paper.Children.Add(p);
                }
            }
            int pos = 0; var shown = new HashSet<int>();
            foreach (Match m in Illustrations.Marker().Matches(section.PlainText))
            {
                if (!int.TryParse(m.Groups[1].Value, out var index) || index < 1 || index > section.Images.Count) continue;
                Prose(section.PlainText[pos..m.Index]); Illustration(section.Images[index - 1]); shown.Add(index - 1); pos = m.Index + m.Length;
            }
            Prose(section.PlainText[pos..]);
            for (int i = 0; i < section.Images.Count; i++) if (!shown.Contains(i)) Illustration(section.Images[i]);
            foreach (var entry in section.Entries)
            {
                paper.Children.Add(Display(entry.Heading, 21)); paper.Children.Add(Label(string.Join(" · ", new[] { entry.Organization, entry.Period }.Where(s => s != "")))); Prose(entry.Description);
            }
            if (section.Summary != "") Prose(section.Summary);
            var indexOf = sections.IndexOf(section); var nav = Row();
            if (indexOf > 0) nav.Children.Add(Action("‹ Anterior", () => ReadSection(sections[indexOf - 1])));
            if (indexOf + 1 < sections.Count) nav.Children.Add(Action("Siguiente ›", () => ReadSection(sections[indexOf + 1])));
            paper.Children.Add(nav);
        }
        root.Children.Add(new ScrollViewer { Content = Card(paper) }); page.Content = root;
    }
}

