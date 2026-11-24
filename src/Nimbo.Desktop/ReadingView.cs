using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nimbo.Core;
using System.Text.RegularExpressions;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    internal static readonly string[] SlideLayouts =
    [
        "Portada",
        "Ficha de concepto",
        "Tabla visual",
        "Texto e imagen",
        "Dos columnas",
        "Moodboard y paleta",
        "Moodboard y landmark",
        "Plano y leyenda",
        "Imagen protagonista",
        "Texto"
    ];

    internal static readonly string[] ConceptSlotOptions =
    [
        "Automática",
        "Plataforma",
        "Target & ESRB",
        "Referencia visual 1",
        "Referencia visual 2",
        "Referencia visual 3",
        "Referencia visual 4",
        "Gameplay 1",
        "Gameplay 2",
        "Gameplay 3",
        "Fondo de diapositiva"
    ];

    internal static (List<string> References, List<string> Keywords) ParseConceptLists(string plainText)
    {
        var refs = new List<string>();
        var keys = new List<string>();
        var lines = plainText.ReplaceLineEndings("\n").Split('\n');
        bool tableMode = false;
        int section = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || (line.StartsWith("[[") && line.EndsWith("]]"))) continue;
            if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (cells.All(c => Regex.IsMatch(c, @"^:?-+:?$"))) continue;
                if (!tableMode && cells.Length >= 2 && (cells[0].Contains("Referencia", StringComparison.OrdinalIgnoreCase) || cells[1].Contains("Key", StringComparison.OrdinalIgnoreCase) || cells[1].Contains("Palabra", StringComparison.OrdinalIgnoreCase)))
                {
                    tableMode = true;
                    continue;
                }
                tableMode = true;
                if (cells.Length >= 1 && cells[0] != "" && refs.Count < 6) refs.Add(Regex.Replace(cells[0], @"^([1-9][0-9]*[\.\)\-]\s*)", ""));
                if (cells.Length >= 2 && cells[1] != "" && keys.Count < 6) keys.Add(Regex.Replace(cells[1], @"^([1-9][0-9]*[\.\)\-]\s*)", ""));
                continue;
            }
            if (line.StartsWith('#'))
            {
                var h = line.TrimStart('#').Trim();
                if (h.Contains("Key", StringComparison.OrdinalIgnoreCase) || h.Contains("Palabra", StringComparison.OrdinalIgnoreCase)) section = 2;
                else if (h.Contains("Referencia", StringComparison.OrdinalIgnoreCase)) section = 1;
                continue;
            }
            var clean = Regex.Replace(line, @"^(?:-\s*|[1-9][0-9]*[\.\)]\s*)", "").Trim();
            if (clean == "") continue;
            if (section == 2 || (section == 0 && refs.Count >= 6))
            {
                if (keys.Count < 6) keys.Add(clean);
            }
            else if (refs.Count < 6) refs.Add(clean);
            else if (keys.Count < 6) keys.Add(clean);
        }
        return (refs, keys);
    }

    internal static Dictionary<string, int> MapConceptImages(Chapter slide)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unmapped = new List<int>();
        for (int i = 0; i < slide.Images.Count; i++)
        {
            var role = slide.ImageLayouts.GetValueOrDefault(i, "");
            if (role is "Plataforma" or "Target & ESRB" or "Referencia visual 1" or "Referencia visual 2" or "Referencia visual 3" or "Referencia visual 4" or "Gameplay 1" or "Gameplay 2" or "Gameplay 3" or "Fondo de diapositiva")
                map[role] = i;
            else
                unmapped.Add(i);
        }
        void Fill(string slot, int idx) { if (!map.ContainsKey(slot)) map[slot] = idx; }
        if (unmapped.Count >= 9)
        {
            Fill("Plataforma", unmapped[0]);
            Fill("Target & ESRB", unmapped[1]);
            Fill("Referencia visual 1", unmapped[2]);
            Fill("Referencia visual 2", unmapped[3]);
            Fill("Referencia visual 3", unmapped[4]);
            Fill("Referencia visual 4", unmapped[5]);
            Fill("Gameplay 1", unmapped[6]);
            Fill("Gameplay 2", unmapped[7]);
            Fill("Gameplay 3", unmapped[8]);
            if (unmapped.Count >= 10) Fill("Fondo de diapositiva", unmapped[9]);
        }
        else if (unmapped.Count >= 7)
        {
            Fill("Referencia visual 1", unmapped[0]);
            Fill("Referencia visual 2", unmapped[1]);
            Fill("Referencia visual 3", unmapped[2]);
            Fill("Referencia visual 4", unmapped[3]);
            Fill("Gameplay 1", unmapped[4]);
            Fill("Gameplay 2", unmapped[5]);
            Fill("Gameplay 3", unmapped[6]);
            if (unmapped.Count >= 8) Fill("Fondo de diapositiva", unmapped[7]);
        }
        else if (unmapped.Count >= 4)
        {
            Fill("Referencia visual 1", unmapped[0]);
            Fill("Referencia visual 2", unmapped[1]);
            Fill("Referencia visual 3", unmapped[2]);
            Fill("Referencia visual 4", unmapped[3]);
            for (int g = 4; g < unmapped.Count && g < 7; g++) Fill($"Gameplay {g - 3}", unmapped[g]);
        }
        else
        {
            for (int g = 0; g < unmapped.Count && g < 3; g++) Fill($"Gameplay {g + 1}", unmapped[g]);
        }
        return map;
    }

    Border BuildSlideCanvas(Work work, Chapter slide, int decodeWidth = 720)
    {
        const double W = 960, H = 540;
        var accentHex = Color.TryParse(work.CoverColor, out var parsed) && !work.CoverColor.Equals("#43676B", StringComparison.OrdinalIgnoreCase)
            ? parsed.ToString()
            : "#1E5B88";
        var goldBrush = Brush.Parse("#E6AA68");
        var darkNavy = Brush.Parse("#101B2A");
        var deepCard = Brush.Parse("#080D15");
        var ivoryInk = Brush.Parse("#F5F4F1");
        var softInk = Brush.Parse("#C4CDD7");
        var tealAccent = Brush.Parse("#006464");

        var root = new Grid { Width = W, Height = H, ClipToBounds = true, Background = darkNavy };

        Control? SlidePicture(int index, double boxW, double boxH, bool uniformToFill = false, string? bgHex = "#080D15")
        {
            if (index < 0 || index >= slide.Images.Count) return null;
            var key = slide.Images[index];
            if (string.IsNullOrEmpty(key)) return null;
            var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(key));
            if (Thumbnail(path, decodeWidth, boxH) is not { } img) return null;
            img.Width = double.NaN;
            img.Height = double.NaN;
            img.MaxWidth = boxW;
            img.MaxHeight = boxH;
            img.Stretch = uniformToFill ? Stretch.UniformToFill : Stretch.Uniform;
            img.HorizontalAlignment = HorizontalAlignment.Center;
            img.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = boxW,
                Height = boxH,
                Background = bgHex != null ? Brush.Parse(bgHex) : Brushes.Transparent,
                ClipToBounds = true,
                CornerRadius = new CornerRadius(2),
                Child = img
            };
        }

        void AddBackgroundArt(string? key, double opacity)
        {
            if (string.IsNullOrEmpty(key)) return;
            var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(key));
            if (Thumbnail(path, decodeWidth, H) is { } bg)
            {
                bg.Width = W; bg.Height = H; bg.Stretch = Stretch.UniformToFill; bg.Opacity = opacity;
                root.Children.Add(bg);
            }
        }

        var layout = SlideLayouts.Contains(slide.Layout) ? slide.Layout : (slide.Order == 0 && slide.Title.Equals("Portada", StringComparison.OrdinalIgnoreCase) ? "Portada" : "Texto e imagen");

        if (layout == "Portada")
        {
            root.Background = Brush.Parse("#243750");
            var coverKey = slide.Images.FirstOrDefault() ?? work.CoverImage;
            AddBackgroundArt(coverKey, 0.52);
            var canvas = new Canvas { Width = W, Height = H, ClipToBounds = true };
            var waveTopLeft = new Avalonia.Controls.Shapes.Path
            {
                Fill = Brush.Parse("#243750"),
                Data = Geometry.Parse("M 0,0 L 274,0 C 278,83 233,115 175,97 C 96,74 61,38 0,112 Z")
            };
            var goldTopLeft = new Avalonia.Controls.Shapes.Path
            {
                Stroke = goldBrush,
                StrokeThickness = 1.8,
                Data = Geometry.Parse("M 254,-6 C 269,76 226,104 172,86 C 95,60 59,36 -5,100")
            };
            var creamTopRight = new Avalonia.Controls.Shapes.Path
            {
                Fill = Brush.Parse("#E4DAD3"),
                Opacity = .9,
                Data = Geometry.Parse("M 701,0 L 960,0 L 960,158 C 865,157 897,99 806,78 C 749,66 733,38 701,0 Z")
            };
            var waveBotLeft = new Avalonia.Controls.Shapes.Path
            {
                Fill = Brush.Parse("#243750"),
                Data = Geometry.Parse("M 0,372 C 88,338 207,413 252,457 C 291,495 295,521 294,540 L 0,540 Z")
            };
            var goldBotLeft = new Avalonia.Controls.Shapes.Path
            {
                Stroke = goldBrush,
                StrokeThickness = 1.8,
                Data = Geometry.Parse("M -8,421 C 120,479 168,379 250,432 C 296,460 317,507 306,547")
            };
            var creamBotRight = new Avalonia.Controls.Shapes.Path
            {
                Fill = Brush.Parse("#E4DAD3"),
                Data = Geometry.Parse("M 727,540 C 711,491 746,449 813,455 C 866,463 907,434 960,441 L 960,540 Z")
            };
            canvas.Children.Add(waveTopLeft); canvas.Children.Add(goldTopLeft); canvas.Children.Add(creamTopRight);
            canvas.Children.Add(waveBotLeft); canvas.Children.Add(goldBotLeft); canvas.Children.Add(creamBotRight);
            root.Children.Add(canvas);

            var centerStack = Column(12);
            centerStack.VerticalAlignment = VerticalAlignment.Center;
            centerStack.HorizontalAlignment = HorizontalAlignment.Center;
            centerStack.MaxWidth = 760;
            var subject = slide.Detail != "" ? slide.Detail : string.Join(" · ", new[] { work.DocumentFacts.GetValueOrDefault("Institución", ""), work.DocumentFacts.GetValueOrDefault("Evento", "") }.Where(s => s != ""));
            if (subject != "")
            {
                centerStack.Children.Add(new TextBlock { Text = subject, FontSize = 20, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                centerStack.Children.Add(new Border { Width = 84, Height = 1.6, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
            }
            var act = slide.Summary != "" ? slide.Summary : work.Subtitle;
            if (act != "")
                centerStack.Children.Add(new TextBlock { Text = act, FontSize = 25, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            var mainTitle = (slide.Title == "" || slide.Title.Equals("Portada", StringComparison.OrdinalIgnoreCase)) ? work.Title : slide.Title;
            centerStack.Children.Add(new TextBlock { Text = mainTitle.ToUpperInvariant(), FontSize = 38, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            centerStack.Children.Add(new Border { Width = 175, Height = 1.4, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8) });
            if (work.Author != "")
            {
                centerStack.Children.Add(new TextBlock { Text = "Autor", FontSize = 12, Foreground = softInk, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                centerStack.Children.Add(new TextBlock { Text = work.Author, FontSize = 19, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            }
            root.Children.Add(centerStack);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout == "Ficha de concepto")
        {
            var slots = MapConceptImages(slide);
            var bgKey = slots.TryGetValue("Fondo de diapositiva", out var bgIdx) ? slide.Images[bgIdx] : work.CoverImage;
            AddBackgroundArt(bgKey, 0.17);
            var canvas = new Canvas { Width = W, Height = H };

            void Place(Control ctrl, double x, double y) { Canvas.SetLeft(ctrl, x); Canvas.SetTop(ctrl, y); canvas.Children.Add(ctrl); }

            Place(new TextBlock { Text = slide.Title, Width = 315, FontSize = 28, FontWeight = FontWeight.Bold, Foreground = ivoryInk, TextTrimming = TextTrimming.CharacterEllipsis }, 32, 24);
            if (slide.Detail != "")
                Place(new TextBlock { Text = "(" + slide.Detail.Trim('(', ')') + ")", Width = 315, FontSize = 14, Foreground = softInk }, 32, 68);
            var conceptBlock = new TextBlock { Width = 563, FontSize = 15.5, LineHeight = 20, Foreground = ivoryInk, TextWrapping = TextWrapping.Wrap, MaxLines = 3 };
            conceptBlock.Inlines!.Add(new Run("Concepto: ") { FontWeight = FontWeight.Bold });
            conceptBlock.Inlines.Add(new Run(slide.Summary != "" ? slide.Summary : "Describe la premisa y mecánicas principales en el campo Concepto."));
            Place(conceptBlock, 365, 26);
            Place(new Border { Width = 896, Height = 1.3, Background = goldBrush }, 32, 112);

            Place(new TextBlock { Text = "Plataforma", FontSize = 12, Foreground = ivoryInk }, 32, 126);
            Place(slots.TryGetValue("Plataforma", out var pIdx) && SlidePicture(pIdx, 78, 57, false, "#FFFFFF") is { } pPic
                ? pPic
                : new Border { Width = 78, Height = 57, Background = Brushes.White, CornerRadius = new CornerRadius(2), Child = new TextBlock { Text = "PC / Steam", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, 35, 150);

            Place(new TextBlock { Text = "Target & ESRB", FontSize = 11, Foreground = ivoryInk }, 32, 217);
            Place(slots.TryGetValue("Target & ESRB", out var tIdx) && SlidePicture(tIdx, 78, 57, false, "#FFFFFF") is { } tPic
                ? tPic
                : new Border { Width = 78, Height = 57, Background = Brushes.White, CornerRadius = new CornerRadius(2), Child = new TextBlock { Text = "+16 / T", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, 35, 240);

            Place(new TextBlock { Text = "Referencias visuales", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = ivoryInk }, 136, 126);
            (double rx, double ry, string slotName)[] refBoxes =
            [
                (136, 155, "Referencia visual 1"),
                (260, 155, "Referencia visual 2"),
                (136, 230, "Referencia visual 3"),
                (260, 230, "Referencia visual 4")
            ];
            foreach (var (rx, ry, slotName) in refBoxes)
            {
                Place(slots.TryGetValue(slotName, out var rIdx) && SlidePicture(rIdx, 116, 65.25) is { } rPic
                    ? rPic
                    : new Border { Width = 116, Height = 65.25, Background = deepCard, BorderBrush = Brush.Parse("#263547"), BorderThickness = new Thickness(1) }, rx, ry);
            }

            var (references, keywords) = ParseConceptLists(slide.PlainText);
            Place(new TextBlock { Text = "Referencias", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = ivoryInk }, 402, 126);
            Place(new TextBlock { Text = "Key Words", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = ivoryInk }, 762, 126);
            for (int i = 0; i < 6; i++)
            {
                double ly = 155 + i * 25.5;
                Place(new TextBlock { Text = (i + 1).ToString(), FontSize = 13, Foreground = goldBrush }, 402, ly);
                Place(new TextBlock { Text = i < references.Count ? references[i] : "—", Width = 310, FontSize = 14.5, Foreground = ivoryInk, TextTrimming = TextTrimming.CharacterEllipsis }, 427, ly - 1);
                Place(new TextBlock { Text = (i + 1).ToString(), FontSize = 13, Foreground = goldBrush }, 762, ly);
                Place(new TextBlock { Text = i < keywords.Count ? keywords[i] : "—", Width = 145, FontSize = 14.5, Foreground = ivoryInk, TextTrimming = TextTrimming.CharacterEllipsis }, 787, ly - 1);
            }

            double[] gx = [32, 336, 640];
            for (int g = 0; g < 3; g++)
            {
                var slotKey = $"Gameplay {g + 1}";
                Control gpBox = slots.TryGetValue(slotKey, out var gIdx) && SlidePicture(gIdx, 288, 188) is { } gPic
                    ? gPic
                    : new Border { Width = 288, Height = 188, Background = deepCard, BorderBrush = Brush.Parse("#263547"), BorderThickness = new Thickness(1) };
                Place(gpBox, gx[g], 316);
                var capText = slots.TryGetValue(slotKey, out var cIdx) && slide.ImageCaptions.TryGetValue(cIdx, out var customCap) && customCap != ""
                    ? (customCap.StartsWith("Gameplay", StringComparison.OrdinalIgnoreCase) ? customCap : "Gameplay: " + customCap)
                    : "Gameplay: " + (g < keywords.Count ? keywords[g] : $"Escena {g + 1}");
                Place(new TextBlock { Text = capText, Width = 288, FontSize = 13.5, Foreground = ivoryInk, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, gx[g], 511);
            }

            root.Children.Add(canvas);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        bool lightSlide = layout == "Tabla visual";
        root.Background = lightSlide ? Brush.Parse("#F7FAFA") : darkNavy;
        if (!lightSlide) AddBackgroundArt(work.CoverImage, 0.12);
        var fg = lightSlide ? Brush.Parse("#183737") : ivoryInk;
        var subFg = lightSlide ? tealAccent : softInk;

        var slideGrid = new DockPanel { Margin = new Thickness(32, 22, 32, 20) };
        var header = Column(3);
        header.Children.Add(new TextBlock { Text = slide.Title, FontSize = 27, FontWeight = FontWeight.Bold, Foreground = lightSlide ? tealAccent : ivoryInk, TextTrimming = TextTrimming.CharacterEllipsis });
        if (slide.Detail != "")
            header.Children.Add(new TextBlock { Text = slide.Detail, FontSize = 15, Foreground = subFg });
        header.Children.Add(new Border { Height = 1.4, Background = lightSlide ? tealAccent : goldBrush, Margin = new Thickness(0, 6, 0, 12) });
        DockPanel.SetDock(header, Dock.Top);
        slideGrid.Children.Add(header);

        var usedInTables = new HashSet<int>();

        Control RenderRichBlocks(string source, double maxTableHeight)
        {
            var stack = Column(8);
            var lines = source.ReplaceLineEndings("\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || Illustrations.Marker().IsMatch(line) && !line.StartsWith('|')) continue;
                if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
                {
                    var rows = new List<string[]>();
                    do
                    {
                        var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                        if (!cells.All(c => Regex.IsMatch(c, @"^:?-+:?$"))) rows.Add(cells);
                        i++;
                    } while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                    i--;
                    if (rows.Count == 0) continue;
                    int cols = rows.Max(r => r.Length);
                    bool firstColImage = rows.Skip(1).Any(r => r.Length > 0 && Illustrations.Marker().IsMatch(r[0]));
                    var colDefs = firstColImage && cols > 1
                        ? "112," + string.Join(",", Enumerable.Repeat("*", cols - 1))
                        : string.Join(",", Enumerable.Repeat("*", cols));
                    var tbl = new StackPanel { Spacing = 0 };
                    double rowFontSize = rows.Count >= 7 ? 12 : rows.Count >= 5 ? 13 : 14;
                    double imgRowH = Math.Clamp((maxTableHeight - 36) / Math.Max(1, rows.Count - 1) - 8, 28, 52);
                    for (int r = 0; r < rows.Count; r++)
                    {
                        bool isHeader = r == 0 && rows.Count > 1;
                        var rowGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(colDefs), Background = isHeader ? (lightSlide ? tealAccent : Brush.Parse("#1F354D")) : (r % 2 == 1 ? (lightSlide ? Brush.Parse("#EAF2F2") : Brush.Parse("#162538")) : (lightSlide ? Brushes.White : Brush.Parse("#121F30"))) };
                        for (int c = 0; c < cols; c++)
                        {
                            var rawCell = c < rows[r].Length ? rows[r][c] : "";
                            Control cellChild;
                            var imgMatch = Illustrations.Marker().Match(rawCell);
                            if (!isHeader && imgMatch.Success && int.TryParse(imgMatch.Groups[1].Value, out var imgNum) && imgNum <= slide.Images.Count)
                            {
                                usedInTables.Add(imgNum - 1);
                                cellChild = SlidePicture(imgNum - 1, 96, imgRowH, false, null) ?? new TextBlock { Text = rawCell };
                            }
                            else
                            {
                                cellChild = new TextBlock
                                {
                                    Text = rawCell.Replace("**", "").Replace("*", ""),
                                    FontSize = isHeader ? rowFontSize + .5 : rowFontSize,
                                    FontWeight = isHeader || (c == 0 || (firstColImage && c == 1)) ? FontWeight.Bold : FontWeight.Normal,
                                    Foreground = isHeader ? Brushes.White : fg,
                                    TextWrapping = TextWrapping.Wrap,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                            }
                            var cellBorder = new Border
                            {
                                BorderBrush = lightSlide ? Brush.Parse("#456565") : Brush.Parse("#2B4058"),
                                BorderThickness = new Thickness(.7),
                                Padding = new Thickness(8, isHeader ? 6 : 5),
                                Child = cellChild
                            };
                            Grid.SetColumn(cellBorder, c);
                            rowGrid.Children.Add(cellBorder);
                        }
                        tbl.Children.Add(rowGrid);
                    }
                    stack.Children.Add(tbl);
                    continue;
                }
                if (line.StartsWith("[[Paleta:") && line.EndsWith("]]"))
                {
                    var items = line[9..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var pGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", items.Select(_ => "*"))), ColumnSpacing = 8, Margin = new Thickness(0, 6) };
                    for (int p = 0; p < items.Length; p++)
                    {
                        var m = Regex.Match(items[p], @"^#?([0-9A-Fa-f]{6})(?:\s+(.*))?$");
                        var hex = "#" + (m.Success ? m.Groups[1].Value.ToUpperInvariant() : "666666");
                        var lbl = m.Success ? m.Groups[2].Value.Trim() : items[p];
                        var sw = Column(3);
                        sw.Children.Add(new Border { Height = 36, Background = Color.TryParse(hex, out var col) ? new SolidColorBrush(col) : Brushes.Gray, CornerRadius = new CornerRadius(3), BorderBrush = Brush.Parse("#44FFFFFF"), BorderThickness = new Thickness(1) });
                        sw.Children.Add(new TextBlock { Text = hex, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = goldBrush, TextAlignment = TextAlignment.Center });
                        if (lbl != "") sw.Children.Add(new TextBlock { Text = lbl, FontSize = 11, Foreground = softInk, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                        Grid.SetColumn(sw, p); pGrid.Children.Add(sw);
                    }
                    stack.Children.Add(pGrid);
                    continue;
                }
                if (line.StartsWith("### ") || line.StartsWith("## "))
                {
                    var hText = line.TrimStart('#').Trim();
                    stack.Children.Add(new TextBlock { Text = hText, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = lightSlide ? tealAccent : goldBrush, Margin = new Thickness(0, 4, 0, 2) });
                    continue;
                }
                var tb = new TextBlock { FontSize = 15, LineHeight = 21, Foreground = fg, TextWrapping = TextWrapping.Wrap };
                var contentLine = line;
                if (line.StartsWith("- "))
                {
                    tb.Inlines!.Add(new Run("▪  ") { Foreground = lightSlide ? tealAccent : goldBrush, FontWeight = FontWeight.Bold });
                    contentLine = line[2..];
                }
                else if (Regex.Match(line, @"^([1-9][0-9]*)\.\s+(.*)$") is { Success: true } nm)
                {
                    tb.Inlines!.Add(new Run(nm.Groups[1].Value + ".  ") { Foreground = lightSlide ? tealAccent : goldBrush, FontWeight = FontWeight.Bold });
                    contentLine = nm.Groups[2].Value;
                }
                foreach (var piece in Regex.Split(contentLine, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
                {
                    if (piece.Length > 4 && piece.StartsWith("**") && piece.EndsWith("**")) tb.Inlines!.Add(new Run(piece[2..^2]) { FontWeight = FontWeight.Bold });
                    else if (piece.Length > 2 && piece.StartsWith('*') && piece.EndsWith('*')) tb.Inlines!.Add(new Run(piece[1..^1]) { FontStyle = FontStyle.Italic });
                    else tb.Inlines!.Add(new Run(piece));
                }
                stack.Children.Add(tb);
            }
            return stack;
        }

        var richBlocks = RenderRichBlocks(slide.PlainText, 380);
        var remainingImages = Enumerable.Range(0, slide.Images.Count).Where(i => !usedInTables.Contains(i)).ToList();

        if (layout == "Imagen protagonista" && remainingImages.Count > 0)
        {
            var stage = Column(8);
            if (slide.PlainText.Trim() != "") stage.Children.Add(richBlocks);
            int idx = remainingImages[0];
            if (SlidePicture(idx, 888, 355) is { } heroPic)
                stage.Children.Add(heroPic);
            var cap = slide.ImageCaptions.GetValueOrDefault(idx, slide.Summary);
            if (cap != "")
                stage.Children.Add(new TextBlock { Text = cap, FontSize = 14, FontStyle = FontStyle.Italic, Foreground = softInk, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            slideGrid.Children.Add(stage);
        }
        else if (layout == "Tabla visual" && remainingImages.Count == 0)
        {
            var body = Column(8);
            body.Children.Add(richBlocks);
            if (slide.Summary != "")
                body.Children.Add(new TextBlock { Text = slide.Summary, FontSize = 13.5, FontStyle = FontStyle.Italic, Foreground = subFg });
            slideGrid.Children.Add(body);
        }
        else if (layout is "Moodboard y paleta" or "Moodboard y landmark")
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("0.92*,1.08*"), ColumnSpacing = 24 };
            var leftCol = Column(10);
            leftCol.Children.Add(richBlocks);
            if (slide.Summary != "")
                leftCol.Children.Add(new Border { Background = Brush.Parse("#18293D"), BorderBrush = goldBrush, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 8), Child = new TextBlock { Text = slide.Summary, FontSize = 13.5, Foreground = softInk, TextWrapping = TextWrapping.Wrap } });
            cols.Children.Add(leftCol);

            var rightCol = Column(8);
            int count = Math.Min(6, remainingImages.Count);
            int gridCols = count is 3 or 5 or 6 ? 3 : 2;
            int gridRows = Math.Max(1, (int)Math.Ceiling(count / (double)gridCols));
            double cellW = gridCols == 3 ? 150 : 228;
            double cellH = gridRows > 1 ? 148 : 290;
            var mosaic = new WrapPanel { ItemWidth = cellW + 8 };
            foreach (var idx in remainingImages.Take(6))
            {
                var card = Column(3); card.Margin = new Thickness(0, 0, 8, 8);
                if (SlidePicture(idx, cellW, cellH) is { } pic) card.Children.Add(pic);
                if (slide.ImageCaptions.TryGetValue(idx, out var cap) && cap != "")
                    card.Children.Add(new TextBlock { Text = cap, Width = cellW, FontSize = 11.5, Foreground = softInk, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                mosaic.Children.Add(card);
            }
            rightCol.Children.Add(mosaic);
            Grid.SetColumn(rightCol, 1); cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else if (layout == "Plano y leyenda")
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("1.18*,0.82*"), ColumnSpacing = 24 };
            var leftCol = Column(6);
            if (remainingImages.Count > 0 && SlidePicture(remainingImages[0], 500, 340, false, "#FFFFFF") is { } mapPic)
            {
                leftCol.Children.Add(mapPic);
                if (slide.ImageCaptions.TryGetValue(remainingImages[0], out var cap) && cap != "")
                    leftCol.Children.Add(new TextBlock { Text = cap, FontSize = 12.5, FontStyle = FontStyle.Italic, Foreground = softInk, TextAlignment = TextAlignment.Center });
            }
            cols.Children.Add(leftCol);

            var rightCol = Column(10);
            rightCol.Children.Add(richBlocks);
            if (slide.Summary != "")
                rightCol.Children.Add(new Border { Background = Brush.Parse("#18293D"), BorderBrush = goldBrush, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 8), Child = new TextBlock { Text = slide.Summary, FontSize = 13.5, Foreground = softInk, TextWrapping = TextWrapping.Wrap } });
            Grid.SetColumn(rightCol, 1); cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else if (remainingImages.Count > 0 || (layout == "Dos columnas" && slide.Summary != ""))
        {
            bool hasTable = slide.PlainText.Contains('|');
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions(hasTable ? "1.32*,0.88*" : "1*,1*"), ColumnSpacing = 24 };
            cols.Children.Add(richBlocks);
            var rightCol = Column(8);
            foreach (var idx in remainingImages.Take(2))
            {
                double picH = remainingImages.Count > 1 ? 150 : 275;
                if (SlidePicture(idx, hasTable ? 345 : 420, picH) is { } pic) rightCol.Children.Add(pic);
                if (slide.ImageCaptions.TryGetValue(idx, out var cap) && cap != "")
                    rightCol.Children.Add(new TextBlock { Text = cap, FontSize = 12.5, FontStyle = FontStyle.Italic, Foreground = subFg, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            }
            if (slide.Summary != "")
                rightCol.Children.Add(new Border { Background = lightSlide ? Brush.Parse("#EAF2F2") : Brush.Parse("#18293D"), BorderBrush = lightSlide ? tealAccent : goldBrush, BorderThickness = new Thickness(2.5, 0, 0, 0), Padding = new Thickness(12, 9), Child = new TextBlock { Text = slide.Summary, FontSize = 13.5, Foreground = fg, TextWrapping = TextWrapping.Wrap } });
            Grid.SetColumn(rightCol, 1); cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else
        {
            var single = Column(10);
            single.Children.Add(richBlocks);
            if (slide.Summary != "")
                single.Children.Add(new Border { Background = Brush.Parse("#18293D"), BorderBrush = goldBrush, BorderThickness = new Thickness(2.5, 0, 0, 0), Padding = new Thickness(12, 9), Child = new TextBlock { Text = slide.Summary, FontSize = 14, Foreground = softInk, TextWrapping = TextWrapping.Wrap } });
            slideGrid.Children.Add(single);
        }

        root.Children.Add(slideGrid);
        return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
    }

    void ReadPresentation(Work work, List<Chapter> sections, Chapter? selected)
    {
        var section = selected ?? sections.FirstOrDefault();
        var root = new DockPanel();
        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        top.Children.Add(Action("‹ Editar diapositiva", () => { chapter = section; Editor(); }));
        top.Children.Add(Action("Vista previa PDF", async () => await Preview()));
        top.Children.Add(QuietAction("Exportar PDF…", async () => await Export(true)));

        if (section != null)
        {
            int idx = sections.IndexOf(section);
            var prev = Action("‹ Anterior", () => { if (idx > 0) ReadSection(sections[idx - 1]); });
            prev.IsEnabled = idx > 0;
            var next = Action("Siguiente ›", () => { if (idx + 1 < sections.Count) ReadSection(sections[idx + 1]); });
            next.IsEnabled = idx + 1 < sections.Count;
            top.Children.Add(prev);
            var picker = new ComboBox { ItemsSource = sections, SelectedItem = section, MaxWidth = 300 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is Chapter target && target != section) ReadSection(target); };
            top.Children.Add(picker);
            top.Children.Add(next);
            top.Children.Add(Kicker($"  Diapositiva {idx + 1} de {sections.Count}  ·  {(SlideLayouts.Contains(section.Layout) ? section.Layout : "Texto e imagen")}"));
        }
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        if (section != null)
        {
            var filmstrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(4, 10, 4, 4) };
            for (int i = 0; i < sections.Count; i++)
            {
                var item = sections[i];
                bool isCurrent = item == section;
                var miniCard = Column(4);
                var thumbBox = new Viewbox
                {
                    Width = 176,
                    Height = 99,
                    Stretch = Stretch.Uniform,
                    Child = BuildSlideCanvas(work, item, 240)
                };
                var frame = new Border
                {
                    Child = thumbBox,
                    BorderBrush = isCurrent ? Accent : Edge,
                    BorderThickness = new Thickness(isCurrent ? 2.5 : 1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(1)
                };
                miniCard.Children.Add(frame);
                var lbl = Label($"{i + 1:D2} · {item.Title}", 11.5);
                lbl.MaxWidth = 176; lbl.TextWrapping = TextWrapping.NoWrap; lbl.TextTrimming = TextTrimming.CharacterEllipsis;
                if (isCurrent) { lbl.Foreground = Accent; lbl.FontWeight = FontWeight.SemiBold; }
                miniCard.Children.Add(lbl);
                var btn = new Button
                {
                    Content = miniCard,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                btn.Click += (_, _) => { chapter = item; ReadSection(item); };
                filmstrip.Children.Add(btn);
            }
            var stripBar = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = filmstrip
            };
            DockPanel.SetDock(stripBar, Dock.Bottom);
            root.Children.Add(stripBar);

            var stageBox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                MaxWidth = 1240,
                Margin = new Thickness(8, 4, 8, 8),
                Child = BuildSlideCanvas(work, section, 960)
            };
            root.Children.Add(stageBox);
        }

        page.Content = root;
    }

    void ReadSection(Chapter? selected)
    {
        Navigate("Lectura");var work = current!;
        var sections = WorldModel.Ordered(work).ToList();
        if (work.Type == "Presentación")
        {
            ReadPresentation(work, sections, selected);
            return;
        }
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
                var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(section.Images[index]));
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

