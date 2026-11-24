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
        Navigate("Lectura");var work = current!;
        var sections = WorldModel.Ordered(work).ToList();
        var section = selected ?? sections.FirstOrDefault();
        var root = new DockPanel(); var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        top.Children.Add(Action("‹ Editar", () => { chapter = section; Editor(); }));
        top.Children.Add(Action("Vista previa de impresión", async () => await Preview()));
        var picker = new ComboBox { ItemsSource = sections, SelectedItem = section, MaxWidth = 350 };
        picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is Chapter next) ReadSection(next); };
        top.Children.Add(picker); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var paper = Column(18); paper.MaxWidth=780;paper.Margin=new Thickness(34,32);
        paper.Children.Add(Label(work.Title, 15)); if (section != null)
        {
            paper.Children.Add(Display(section.Title,42));paper.Children.Add(Rule(Edge));
            if (section.Detail != "") paper.Children.Add(Label(section.Detail));
            void Illustration(int index,Panel destination,bool small=false)
            {
                var path = Path.Combine(shelf.Media, Path.GetFileName(section.Images[index]));
                try
                {
                    using var file = File.OpenRead(path); var bitmap = Bitmap.DecodeToWidth(file, 780);
                    var layout=Illustrations.Layout(section,index);
                    var img=new Image{Source=bitmap,Stretch=Stretch.Uniform,MaxHeight=layout=="Página completa"?680:layout.StartsWith("Ajustar")?240:460,
                        HorizontalAlignment=layout.EndsWith("izquierda")?HorizontalAlignment.Left:layout.EndsWith("derecha")?HorizontalAlignment.Right:HorizontalAlignment.Center};
                    if(small)img.MaxHeight=170;
                    img.DetachedFromVisualTree += (_, _) => bitmap.Dispose(); destination.Children.Add(img);
                }
                catch (Exception e) when (e is IOException or ArgumentException) { destination.Children.Add(Label("Ilustración no disponible")); }
                if(section.ImageCaptions.TryGetValue(index,out var caption) && caption!="")destination.Children.Add(Label(caption,13));
            }
            void Prose(string text)
            {
                foreach (var line in text.ReplaceLineEndings("\n").Split('\n').Where(l => l.Trim().Length > 0))
                {
                    if(line=="[[Página]]" || line=="[[Índice]]"){paper.Children.Add(new Separator());continue;}
                    if (line.StartsWith("### ")) { paper.Children.Add(Display(line[4..], 19)); continue; }
                    if (line.StartsWith("## ")) { paper.Children.Add(Display(line[3..], 24)); continue; }
                    if (line.StartsWith("[[Paleta:") && line.EndsWith("]]"))
                    {
                        var strip = new WrapPanel { Margin = new Thickness(0, 4) };
                        foreach (var item in line[9..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            var m = Regex.Match(item, @"^#?([0-9A-Fa-f]{6})(?:\s+(.*))?$");
                            var hex = "#" + (m.Success ? m.Groups[1].Value.ToUpperInvariant() : "666666");
                            var name = m.Success ? m.Groups[2].Value.Trim() : item;
                            var swatch = Column(3); swatch.Width = 112; swatch.Margin = new Thickness(0, 0, 10, 8);
                            swatch.Children.Add(new Border { Height = 32, Background = Color.TryParse(hex, out var col) ? new SolidColorBrush(col) : Brushes.Gray, CornerRadius = new CornerRadius(3), BorderBrush = Edge, BorderThickness = new Thickness(1) });
                            swatch.Children.Add(Label(hex + (name != "" ? " · " + name : ""), 12));
                            strip.Children.Add(swatch);
                        }
                        paper.Children.Add(strip); continue;
                    }
                    if (line.StartsWith("[[Firmas:") && line.EndsWith("]]"))
                    {
                        var signers = line[9..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", signers.Select(_ => "*"))), Margin = new Thickness(0, 18, 0, 8) };
                        for (int s = 0; s < signers.Length; s++)
                        {
                            var col = Column(4); col.Margin = new Thickness(12, 0);
                            col.Children.Add(new Border { Height = 1, Background = Edge });
                            var lbl = Label(signers[s], 13); lbl.HorizontalAlignment = HorizontalAlignment.Center;
                            col.Children.Add(lbl);
                            Grid.SetColumn(col, s); row.Children.Add(col);
                        }
                        paper.Children.Add(row); continue;
                    }
                    if(line.Trim().StartsWith('|') && line.Trim().EndsWith('|') && line.Trim().Length>2){
                        var cells=line.Trim().Trim('|').Split('|');
                        if(cells.All(c=>Regex.IsMatch(c.Trim(),@"^:?-+:?$")))continue;
                        var row=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",cells.Select(_=>"*")))};
                        for(int col=0;col<cells.Length;col++){
                            var cell=new Border{BorderThickness=new Thickness(0,0,0,1),BorderBrush=Accent,Padding=new Thickness(5),Child=Label(cells[col].Trim())};
                            Grid.SetColumn(cell,col);row.Children.Add(cell);
                        }
                        paper.Children.Add(row);continue;
                    }
                    var p = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = WritingSize, LineHeight = WritingSize*1.6, FontFamily=WritingFamily };
                    var quote=line.StartsWith("> ");
                    if(quote){p.Margin=new Thickness(20,2,14,2);p.FontStyle=FontStyle.Italic;}
                    foreach (var piece in Regex.Split(line.StartsWith("- ") ? "•  " + line[2..] : quote?line[2..]:line, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
                    {
                        if (piece.Length > 4 && piece.StartsWith("**") && piece.EndsWith("**")) p.Inlines!.Add(new Run(piece[2..^2]) { FontWeight = FontWeight.Bold });
                        else if (piece.Length > 2 && piece.StartsWith('*') && piece.EndsWith('*')) p.Inlines!.Add(new Run(piece[1..^1]) { FontStyle = FontStyle.Italic });
                        else p.Inlines!.Add(new Run(piece));
                    }
                    if(quote)paper.Children.Add(new Border{Child=p,BorderBrush=Accent,BorderThickness=new Thickness(2,0,0,0)});else paper.Children.Add(p);
                }
            }
            int pos = 0; var shown = new HashSet<int>();
            foreach (var row in Illustrations.Rows(section))
            {
                Prose(section.PlainText[pos..row[0].Start]);var strip=ImageRow(row.Count);paper.Children.Add(strip);
                for(int col=0;col<row.Count;col++){
                    var cell=Column(4);Grid.SetColumn(cell,col);strip.Children.Add(cell);
                    Illustration(row[col].Image,cell,row.Count>1);shown.Add(row[col].Image);
                }
                pos=row[^1].End;
            }
            Prose(section.PlainText[pos..]);
            for (int i = 0; i < section.Images.Count; i++) if (!shown.Contains(i)) Illustration(i,paper);
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
        root.Children.Add(new ScrollViewer { Content = paper }); page.Content = root;
    }
}

