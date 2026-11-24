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
        "Tres columnas",
        "Cuadrícula 2x2",
        "Métricas e indicadores (KPIs)",
        "Línea de tiempo / Roadmap",
        "Comparativa (Antes / Después)",
        "Moodboard y paleta",
        "Moodboard y landmark",
        "Plano y leyenda",
        "Galería triple",
        "Imagen protagonista",
        "Cita o conclusión destacada",
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

    internal static List<(string Title, string Subtitle, List<string> Lines)> ParseStructuredCards(string plainText)
    {
        var cards = new List<(string Title, string Subtitle, List<string> Lines)>();
        var lines = plainText.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || (line.StartsWith("[[") && line.EndsWith("]]"))) continue;
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
                int startRow = rows.Count > 1 && rows[0].Any(c => Regex.IsMatch(c, @"^(Indicador|Métrica|KPI|Fase|Etapa|Pilar|Columna|Cuadrante|Elemento|Título|Opción|Criterio|Antes)\b", RegexOptions.IgnoreCase)) ? 1 : 0;
                for (int r = startRow; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace)) continue;
                    string title = row[0].Replace("**", "").Trim();
                    string sub = row.Length > 1 ? row[1].Replace("**", "").Trim() : "";
                    var body = row.Skip(2).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Replace("**", "").Trim()).ToList();
                    cards.Add((title, sub, body));
                }
                continue;
            }
            if (line.StartsWith("### ") || line.StartsWith("## "))
            {
                var rawHead = line.TrimStart('#').Trim();
                var parts = rawHead.Split('|', 2, StringSplitOptions.TrimEntries);
                cards.Add((parts[0], parts.Length > 1 ? parts[1] : "", []));
                continue;
            }
            var cleaned = Regex.Replace(line, @"^(?:-\s*|[1-9][0-9]*[\.\)]\s*)", "").Trim();
            if (cards.Count > 0 && (line.StartsWith("- ") || !line.Contains(':')))
            {
                if (cards[^1].Subtitle == "" && cards[^1].Lines.Count == 0 && cleaned.Length <= 28)
                {
                    var last = cards[^1];
                    cards[^1] = (last.Title, cleaned.Replace("**", ""), last.Lines);
                }
                else
                {
                    cards[^1].Lines.Add(cleaned.Replace("**", ""));
                }
            }
            else if (cleaned.Contains(':'))
            {
                var colon = cleaned.Split(':', 2, StringSplitOptions.TrimEntries);
                var rhs = colon[1].Split('|', StringSplitOptions.TrimEntries);
                cards.Add((colon[0].Replace("**", ""), rhs[0].Replace("**", ""), rhs.Skip(1).ToList()));
            }
            else
            {
                cards.Add((cleaned.Replace("**", ""), "", []));
            }
        }
        return cards;
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
        var navyBrush = Brush.Parse("#243750");
        var creamBrush = Brush.Parse("#E4DAD3");
        var cardBrush = Brush.Parse("#EFECE7");
        var goldBrush = Brush.Parse("#E6AA68");
        var tealBrush = Brush.Parse("#2C6467");
        var rowAltBrush = Brush.Parse("#EAF2F2");
        var inkBrush = Brush.Parse("#1E2328");
        var mutedBrush = Brush.Parse("#526068");
        var frameBorderBrush = Brush.Parse("#C5B9AF");

        var root = new Grid { Width = W, Height = H, ClipToBounds = true, Background = creamBrush };

        Control? SlidePicture(int index, double boxW, double boxH, bool uniformToFill = false, string? bgHex = "#FFFFFF", double borderThickness = 0.8)
        {
            if (index < 0 || index >= slide.Images.Count) return null;
            var key = slide.Images[index];
            if (string.IsNullOrEmpty(key)) return null;
            var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(key));
            if (Thumbnail(path, decodeWidth, boxH) is not { } img) return null;
            img.Width = double.NaN;
            img.Height = double.NaN;
            img.MaxWidth = Math.Max(16, boxW - 6);
            img.MaxHeight = Math.Max(16, boxH - 6);
            img.Stretch = uniformToFill ? Stretch.UniformToFill : Stretch.Uniform;
            img.HorizontalAlignment = HorizontalAlignment.Center;
            img.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = boxW,
                Height = boxH,
                Background = bgHex != null ? Brush.Parse(bgHex) : Brushes.Transparent,
                BorderBrush = borderThickness > 0 ? frameBorderBrush : null,
                BorderThickness = new Thickness(borderThickness),
                ClipToBounds = true,
                CornerRadius = new CornerRadius(4),
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

        void AddTopBottomBars()
        {
            root.Children.Add(new Border { Height = 6, Background = navyBrush, VerticalAlignment = VerticalAlignment.Top });
            root.Children.Add(new Border { Height = 4, Background = goldBrush, VerticalAlignment = VerticalAlignment.Bottom });
        }

        var layout = SlideLayouts.Contains(slide.Layout) ? slide.Layout : (slide.Order == 0 && slide.Title.Equals("Portada", StringComparison.OrdinalIgnoreCase) ? "Portada" : "Texto e imagen");

        if (layout == "Portada")
        {
            root.Background = navyBrush;
            var coverKey = slide.Images.FirstOrDefault() ?? work.CoverImage;
            AddBackgroundArt(coverKey, 0.36);
            var canvas = new Canvas { Width = W, Height = H, ClipToBounds = true };
            void Wave(string data, IBrush? fill, IBrush? stroke = null, double strokeW = 1.6)
            {
                canvas.Children.Add(new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse(data),
                    Fill = fill,
                    Stroke = stroke,
                    StrokeThickness = strokeW
                });
            }
            Wave("M 0,0 L 365,0 C 298,86 202,94 134,151 C 91,187 58,245 0,270 Z", navyBrush);
            Wave("M 0,0 L 274,0 C 226,63 144,69 94,117 C 61,148 40,189 0,210 Z", creamBrush);
            Wave("M 0,0 L 187,0 C 158,42 101,46 63,81 C 38,104 23,133 0,151 Z", navyBrush);

            Wave("M 960,0 L 595,0 C 662,86 758,94 826,151 C 869,187 902,245 960,270 Z", navyBrush);
            Wave("M 960,0 L 686,0 C 734,63 816,69 866,117 C 899,148 920,189 960,210 Z", creamBrush);
            Wave("M 960,0 L 773,0 C 802,42 859,46 897,81 C 922,104 937,133 960,151 Z", navyBrush);

            Wave("M 0,540 L 0,299 C 62,324 106,385 168,421 C 240,464 346,464 413,540 Z", navyBrush);
            Wave("M 0,540 L 0,360 C 48,382 79,428 127,457 C 182,490 259,490 312,540 Z", creamBrush);
            Wave("M 0,540 L 0,414 C 34,428 56,462 91,484 C 132,508 187,508 226,540 Z", navyBrush);

            Wave("M 960,540 L 960,299 C 898,324 854,385 792,421 C 720,464 614,464 547,540 Z", navyBrush);
            Wave("M 960,540 L 960,360 C 912,382 881,428 833,457 C 778,490 701,490 648,540 Z", creamBrush);
            Wave("M 960,540 L 960,414 C 926,428 904,462 869,484 C 828,508 773,508 734,540 Z", navyBrush);

            Wave("M 312,0 C 254,76 168,81 110,134 C 75,167 46,215 0,238", null, goldBrush);
            Wave("M 648,0 C 706,76 792,81 850,134 C 885,167 914,215 960,238", null, goldBrush);
            Wave("M 0,331 C 56,354 92,407 146,441 C 209,479 300,479 360,540", null, goldBrush);
            Wave("M 960,331 C 904,354 868,407 814,441 C 751,479 660,479 600,540", null, goldBrush);
            root.Children.Add(canvas);

            var centerStack = Column(10);
            centerStack.VerticalAlignment = VerticalAlignment.Center;
            centerStack.HorizontalAlignment = HorizontalAlignment.Center;
            centerStack.MaxWidth = 640;
            var mainTitle = (slide.Title == "" || slide.Title.Equals("Portada", StringComparison.OrdinalIgnoreCase)) ? work.Title : slide.Title;
            centerStack.Children.Add(new TextBlock { Text = mainTitle, FontSize = 34, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            var sub = slide.Detail != "" ? slide.Detail : (slide.Summary != "" ? slide.Summary : work.Subtitle);
            if (sub != "")
                centerStack.Children.Add(new TextBlock { Text = sub, FontSize = 16, Foreground = creamBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            centerStack.Children.Add(new Border { Width = 220, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6) });
            if (work.Author != "")
                centerStack.Children.Add(new TextBlock { Text = work.Author, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            var metaLine = string.Join(" · ", new[]
            {
                slide.PlainText.Replace('\n', ' ').Trim(),
                work.DocumentFacts.GetValueOrDefault("Institución", ""),
                work.DocumentFacts.GetValueOrDefault("Evento", ""),
                work.DocumentFacts.GetValueOrDefault("Fecha", "")
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (metaLine != "")
                centerStack.Children.Add(new TextBlock { Text = metaLine, FontSize = 12.5, Foreground = creamBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            root.Children.Add(centerStack);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout == "Ficha de concepto")
        {
            var slots = MapConceptImages(slide);
            var bgKey = slots.TryGetValue("Fondo de diapositiva", out var bgIdx) ? slide.Images[bgIdx] : null;
            AddBackgroundArt(bgKey, 0.17);
            AddTopBottomBars();
            var canvas = new Canvas { Width = W, Height = H };

            void Place(Control ctrl, double x, double y) { Canvas.SetLeft(ctrl, x); Canvas.SetTop(ctrl, y); canvas.Children.Add(ctrl); }

            Place(new TextBlock { Text = slide.Title, Width = 320, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextTrimming = TextTrimming.CharacterEllipsis }, 32, 24);
            if (slide.Detail != "")
                Place(new TextBlock { Text = "(" + slide.Detail.Trim('(', ')') + ")", Width = 320, FontSize = 14, FontWeight = FontWeight.Bold, Foreground = tealBrush }, 32, 70);
            var conceptBlock = new TextBlock { Width = 563, FontSize = 13.5, LineHeight = 18.5, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap, MaxLines = 4 };
            conceptBlock.Inlines!.Add(new Run("Concepto: ") { FontWeight = FontWeight.Bold, Foreground = navyBrush });
            conceptBlock.Inlines.Add(new Run(slide.Summary != "" ? slide.Summary : "Describe la premisa y mecánicas principales en el campo Concepto."));
            Place(conceptBlock, 365, 26);
            Place(new Border { Width = 896, Height = 1.6, Background = goldBrush }, 32, 112);

            Place(new TextBlock { Text = "Plataforma", Width = 78, TextAlignment = TextAlignment.Center, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = navyBrush }, 35, 128);
            Place(slots.TryGetValue("Plataforma", out var pIdx) && SlidePicture(pIdx, 78, 57, false, "#FFFFFF", 0.8) is { } pPic
                ? pPic
                : new Border { Width = 78, Height = 57, Background = Brushes.White, BorderBrush = goldBrush, BorderThickness = new Thickness(0.8), CornerRadius = new CornerRadius(4), Child = new TextBlock { Text = "PC / Steam", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = navyBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, 35, 148);

            Place(new TextBlock { Text = "Target & ESRB", Width = 84, TextAlignment = TextAlignment.Center, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = navyBrush }, 32, 218);
            Place(slots.TryGetValue("Target & ESRB", out var tIdx) && SlidePicture(tIdx, 78, 57, false, "#FFFFFF", 0.8) is { } tPic
                ? tPic
                : new Border { Width = 78, Height = 57, Background = Brushes.White, BorderBrush = goldBrush, BorderThickness = new Thickness(0.8), CornerRadius = new CornerRadius(4), Child = new TextBlock { Text = "+16 / T", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = navyBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, 35, 238);

            Place(new TextBlock { Text = "Referencias visuales", Width = 240, TextAlignment = TextAlignment.Center, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = navyBrush }, 136, 128);
            (double rx, double ry, string slotName)[] refBoxes =
            [
                (136, 152, "Referencia visual 1"),
                (260, 152, "Referencia visual 2"),
                (136, 228, "Referencia visual 3"),
                (260, 228, "Referencia visual 4")
            ];
            foreach (var (rx, ry, slotName) in refBoxes)
            {
                Place(slots.TryGetValue(slotName, out var rIdx) && SlidePicture(rIdx, 116, 66, false, "#EFECE7", 0.7) is { } rPic
                    ? rPic
                    : new Border { Width = 116, Height = 66, Background = cardBrush, BorderBrush = frameBorderBrush, BorderThickness = new Thickness(0.8), CornerRadius = new CornerRadius(3) }, rx, ry);
            }

            var (references, keywords) = ParseConceptLists(slide.PlainText);
            Place(new TextBlock { Text = "Referencias:", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = navyBrush }, 402, 128);
            Place(new TextBlock { Text = "Key Words:", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = navyBrush }, 758, 128);
            for (int i = 0; i < 6; i++)
            {
                double ly = 152 + i * 24.5;
                Place(new TextBlock { Text = (i + 1) + ".", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = tealBrush }, 402, ly);
                Place(new TextBlock { Text = i < references.Count ? references[i] : "—", Width = 320, FontSize = 13, Foreground = inkBrush, TextTrimming = TextTrimming.CharacterEllipsis }, 424, ly);
                Place(new TextBlock { Text = (i + 1) + ".", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = goldBrush }, 758, ly);
                Place(new TextBlock { Text = i < keywords.Count ? keywords[i] : "—", Width = 148, FontSize = 13, Foreground = inkBrush, TextTrimming = TextTrimming.CharacterEllipsis }, 778, ly);
            }

            double[] gx = [32, 336, 640];
            for (int g = 0; g < 3; g++)
            {
                var slotKey = $"Gameplay {g + 1}";
                Control gpBox = slots.TryGetValue(slotKey, out var gIdx) && SlidePicture(gIdx, 288, 188, false, "#243750", 1.0) is { } gPic
                    ? gPic
                    : new Border { Width = 288, Height = 188, Background = navyBrush, BorderBrush = goldBrush, BorderThickness = new Thickness(1) };
                Place(gpBox, gx[g], 314);
                var capText = slots.TryGetValue(slotKey, out var cIdx) && slide.ImageCaptions.TryGetValue(cIdx, out var customCap) && customCap != ""
                    ? customCap
                    : (g < keywords.Count ? keywords[g] : $"Gameplay {g + 1}");
                Place(new TextBlock { Text = capText, Width = 288, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, gx[g], 508);
            }

            root.Children.Add(canvas);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        AddTopBottomBars();
        var slideGrid = new DockPanel { Margin = new Thickness(38, 22, 38, 18) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
        header.Children.Add(new TextBlock { Text = slide.Title, FontSize = 25, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Bottom });
        if (slide.Detail != "")
        {
            var detailTb = new TextBlock { Text = slide.Detail, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = tealBrush, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 0, 0, 2) };
            Grid.SetColumn(detailTb, 1);
            header.Children.Add(detailTb);
        }
        var headerStack = Column(6);
        headerStack.Children.Add(header);
        headerStack.Children.Add(new Border { Height = 1.6, Background = goldBrush, Margin = new Thickness(0, 2, 0, 8) });
        DockPanel.SetDock(headerStack, Dock.Top);
        slideGrid.Children.Add(headerStack);

        var usedInTables = new HashSet<int>();

        Control RenderRichBlocks(string source, double maxTableHeight)
        {
            var stack = Column(6);
            var lines = source.ReplaceLineEndings("\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || (Illustrations.Marker().IsMatch(line) && !line.StartsWith('|'))) continue;
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
                        ? "104," + string.Join(",", Enumerable.Repeat("*", cols - 1))
                        : string.Join(",", Enumerable.Repeat("*", cols));
                    var tbl = new StackPanel { Spacing = 0 };
                    double rowFontSize = rows.Count >= 7 ? 11.5 : rows.Count >= 5 ? 12.5 : 13.5;
                    double imgRowH = Math.Clamp((maxTableHeight - 34) / Math.Max(1, rows.Count - 1) - 8, 26, 46);
                    for (int r = 0; r < rows.Count; r++)
                    {
                        bool isHeader = r == 0 && rows.Count > 1;
                        var rowGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions(colDefs),
                            Background = isHeader ? tealBrush : (r % 2 == 1 ? rowAltBrush : Brushes.White)
                        };
                        for (int c = 0; c < cols; c++)
                        {
                            var rawCell = c < rows[r].Length ? rows[r][c] : "";
                            Control cellChild;
                            var imgMatch = Illustrations.Marker().Match(rawCell);
                            if (!isHeader && imgMatch.Success && int.TryParse(imgMatch.Groups[1].Value, out var imgNum) && imgNum <= slide.Images.Count)
                            {
                                usedInTables.Add(imgNum - 1);
                                cellChild = SlidePicture(imgNum - 1, 88, imgRowH, false, null, 0) ?? new TextBlock { Text = rawCell };
                            }
                            else
                            {
                                cellChild = new TextBlock
                                {
                                    Text = rawCell.Replace("**", "").Replace("*", ""),
                                    FontSize = isHeader ? rowFontSize + .5 : rowFontSize,
                                    FontWeight = isHeader || (c == 0 || (firstColImage && c == 1)) ? FontWeight.Bold : FontWeight.Normal,
                                    Foreground = isHeader ? Brushes.White : (c == 0 || (firstColImage && c == 1) ? navyBrush : inkBrush),
                                    TextWrapping = TextWrapping.Wrap,
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                            }
                            var cellBorder = new Border
                            {
                                BorderBrush = Brush.Parse("#8AA4A6"),
                                BorderThickness = new Thickness(.6),
                                Padding = new Thickness(8, isHeader ? 6 : 4),
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
                        var sw = Column(2);
                        sw.Children.Add(new Border { Height = 32, Background = Color.TryParse(hex, out var col) ? new SolidColorBrush(col) : Brushes.Gray, CornerRadius = new CornerRadius(3), BorderBrush = Brush.Parse("#44243750"), BorderThickness = new Thickness(0.8) });
                        sw.Children.Add(new TextBlock { Text = hex, FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center });
                        if (lbl != "") sw.Children.Add(new TextBlock { Text = lbl, FontSize = 10.5, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                        Grid.SetColumn(sw, p); pGrid.Children.Add(sw);
                    }
                    stack.Children.Add(pGrid);
                    continue;
                }
                if (line.StartsWith("### ") || line.StartsWith("## "))
                {
                    var hText = line.TrimStart('#').Trim();
                    stack.Children.Add(new TextBlock { Text = hText, FontSize = 16.5, FontWeight = FontWeight.Bold, Foreground = tealBrush, Margin = new Thickness(0, 4, 0, 2) });
                    continue;
                }
                if (line.StartsWith("> "))
                {
                    stack.Children.Add(new Border
                    {
                        Background = cardBrush,
                        BorderBrush = goldBrush,
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        Padding = new Thickness(10, 6),
                        Child = new TextBlock { Text = line[2..].Replace("**", ""), FontSize = 13.5, FontStyle = FontStyle.Italic, Foreground = navyBrush, TextWrapping = TextWrapping.Wrap }
                    });
                    continue;
                }
                var tb = new TextBlock { FontSize = 14, LineHeight = 19.5, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap };
                var contentLine = line;
                if (line.StartsWith("- "))
                {
                    tb.Inlines!.Add(new Run("▪  ") { Foreground = goldBrush, FontWeight = FontWeight.Bold });
                    contentLine = line[2..];
                }
                else if (Regex.Match(line, @"^([1-9][0-9]*)\.\s+(.*)$") is { Success: true } nm)
                {
                    tb.Inlines!.Add(new Run(nm.Groups[1].Value + ".  ") { Foreground = tealBrush, FontWeight = FontWeight.Bold });
                    contentLine = nm.Groups[2].Value;
                }
                foreach (var piece in Regex.Split(contentLine, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
                {
                    if (piece.Length > 4 && piece.StartsWith("**") && piece.EndsWith("**")) tb.Inlines!.Add(new Run(piece[2..^2]) { FontWeight = FontWeight.Bold, Foreground = navyBrush });
                    else if (piece.Length > 2 && piece.StartsWith('*') && piece.EndsWith('*')) tb.Inlines!.Add(new Run(piece[1..^1]) { FontStyle = FontStyle.Italic });
                    else tb.Inlines!.Add(new Run(piece));
                }
                stack.Children.Add(tb);
            }
            return stack;
        }

        Border SummaryBanner(string labelText, string bodyText)
        {
            var tb = new TextBlock
            {
                FontSize = 13,
                LineHeight = 17.5,
                Foreground = navyBrush,
                TextWrapping = TextWrapping.Wrap
            };
            tb.Inlines!.Add(new Run(labelText + ": ") { FontWeight = FontWeight.Bold, Foreground = tealBrush });
            tb.Inlines.Add(new Run(bodyText));
            return new Border
            {
                Background = cardBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(14, 9),
                Child = tb
            };
        }

        if (layout == "Cita o conclusión destacada")
        {
            var quoteCard = new Border
            {
                Background = navyBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(38, 32),
                Margin = new Thickness(0, 12)
            };
            var qStack = Column(14);
            qStack.VerticalAlignment = VerticalAlignment.Center;
            var mainQuote = slide.PlainText.Replace("> ", "").Trim();
            if (mainQuote == "") mainQuote = slide.Summary != "" ? slide.Summary : "Escribe aquí la conclusión ejecutiva o cita central de la diapositiva.";
            qStack.Children.Add(new TextBlock { Text = "“" + mainQuote.Replace("**", "") + "”", FontSize = 23, FontStyle = FontStyle.Italic, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            qStack.Children.Add(new Border { Width = 160, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
            if (slide.Summary != "" && slide.PlainText.Trim() != "")
                qStack.Children.Add(new TextBlock { Text = slide.Summary, FontSize = 14.5, Foreground = creamBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            quoteCard.Child = qStack;
            slideGrid.Children.Add(quoteCard);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout is "Tres columnas" or "Cuadrícula 2x2" or "Métricas e indicadores (KPIs)" or "Línea de tiempo / Roadmap" or "Comparativa (Antes / Después)")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Síntesis", slide.Summary);
                bottomNote.Margin = new Thickness(0, 10, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            var cards = ParseStructuredCards(slide.PlainText);
            if (cards.Count == 0)
            {
                cards.Add(("Apartado 1", "Valor o enfoque", ["Añade bloques con ### Título o una tabla Markdown."]));
                cards.Add(("Apartado 2", "Valor o enfoque", ["Cada bloque o fila se convierte en una tarjeta visual."]));
            }

            if (layout == "Métricas e indicadores (KPIs)")
            {
                int kCount = Math.Clamp(cards.Count, 1, 4);
                var kGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", kCount))), ColumnSpacing = 14 };
                for (int i = 0; i < kCount; i++)
                {
                    var item = cards[i];
                    var bigValue = item.Subtitle != "" ? item.Subtitle : item.Title;
                    var label = item.Subtitle != "" ? item.Title : (item.Lines.FirstOrDefault() ?? $"Indicador {i + 1}");
                    var desc = item.Subtitle != "" ? string.Join(" · ", item.Lines) : string.Join(" · ", item.Lines.Skip(1));
                    var cardStack = Column(8);
                    cardStack.VerticalAlignment = VerticalAlignment.Center;
                    cardStack.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeight.Bold, Foreground = tealBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    cardStack.Children.Add(new TextBlock { Text = bigValue, FontSize = 36, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    cardStack.Children.Add(new Border { Width = 64, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
                    if (desc != "")
                        cardStack.Children.Add(new TextBlock { Text = desc, FontSize = 12.5, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    var kBorder = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = goldBrush,
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 18),
                        Child = cardStack
                    };
                    Grid.SetColumn(kBorder, i);
                    kGrid.Children.Add(kBorder);
                }
                slideGrid.Children.Add(kGrid);
            }
            else if (layout == "Línea de tiempo / Roadmap")
            {
                int tCount = Math.Clamp(cards.Count, 1, 5);
                var tGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", tCount))), ColumnSpacing = 12 };
                for (int i = 0; i < tCount; i++)
                {
                    var item = cards[i];
                    var stepStack = Column(6);
                    var badge = new Border
                    {
                        Background = i % 2 == 0 ? navyBrush : tealBrush,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6),
                        Child = new TextBlock { Text = $"{i + 1:D2} · {item.Title}", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis }
                    };
                    stepStack.Children.Add(badge);
                    if (item.Subtitle != "")
                        stepStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = goldBrush, Margin = new Thickness(2, 2, 0, 0) });
                    foreach (var l in item.Lines.Take(4))
                        stepStack.Children.Add(new TextBlock { Text = "▪ " + l, FontSize = 12.5, LineHeight = 17, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var stepCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = frameBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12),
                        Child = stepStack
                    };
                    Grid.SetColumn(stepCard, i);
                    tGrid.Children.Add(stepCard);
                }
                slideGrid.Children.Add(tGrid);
            }
            else if (layout == "Cuadrícula 2x2")
            {
                var qGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("*,*"),
                    ColumnSpacing = 14,
                    RowSpacing = 12
                };
                for (int i = 0; i < Math.Min(4, cards.Count); i++)
                {
                    var item = cards[i];
                    var qStack = Column(5);
                    qStack.Children.Add(new TextBlock { Text = item.Title, FontSize = 16, FontWeight = FontWeight.Bold, Foreground = i % 2 == 0 ? navyBrush : tealBrush });
                    if (item.Subtitle != "")
                        qStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = mutedBrush });
                    qStack.Children.Add(new Border { Height = 1.2, Background = goldBrush });
                    foreach (var l in item.Lines.Take(3))
                        qStack.Children.Add(new TextBlock { Text = "▪ " + l, FontSize = 12.5, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var qCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = frameBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 10),
                        Child = qStack
                    };
                    Grid.SetRow(qCard, i / 2);
                    Grid.SetColumn(qCard, i % 2);
                    qGrid.Children.Add(qCard);
                }
                slideGrid.Children.Add(qGrid);
            }
            else
            {
                int colCount = layout == "Comparativa (Antes / Después)" ? 2 : 3;
                var cGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", colCount))), ColumnSpacing = 16 };
                for (int i = 0; i < Math.Min(colCount, cards.Count); i++)
                {
                    var item = cards[i];
                    var cStack = Column(6);
                    var headBar = new Border
                    {
                        Background = i == 0 ? navyBrush : (i == 1 ? tealBrush : Brush.Parse("#3A4F66")),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(12, 7),
                        Child = new TextBlock { Text = item.Title, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis }
                    };
                    cStack.Children.Add(headBar);
                    if (item.Subtitle != "")
                        cStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = tealBrush });
                    if (i < slide.Images.Count && SlidePicture(i, colCount == 2 ? 390 : 250, 120) is { } colPic)
                        cStack.Children.Add(colPic);
                    foreach (var l in item.Lines.Take(5))
                        cStack.Children.Add(new TextBlock { Text = "▪ " + l, FontSize = 13, LineHeight = 18, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var cCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = frameBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 12),
                        Child = cStack
                    };
                    Grid.SetColumn(cCard, i);
                    cGrid.Children.Add(cCard);
                }
                slideGrid.Children.Add(cGrid);
            }
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout == "Galería triple")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Síntesis visual", slide.Summary);
                bottomNote.Margin = new Thickness(0, 10, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            var gGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 16 };
            for (int i = 0; i < 3; i++)
            {
                var gCard = Column(8);
                if (i < slide.Images.Count && SlidePicture(i, 272, 240, false, "#FFFFFF", 1) is { } pic)
                    gCard.Children.Add(pic);
                else
                    gCard.Children.Add(new Border { Width = 272, Height = 240, Background = Brushes.White, BorderBrush = frameBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
                var cap = slide.ImageCaptions.GetValueOrDefault(i, $"Lámina {i + 1}");
                gCard.Children.Add(new TextBlock { Text = cap, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(gCard, i);
                gGrid.Children.Add(gCard);
            }
            slideGrid.Children.Add(gGrid);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        var richBlocks = RenderRichBlocks(slide.PlainText, 360);
        var remainingImages = Enumerable.Range(0, slide.Images.Count).Where(i => !usedInTables.Contains(i)).ToList();

        if (layout == "Imagen protagonista" && remainingImages.Count > 0)
        {
            var stage = Column(8);
            int idx = remainingImages[0];
            if (SlidePicture(idx, 884, 372, false, "#243750", 1.2) is { } heroPic)
                stage.Children.Add(heroPic);
            var cap = slide.ImageCaptions.GetValueOrDefault(idx, "");
            var footer = string.Join(" — ", new[] { cap, slide.PlainText.Replace('\n', ' ').Trim(), slide.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (footer != "")
                stage.Children.Add(new TextBlock { Text = footer, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            slideGrid.Children.Add(stage);
        }
        else if (layout == "Tabla visual")
        {
            if (slide.Summary != "")
            {
                var note = SummaryBanner("Nota clave", slide.Summary);
                note.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(note, Dock.Bottom);
                slideGrid.Children.Add(note);
            }
            slideGrid.Children.Add(richBlocks);
        }
        else if (layout is "Moodboard y paleta" or "Moodboard y landmark")
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("0.95*,1.05*"), ColumnSpacing = 22 };
            cols.Children.Add(richBlocks);

            var rightCol = new DockPanel();
            if (slide.Summary != "")
            {
                var syn = SummaryBanner("Síntesis visual", slide.Summary);
                syn.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(syn, Dock.Bottom);
                rightCol.Children.Add(syn);
            }

            var imgs = remainingImages.Take(6).ToList();
            int count = Math.Max(1, imgs.Count);
            int gCols = count >= 5 ? 3 : 2;
            int gRows = Math.Max(1, (int)Math.Ceiling(count / (double)gCols));
            double availW = 436;
            double availH = slide.Summary != "" ? 332 : 404;
            double gap = 10;
            double cellW = (availW - gap * (gCols - 1)) / gCols;
            double cellH = (availH - gap * (gRows - 1)) / gRows;

            var mosaicGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", gCols))),
                RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", gRows))),
                ColumnSpacing = gap,
                RowSpacing = gap
            };
            for (int i = 0; i < imgs.Count; i++)
            {
                int imgIdx = imgs[i];
                var cap = slide.ImageCaptions.GetValueOrDefault(imgIdx, "");
                double picH = cap != "" ? Math.Max(40, cellH - 22) : Math.Max(40, cellH - 6);
                var tileStack = Column(2);
                tileStack.HorizontalAlignment = HorizontalAlignment.Center;
                tileStack.VerticalAlignment = VerticalAlignment.Center;
                if (SlidePicture(imgIdx, cellW - 6, picH, false, "#FFFFFF", 0) is { } pic)
                    tileStack.Children.Add(pic);
                if (cap != "")
                    tileStack.Children.Add(new TextBlock { Text = cap, Width = cellW - 8, FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                var tileBorder = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = frameBorderBrush,
                    BorderThickness = new Thickness(0.8),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2),
                    Child = tileStack
                };
                Grid.SetRow(tileBorder, i / gCols);
                Grid.SetColumn(tileBorder, i % gCols);
                mosaicGrid.Children.Add(tileBorder);
            }
            rightCol.Children.Add(mosaicGrid);
            Grid.SetColumn(rightCol, 1);
            cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else if (layout == "Plano y leyenda" || remainingImages.Count > 0 || (layout == "Dos columnas" && slide.Summary != ""))
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("0.96*,1.04*"), ColumnSpacing = 22 };
            cols.Children.Add(richBlocks);
            var rightCol = new DockPanel();
            if (slide.Summary != "")
            {
                var syn = SummaryBanner("Síntesis visual", slide.Summary);
                syn.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(syn, Dock.Bottom);
                rightCol.Children.Add(syn);
            }
            if (remainingImages.Count > 0)
            {
                int idx = remainingImages[0];
                var cap = slide.ImageCaptions.GetValueOrDefault(idx, "");
                double boxH = slide.Summary != "" ? 324 : 392;
                double imgH = cap != "" ? boxH - 28 : boxH - 10;
                var imgStack = Column(4);
                imgStack.HorizontalAlignment = HorizontalAlignment.Center;
                imgStack.VerticalAlignment = VerticalAlignment.Center;
                if (SlidePicture(idx, 424, imgH, false, "#FFFFFF", 0) is { } pic)
                    imgStack.Children.Add(pic);
                if (cap != "")
                    imgStack.Children.Add(new TextBlock { Text = cap, Width = 420, FontSize = 11.5, FontStyle = FontStyle.Italic, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                var imgFrame = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = goldBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(4),
                    Child = imgStack
                };
                rightCol.Children.Add(imgFrame);
            }
            Grid.SetColumn(rightCol, 1);
            cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else
        {
            if (slide.Summary != "")
            {
                var syn = SummaryBanner("Síntesis", slide.Summary);
                syn.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(syn, Dock.Bottom);
                slideGrid.Children.Add(syn);
            }
            slideGrid.Children.Add(richBlocks);
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
            top.Children.Add(Kicker($"  Modo presentación 16:9  ·  Diapositiva {idx + 1} de {sections.Count}  ·  {(SlideLayouts.Contains(section.Layout) ? section.Layout : "Texto e imagen")}"));
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
            var rows = Illustrations.Rows(section).ToList();
            var used = new HashSet<int>(); int start = 0;
            if (rows.Count == 0 && section.Images.Count > 0 && Illustrations.Layout(section,0).StartsWith("Ajustar")) { Illustration(0,paper); used.Add(0); }
            foreach (var row in rows)
            {
                AddBlocks(paper, section.PlainText[start..row[0].Start]);
                if(row.Count==1){int index=row[0].Image;used.Add(index);Illustration(index,paper);}
                else{
                    var strip=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",row.Select(_=>"*"))),ColumnSpacing=12};
                    for(int col=0;col<row.Count;col++){
                        int index=row[col].Image;used.Add(index);
                        var cell=Column(4);Illustration(index,cell,true);Grid.SetColumn(cell,col);strip.Children.Add(cell);
                    }
                    paper.Children.Add(strip);
                }
                start = row[^1].End;
            }
            AddBlocks(paper, section.PlainText[start..]);
            for (int i = 0; i < section.Images.Count; i++) if (!used.Contains(i)) Illustration(i,paper);
            if(work.Type=="CV"){
                foreach(var entry in section.Entries){
                    var block=Column(2);block.Children.Add(Display(entry.Heading+"  "+entry.Period,19));
                    if(entry.Organization!="")block.Children.Add(Label(entry.Organization));
                    AddBlocks(block,entry.Description);paper.Children.Add(block);
                }
            }
            if (section.Summary != "") { paper.Children.Add(Rule(Edge)); AddBlocks(paper, section.Summary); }
        }
        var sheet=new Border{Background=IsLight?Brush.Parse("#FCFBF8"):PanelColor,BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),
            BoxShadow=BoxShadows.Parse("0 12 28 #22000000"),Child=paper,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,28)};
        root.Children.Add(new ScrollViewer { Content = sheet }); page.Content = root;
    }
    void AddBlocks(Panel paper, string text)
    {
        var lines=text.ReplaceLineEndings("\n").Split('\n');
        for(int i=0;i<lines.Length;i++)
        {
            var line=lines[i];
            if(line is "[[Página]]" or "[[Índice]]"){paper.Children.Add(Rule(Edge));continue;}
            if(line.StartsWith("[[Paleta:") && line.EndsWith("]]")){
                var items=line[9..^2].Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                if(items.Length>0){
                    var grid=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",items.Select(_=>"*"))),ColumnSpacing=8,Margin=new Thickness(0,6)};
                    for(int c=0;c<items.Length;c++){
                        var m=Regex.Match(items[c],@"^#?([0-9A-Fa-f]{6})(?:\s+(.*))?$");
                        var hex="#"+(m.Success?m.Groups[1].Value.ToUpperInvariant():"666666");
                        var lbl=m.Success?m.Groups[2].Value.Trim():items[c];
                        var cell=Column(3);
                        cell.Children.Add(new Border{Height=34,Background=Color.TryParse(hex,out var col)?new SolidColorBrush(col):Brushes.Gray,CornerRadius=new CornerRadius(3),BorderBrush=Edge,BorderThickness=new Thickness(1)});
                        var codeLbl=Label(hex,11);codeLbl.HorizontalAlignment=HorizontalAlignment.Center;cell.Children.Add(codeLbl);
                        if(lbl!=""){var nameLbl=Label(lbl,11);nameLbl.HorizontalAlignment=HorizontalAlignment.Center;cell.Children.Add(nameLbl);}
                        Grid.SetColumn(cell,c);grid.Children.Add(cell);
                    }
                    paper.Children.Add(grid);
                }
                continue;
            }
            if(line.StartsWith("[[Firmas:") && line.EndsWith("]]")){
                var signers=line[9..^2].Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                if(signers.Length>0){
                    var grid=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",signers.Select(_=>"*"))),ColumnSpacing=18,Margin=new Thickness(0,18,0,6)};
                    for(int c=0;c<signers.Length;c++){
                        var cell=Column(4);
                        cell.Children.Add(Rule(Edge));
                        var sLbl=Label(signers[c],13);sLbl.FontWeight=FontWeight.SemiBold;sLbl.HorizontalAlignment=HorizontalAlignment.Center;
                        cell.Children.Add(sLbl);
                        Grid.SetColumn(cell,c);grid.Children.Add(cell);
                    }
                    paper.Children.Add(grid);
                }
                continue;
            }
            if(line.StartsWith('|') && line.EndsWith('|') && line.Length>2){
                var rows=new List<string[]>();
                do{
                    var cells=lines[i].Trim().Trim('|').Split('|');
                    if(!cells.All(c=>Regex.IsMatch(c.Trim(),@"^:?-+:?$")))rows.Add(cells);
                    i++;
                }while(i<lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--;if(rows.Count==0)continue;
                int columns=rows.Max(r=>r.Length);var table=Column(0);
                for(int r=0;r<rows.Count;r++){
                    var row=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",Enumerable.Repeat("*",columns)))};
                    for(int c=0;c<columns;c++){
                        var cell=new Border{BorderBrush=Edge,BorderThickness=new Thickness(.6),Padding=new Thickness(8,5),
                            Child=RichLine(c<rows[r].Length?rows[r][c].Trim():"",r==0 && rows.Count>1?16:WritingSize-1,r==0 && rows.Count>1)};
                        Grid.SetColumn(cell,c);row.Children.Add(cell);
                    }
                    table.Children.Add(row);
                }
                paper.Children.Add(table);continue;
            }
            var quote=line.StartsWith("> ");
            var sub3=line.StartsWith("### ");
            var heading=line.StartsWith("## ");
            var bullet=line.StartsWith("- ");
            var numMatch=Regex.Match(line,@"^([1-9][0-9]*)\.\s+(.*)$");
            var content=sub3?line[4..]:heading?line[3..]:(quote||bullet)?line[2..]:numMatch.Success?numMatch.Groups[2].Value:line;
            var block = RichLine(bullet?"• "+content:numMatch.Success?numMatch.Groups[1].Value+". "+content:content,heading?25:sub3?20:WritingSize,heading||sub3);
            if(quote)block.FontStyle=FontStyle.Italic;
            Control item=quote?new Border{BorderBrush=Accent,BorderThickness=new Thickness(2,0,0,0),Padding=new Thickness(14,2,0,2),Child=block}:block;
            paper.Children.Add(item);
        }
    }
    TextBlock RichLine(string content,double size,bool bold){
        var block = new TextBlock { FontFamily = WritingFamily, FontSize = size, LineHeight = size*1.58, TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal };
        foreach (var part in Regex.Split(content, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
        {
            if (part.Length > 4 && part.StartsWith("**") && part.EndsWith("**")) block.Inlines!.Add(new Run(part[2..^2]) { FontWeight = FontWeight.Bold });
            else if (part.Length > 2 && part.StartsWith('*') && part.EndsWith('*')) block.Inlines!.Add(new Run(part[1..^1]) { FontStyle = FontStyle.Italic });
            else block.Inlines!.Add(new Run(part));
        }
        return block;
    }
}
