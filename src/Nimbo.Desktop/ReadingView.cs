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
        "Divisor de sección / Impacto",
        "Ficha de concepto",
        "Ficha de personaje / Equipo",
        "Tabla visual",
        "Matriz de riesgos y seguridad",
        "Texto e imagen",
        "Dos columnas",
        "Tres columnas",
        "Cuatro columnas",
        "Cuadrícula 2x2",
        "Métricas e indicadores (KPIs)",
        "Línea de tiempo / Roadmap",
        "Flujo de proceso (Pipeline)",
        "Arquitectura en capas",
        "Embudo o pirámide estratégica",
        "Comparativa (Antes / Después)",
        "Checklist de validación",
        "Moodboard y paleta",
        "Moodboard y landmark",
        "Plano y leyenda",
        "Galería triple",
        "Imagen protagonista",
        "Cita o conclusión destacada",
        "Cierre y contacto",
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
        var slideGrid = new DockPanel { Margin = new Thickness(38, 20, 38, 16), ClipToBounds = true };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
        double titleFontSize = slide.Title.Length > 54 ? 20.5 : slide.Title.Length > 40 ? 22.5 : 25;
        header.Children.Add(new TextBlock { Text = slide.Title, FontSize = titleFontSize, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, VerticalAlignment = VerticalAlignment.Bottom });
        if (slide.Detail != "")
        {
            var detailTb = new TextBlock { Text = slide.Detail, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = tealBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, MaxWidth = 340, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 0, 0, 2) };
            Grid.SetColumn(detailTb, 1);
            header.Children.Add(detailTb);
        }
        var headerStack = Column(5);
        headerStack.Children.Add(header);
        headerStack.Children.Add(new Border { Height = 1.6, Background = goldBrush, Margin = new Thickness(0, 2, 0, 6) });
        DockPanel.SetDock(headerStack, Dock.Top);
        slideGrid.Children.Add(headerStack);

        Control FitBox(Control content, double maxWidth, double maxHeight, VerticalAlignment vAlign = VerticalAlignment.Top) => new Border
        {
            Width = maxWidth,
            Height = maxHeight,
            ClipToBounds = true,
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = vAlign,
                Child = new Border { Width = maxWidth, Child = content }
            }
        };

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
                FontSize = 12.5,
                LineHeight = 16.5,
                Foreground = navyBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            tb.Inlines!.Add(new Run(labelText + ": ") { FontWeight = FontWeight.Bold, Foreground = tealBrush });
            tb.Inlines.Add(new Run(bodyText));
            return new Border
            {
                Background = cardBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 7),
                MaxHeight = 52,
                ClipToBounds = true,
                Child = tb
            };
        }

        if (layout is "Divisor de sección / Impacto" or "Cierre y contacto")
        {
            var impactCard = new Border
            {
                Background = navyBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(34, 24),
                Margin = new Thickness(0, 6)
            };
            var iStack = Column(10);
            iStack.VerticalAlignment = VerticalAlignment.Center;
            if (slide.Detail != "")
                iStack.Children.Add(new TextBlock { Text = slide.Detail.ToUpperInvariant(), FontSize = 13, FontWeight = FontWeight.Bold, Foreground = goldBrush, TextAlignment = TextAlignment.Center });
            iStack.Children.Add(new TextBlock { Text = slide.Title, FontSize = 31, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            iStack.Children.Add(new Border { Width = 200, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
            if (slide.Summary != "")
                iStack.Children.Add(new TextBlock { Text = slide.Summary, FontSize = 15, Foreground = creamBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var bodyLines = slide.PlainText.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bodyLines.Length > 0)
            {
                var pillWrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
                foreach (var rawLine in bodyLines.Take(6))
                {
                    var clean = rawLine.TrimStart('-', '#', ' ').Replace("**", "");
                    pillWrap.Children.Add(new Border
                    {
                        Background = tealBrush,
                        BorderBrush = goldBrush,
                        BorderThickness = new Thickness(0.8),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14, 5),
                        Margin = new Thickness(5, 4),
                        Child = new TextBlock { Text = clean, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White }
                    });
                }
                iStack.Children.Add(pillWrap);
            }
            impactCard.Child = FitBox(iStack, 810, 355, VerticalAlignment.Center);
            slideGrid.Children.Add(impactCard);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout == "Ficha de personaje / Equipo")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Perfil", slide.Summary);
                bottomNote.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            double availH = slide.Summary != "" ? 360 : 418;
            var pCols = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*"), ColumnSpacing = 20 };
            var leftCard = new Border
            {
                Background = navyBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                ClipToBounds = true
            };
            var lStack = Column(8);
            lStack.HorizontalAlignment = HorizontalAlignment.Center;
            lStack.VerticalAlignment = VerticalAlignment.Center;
            if (slide.Images.Count > 0 && SlidePicture(0, 224, availH - 86, false, "#FFFFFF", 1) is { } portrait)
                lStack.Children.Add(portrait);
            else
                lStack.Children.Add(new Border { Width = 224, Height = availH - 86, Background = Brush.Parse("#18293D"), BorderBrush = goldBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
            var roleCap = slide.ImageCaptions.GetValueOrDefault(0, slide.Detail != "" ? slide.Detail : slide.Title);
            lStack.Children.Add(new TextBlock { Text = roleCap, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            leftCard.Child = lStack;
            pCols.Children.Add(leftCard);

            var rightHost = new Border
            {
                Background = Brushes.White,
                BorderBrush = frameBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12),
                ClipToBounds = true,
                Child = FitBox(RenderRichBlocks(slide.PlainText, availH - 36), 572, availH - 26)
            };
            Grid.SetColumn(rightHost, 1);
            pCols.Children.Add(rightHost);
            slideGrid.Children.Add(pCols);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout == "Cita o conclusión destacada")
        {
            var quoteCard = new Border
            {
                Background = navyBrush,
                BorderBrush = goldBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(38, 28),
                Margin = new Thickness(0, 10)
            };
            var qStack = Column(14);
            qStack.VerticalAlignment = VerticalAlignment.Center;
            var mainQuote = slide.PlainText.Replace("> ", "").Trim();
            if (mainQuote == "") mainQuote = slide.Summary != "" ? slide.Summary : "Escribe aquí la conclusión ejecutiva o cita central de la diapositiva.";
            qStack.Children.Add(new TextBlock { Text = "“" + mainQuote.Replace("**", "") + "”", FontSize = 23, FontStyle = FontStyle.Italic, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            qStack.Children.Add(new Border { Width = 160, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
            if (slide.Summary != "" && slide.PlainText.Trim() != "")
                qStack.Children.Add(new TextBlock { Text = slide.Summary, FontSize = 14.5, Foreground = creamBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            quoteCard.Child = FitBox(qStack, 804, 345, VerticalAlignment.Center);
            slideGrid.Children.Add(quoteCard);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout is "Tres columnas" or "Cuatro columnas" or "Cuadrícula 2x2" or "Métricas e indicadores (KPIs)" or "Línea de tiempo / Roadmap" or "Flujo de proceso (Pipeline)" or "Arquitectura en capas" or "Embudo o pirámide estratégica" or "Checklist de validación" or "Comparativa (Antes / Después)")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Síntesis", slide.Summary);
                bottomNote.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            double areaH = slide.Summary != "" ? 358 : 418;
            var cards = ParseStructuredCards(slide.PlainText);
            if (cards.Count == 0)
            {
                cards.Add(("Apartado 1", "Valor o enfoque", ["Añade bloques con ### Título o una tabla Markdown."]));
                cards.Add(("Apartado 2", "Valor o enfoque", ["Cada bloque o fila se convierte en una tarjeta visual."]));
            }

            if (layout == "Métricas e indicadores (KPIs)")
            {
                int kCount = Math.Clamp(cards.Count, 1, 4);
                double cardW = (884 - 14 * (kCount - 1)) / kCount - 28;
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
                    cardStack.Children.Add(new TextBlock { Text = bigValue, FontSize = 34, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    cardStack.Children.Add(new Border { Width = 64, Height = 2, Background = goldBrush, HorizontalAlignment = HorizontalAlignment.Center });
                    if (desc != "")
                        cardStack.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    var kBorder = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = goldBrush,
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 14),
                        ClipToBounds = true,
                        Child = FitBox(cardStack, cardW, areaH - 32, VerticalAlignment.Center)
                    };
                    Grid.SetColumn(kBorder, i);
                    kGrid.Children.Add(kBorder);
                }
                slideGrid.Children.Add(kGrid);
            }
            else if (layout is "Línea de tiempo / Roadmap" or "Flujo de proceso (Pipeline)")
            {
                bool isPipeline = layout == "Flujo de proceso (Pipeline)";
                int tCount = Math.Clamp(cards.Count, 1, 5);
                double cardW = (884 - 12 * (tCount - 1)) / tCount - 24;
                var tGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", tCount))), ColumnSpacing = 12 };
                for (int i = 0; i < tCount; i++)
                {
                    var item = cards[i];
                    var stepStack = Column(6);
                    var prefix = isPipeline ? $"PASO {i + 1} ▸ " : $"{i + 1:D2} · ";
                    var badge = new Border
                    {
                        Background = i % 2 == 0 ? navyBrush : tealBrush,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6),
                        Child = new TextBlock { Text = prefix + item.Title, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis }
                    };
                    stepStack.Children.Add(badge);
                    if (item.Subtitle != "")
                        stepStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = goldBrush, Margin = new Thickness(2, 2, 0, 0) });
                    foreach (var l in item.Lines)
                        stepStack.Children.Add(new TextBlock { Text = "▪ " + l, FontSize = 12, LineHeight = 16.5, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var stepCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = isPipeline ? goldBrush : frameBorderBrush,
                        BorderThickness = new Thickness(isPipeline ? 1.2 : 1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        ClipToBounds = true,
                        Child = FitBox(stepStack, cardW, areaH - 24)
                    };
                    Grid.SetColumn(stepCard, i);
                    tGrid.Children.Add(stepCard);
                }
                slideGrid.Children.Add(tGrid);
            }
            else if (layout is "Arquitectura en capas" or "Embudo o pirámide estratégica")
            {
                bool isFunnel = layout == "Embudo o pirámide estratégica";
                int lCount = Math.Clamp(cards.Count, 1, 5);
                var lGrid = new Grid { RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", lCount))), RowSpacing = 8 };
                double rowH = (areaH - 8 * (lCount - 1)) / lCount;
                for (int i = 0; i < lCount; i++)
                {
                    var item = cards[i];
                    double widthFactor = isFunnel ? (1.0 - i * 0.11) : 1.0;
                    double barW = Math.Round(884 * widthFactor);
                    var rowInner = new Grid { ColumnDefinitions = new ColumnDefinitions("250,*"), ColumnSpacing = 14 };
                    var leftBadge = new Border
                    {
                        Background = i % 2 == 0 ? navyBrush : tealBrush,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(12, 6),
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Child = new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Text = item.Title, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis },
                                new TextBlock { Text = item.Subtitle, FontSize = 11, Foreground = goldBrush, IsVisible = item.Subtitle != "", TextTrimming = TextTrimming.CharacterEllipsis }
                            }
                        }
                    };
                    rowInner.Children.Add(leftBadge);
                    var descText = string.Join("   ·   ", item.Lines);
                    var rightDesc = new TextBlock
                    {
                        Text = descText != "" ? descText : item.Subtitle,
                        FontSize = 12.5,
                        Foreground = inkBrush,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(rightDesc, 1);
                    rowInner.Children.Add(rightDesc);
                    var layerCard = new Border
                    {
                        Width = barW,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Background = Brushes.White,
                        BorderBrush = goldBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 6),
                        ClipToBounds = true,
                        Child = FitBox(rowInner, barW - 24, rowH - 14, VerticalAlignment.Center)
                    };
                    Grid.SetRow(layerCard, i);
                    lGrid.Children.Add(layerCard);
                }
                slideGrid.Children.Add(lGrid);
            }
            else if (layout is "Cuadrícula 2x2" or "Checklist de validación")
            {
                bool isCheck = layout == "Checklist de validación";
                var qGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("*,*"),
                    ColumnSpacing = 14,
                    RowSpacing = 12
                };
                double cellW = (884 - 14) / 2 - 28;
                double cellH = (areaH - 12) / 2 - 20;
                for (int i = 0; i < Math.Min(4, cards.Count); i++)
                {
                    var item = cards[i];
                    var qStack = Column(4);
                    var headPrefix = isCheck ? "✔  " : "";
                    qStack.Children.Add(new TextBlock { Text = headPrefix + item.Title, FontSize = 15.5, FontWeight = FontWeight.Bold, Foreground = i % 2 == 0 ? navyBrush : tealBrush });
                    if (item.Subtitle != "")
                        qStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = mutedBrush });
                    qStack.Children.Add(new Border { Height = 1.2, Background = goldBrush });
                    var bulletPrefix = isCheck ? "☑ " : "▪ ";
                    foreach (var l in item.Lines)
                        qStack.Children.Add(new TextBlock { Text = bulletPrefix + l, FontSize = 12, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var qCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = isCheck ? tealBrush : frameBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 10),
                        ClipToBounds = true,
                        Child = FitBox(qStack, cellW, cellH)
                    };
                    Grid.SetRow(qCard, i / 2);
                    Grid.SetColumn(qCard, i % 2);
                    qGrid.Children.Add(qCard);
                }
                slideGrid.Children.Add(qGrid);
            }
            else
            {
                int colCount = layout == "Comparativa (Antes / Después)" ? 2 : (layout == "Cuatro columnas" ? 4 : 3);
                double gap = colCount == 4 ? 12 : 16;
                double cardW = (884 - gap * (colCount - 1)) / colCount - 24;
                var cGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", colCount))), ColumnSpacing = gap };
                for (int i = 0; i < Math.Min(colCount, cards.Count); i++)
                {
                    var item = cards[i];
                    var cStack = Column(6);
                    var headBar = new Border
                    {
                        Background = i % 2 == 0 ? navyBrush : tealBrush,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6),
                        Child = new TextBlock { Text = item.Title, FontSize = colCount == 4 ? 13 : 14.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis }
                    };
                    cStack.Children.Add(headBar);
                    if (item.Subtitle != "")
                        cStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = tealBrush });
                    if (i < slide.Images.Count && SlidePicture(i, cardW, colCount == 2 ? 120 : 96) is { } colPic)
                        cStack.Children.Add(colPic);
                    foreach (var l in item.Lines)
                        cStack.Children.Add(new TextBlock { Text = "▪ " + l, FontSize = 12, LineHeight = 16.5, Foreground = inkBrush, TextWrapping = TextWrapping.Wrap });
                    var cCard = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = frameBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 10),
                        ClipToBounds = true,
                        Child = FitBox(cStack, cardW, areaH - 22)
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
                bottomNote.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            double picH = slide.Summary != "" ? 290 : 350;
            var gGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 16 };
            for (int i = 0; i < 3; i++)
            {
                var gCard = Column(6);
                if (i < slide.Images.Count && SlidePicture(i, 272, picH, false, "#FFFFFF", 1) is { } pic)
                    gCard.Children.Add(pic);
                else
                    gCard.Children.Add(new Border { Width = 272, Height = picH, Background = Brushes.White, BorderBrush = frameBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
                var cap = slide.ImageCaptions.GetValueOrDefault(i, $"Lámina {i + 1}");
                gCard.Children.Add(new TextBlock { Text = cap, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(gCard, i);
                gGrid.Children.Add(gCard);
            }
            slideGrid.Children.Add(gGrid);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        double maxBodyH = slide.Summary != "" ? 356 : 418;
        var richBlocks = RenderRichBlocks(slide.PlainText, maxBodyH - 12);
        var remainingImages = Enumerable.Range(0, slide.Images.Count).Where(i => !usedInTables.Contains(i)).ToList();

        if (layout == "Imagen protagonista" && remainingImages.Count > 0)
        {
            var stage = Column(6);
            int idx = remainingImages[0];
            if (SlidePicture(idx, 884, 372, false, "#243750", 1.2) is { } heroPic)
                stage.Children.Add(heroPic);
            var cap = slide.ImageCaptions.GetValueOrDefault(idx, "");
            var footer = string.Join(" — ", new[] { cap, slide.PlainText.Replace('\n', ' ').Trim(), slide.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (footer != "")
                stage.Children.Add(new TextBlock { Text = footer, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            slideGrid.Children.Add(stage);
        }
        else if (layout is "Tabla visual" or "Matriz de riesgos y seguridad")
        {
            if (slide.Summary != "")
            {
                var note = SummaryBanner(layout == "Matriz de riesgos y seguridad" ? "Protocolo de seguridad" : "Nota clave", slide.Summary);
                note.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(note, Dock.Bottom);
                slideGrid.Children.Add(note);
            }
            slideGrid.Children.Add(FitBox(richBlocks, 884, maxBodyH));
        }
        else if (layout is "Moodboard y paleta" or "Moodboard y landmark")
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("0.95*,1.05*"), ColumnSpacing = 22 };
            cols.Children.Add(FitBox(richBlocks, 424, 418));

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
                    ClipToBounds = true,
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
            cols.Children.Add(FitBox(richBlocks, 424, 418));
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
                    ClipToBounds = true,
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
            slideGrid.Children.Add(FitBox(richBlocks, 884, maxBodyH));
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

    static string ResolveTemplateStyle(Work work)
    {
        var format = Format.All.FirstOrDefault(f => f.Name == work.Type);
        if (format == null) return "Clásica";
        return EditorialTemplate.For(format).FirstOrDefault(t => t.Name == work.Template)?.Style ?? "Clásica";
    }

    static string ResolveFormatAccentHex(Work work)
    {
        var raw = work.Type switch
        {
            "CV" => "#65455B",
            "Informe" => "#315671",
            "Artículo" => "#4E476E",
            "Carta" => "#784F3E",
            "Acta" => "#5C634F",
            "Apuntes" => "#004785",
            "Propuesta" => "#364A8A",
            "Documento" => "#2B5B78",
            _ => "#365E64"
        };
        return work.PrintAccent switch
        {
            "Petróleo" => "#245966",
            "Vino" => "#843B59",
            "Oliva" => "#51683C",
            "Grafito" => "#414650",
            _ => raw
        };
    }

    internal Border BuildDocumentCoverCanvas(Work work, int decodeWidth = 720, Action<Chapter>? onSelectChapter = null)
    {
        var sections = WorldModel.Ordered(work).ToList();
        var style = ResolveTemplateStyle(work);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);

        if (work.IsNovel)
        {
            const double W = 540, H = 760;
            var clothHex = Color.TryParse(work.CoverColor, out _) ? work.CoverColor : "#284B52";
            var coverRoot = new Grid { Width = W, Height = H, ClipToBounds = true, Background = Brush.Parse(clothHex) };

            void AddFullBleedArt(Panel target, double blurRadius = 0)
            {
                if (work.CoverImage != "")
                {
                    var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                    if (Thumbnail(path, decodeWidth, H) is { } pic)
                    {
                        pic.Width = W;
                        pic.Height = H;
                        pic.Stretch = Stretch.UniformToFill;
                        if (blurRadius > 0) pic.Effect = new BlurEffect { Radius = blurRadius };
                        target.Children.Add(pic);
                        return;
                    }
                }
                var fallback = new Border
                {
                    Width = W,
                    Height = H,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(Color.Parse(clothHex), 0),
                            new GradientStop(Color.Parse("#141C22"), 1)
                        }
                    }
                };
                if (blurRadius > 0) fallback.Effect = new BlurEffect { Radius = blurRadius };
                target.Children.Add(fallback);
            }

            AddFullBleedArt(coverRoot, 0);
            coverRoot.Children.Add(new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#180D1318"), 0),
                        new GradientStop(Color.Parse("#320D1318"), 0.5),
                        new GradientStop(Color.Parse("#700D1318"), 1)
                    }
                }
            });

            var coverMode = EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle)
                ? work.CoverStyle
                : work.CoverStyle switch
                {
                    "Centrada" => "Cartela central difuminada",
                    "Cabecera" => "Franja superior de autor",
                    _ => "Velo editorial al pie"
                };

            if (coverMode is "Marco áureo con velo" or "Doble filete de gala")
            {
                coverRoot.Children.Add(new Border
                {
                    Margin = new Thickness(20),
                    BorderBrush = Brush.Parse("#C8E6C37A"),
                    BorderThickness = new Thickness(1.2),
                    CornerRadius = new CornerRadius(2)
                });
                if (coverMode == "Doble filete de gala")
                {
                    coverRoot.Children.Add(new Border
                    {
                        Margin = new Thickness(26),
                        BorderBrush = Brush.Parse("#90E6C37A"),
                        BorderThickness = new Thickness(0.7),
                        CornerRadius = new CornerRadius(2)
                    });
                }
            }

            var plateMargin = coverMode switch
            {
                "Cartela central difuminada" => new Thickness(40, 0, 40, 0),
                "Franja superior de autor" => new Thickness(28, 36, 28, 0),
                "Marco áureo con velo" => new Thickness(40, 0, 40, 52),
                "Tejuelo clásico translúcido" => new Thickness(44, 0, 44, 58),
                "Banda diagonal moderna" => new Thickness(0, 0, 0, 62),
                "Doble filete de gala" => new Thickness(44, 0, 44, 54),
                "Minimalista a sangre" => new Thickness(28, 0, 88, 34),
                _ => new Thickness(26, 0, 26, 30)
            };
            var plateAlign = coverMode switch
            {
                "Cartela central difuminada" => VerticalAlignment.Center,
                "Franja superior de autor" => VerticalAlignment.Top,
                _ => VerticalAlignment.Bottom
            };
            bool leftAligned = coverMode is "Minimalista a sangre" or "Banda diagonal moderna";

            var plateGrid = new Grid();
            var plateHost = new Border
            {
                Margin = plateMargin,
                VerticalAlignment = plateAlign,
                ClipToBounds = true,
                CornerRadius = new CornerRadius(coverMode == "Banda diagonal moderna" ? 0 : 6),
                Child = plateGrid
            };

            var blurredBackdrop = new Grid { ClipToBounds = true };
            AddFullBleedArt(blurredBackdrop, 22);
            blurredBackdrop.Children.Add(new Border
            {
                Background = Brush.Parse(coverMode == "Tejuelo clásico translúcido" ? "#B810161E" : "#9E111820"),
                Effect = new BlurEffect { Radius = 14 }
            });
            plateGrid.Children.Add(blurredBackdrop);

            var textStack = Column(8);
            textStack.Margin = new Thickness(26, 22);
            textStack.HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center;

            textStack.Children.Add(new TextBlock
            {
                Text = "NOVELA  ·  " + coverMode.ToUpperInvariant(),
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 1.6,
                Foreground = Brush.Parse("#E8C87A"),
                TextAlignment = leftAligned ? TextAlignment.Left : TextAlignment.Center,
                HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center
            });
            textStack.Children.Add(new TextBlock
            {
                Text = work.Title,
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = work.Title.Length > 34 ? 27 : 32,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#FAF6EE"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = leftAligned ? TextAlignment.Left : TextAlignment.Center,
                HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center
            });
            if (work.ShowCoverSubtitle && work.Subtitle != "")
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = work.Subtitle,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                    FontSize = 15,
                    FontStyle = FontStyle.Italic,
                    Foreground = Brush.Parse("#E4DAC8"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = leftAligned ? TextAlignment.Left : TextAlignment.Center,
                    HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center
                });
            }
            textStack.Children.Add(new Border
            {
                Width = 140,
                Height = 1.2,
                Background = Brush.Parse("#D8B76A"),
                HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                Margin = new Thickness(0, 4)
            });
            if (work.Author != "")
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = work.Author,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#FAF5EA"),
                    TextAlignment = leftAligned ? TextAlignment.Left : TextAlignment.Center,
                    HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center
                });
            }

            var frameOverlay = new Border
            {
                BorderBrush = Brush.Parse(coverMode is "Tejuelo clásico translúcido" or "Marco áureo con velo" or "Doble filete de gala" ? "#D8E8C472" : "#66FFFFFF"),
                BorderThickness = new Thickness(coverMode == "Banda diagonal moderna" ? 0 : 1.2),
                CornerRadius = new CornerRadius(coverMode == "Banda diagonal moderna" ? 0 : 6),
                Child = textStack
            };
            plateGrid.Children.Add(frameOverlay);
            coverRoot.Children.Add(plateHost);

            return new Border
            {
                Child = coverRoot,
                BorderBrush = Edge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                BoxShadow = BoxShadows.Parse("0 14 32 #38000000"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        var pagePaper = Brushes.White;
        var ink = Brush.Parse("#1E2328");
        var muted = Brush.Parse("#464646");
        var sheetCol = Column(14);
        sheetCol.Width = 720;
        sheetCol.Margin = new Thickness(44, 38);

        string Fact(string key, string fallback = "") =>
            work.DocumentFacts.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

        void AddTocBlock(string headingTitle, string accentColorHex, string? underlineGreenHex = null)
        {
            if (sections.Count == 0) return;
            sheetCol.Children.Add(new Border { Height = 1, Background = Brush.Parse("#D5DCE2"), Margin = new Thickness(0, 8, 0, 4) });
            var tocHead = new TextBlock
            {
                Text = headingTitle,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse(accentColorHex)
            };
            sheetCol.Children.Add(tocHead);
            if (underlineGreenHex != null)
                sheetCol.Children.Add(new Border { Height = 2, Background = Brush.Parse(underlineGreenHex), Margin = new Thickness(0, 2, 0, 6) });
            var tocList = Column(4);
            for (int i = 0; i < sections.Count; i++)
            {
                var ch = sections[i];
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                row.Children.Add(new TextBlock
                {
                    Text = $"{i + 1}. ",
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse(accentColorHex),
                    FontSize = 13.5
                });
                var titleTb = new TextBlock
                {
                    Text = ch.Title,
                    Foreground = ink,
                    FontSize = 13.5,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(titleTb, 1);
                row.Children.Add(titleTb);
                var pgTb = new TextBlock
                {
                    Text = $"pág. {i + 1}",
                    Foreground = muted,
                    FontSize = 12.5
                };
                Grid.SetColumn(pgTb, 2);
                row.Children.Add(pgTb);
                if (onSelectChapter != null)
                {
                    var btn = new Button
                    {
                        Content = row,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(4, 3),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };
                    var target = ch;
                    btn.Click += (_, _) => onSelectChapter(target);
                    tocList.Children.Add(btn);
                }
                else tocList.Children.Add(row);
            }
            sheetCol.Children.Add(tocList);
        }

        if (work.Type == "Apuntes")
        {
            var uaslpBlue = "#004785";
            var uaslpGreen = "#22714B";
            var blueBrush = Brush.Parse(uaslpBlue);
            var univ = Fact("Universidad", "Universidad Autónoma de San Luis Potosí");
            var fac = Fact("Facultad", "Facultad de Ingeniería");
            var asig = Fact("Asignatura", work.Subtitle != "" ? work.Subtitle : work.Title);
            var clave = Fact("Clave de clase", "AC-001");
            var prof = Fact("Docente", "Docente titular");
            var carrera = Fact("Carrera", "Ingeniería en Sistemas Inteligentes");
            var claveU = Fact("Clave U", "—");
            var alumno = Fact("Alumno", work.Author != "" ? work.Author : "Autor de la libreta");
            var lugarFecha = Fact("Lugar y fecha", Fact("Periodo", "San Luis Potosí, S.L.P."));

            sheetCol.Children.Add(new TextBlock { Text = univ, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = blueBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            sheetCol.Children.Add(new TextBlock { Text = fac, FontSize = 16, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            sheetCol.Children.Add(new TextBlock { Text = asig, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) });

            var bannerStack = Column(6);
            bannerStack.HorizontalAlignment = HorizontalAlignment.Center;
            bannerStack.Children.Add(new TextBlock { Text = style == "Cornell" ? "Cuaderno Cornell:" : "Apuntes:", FontSize = 19, FontWeight = FontWeight.Bold, Foreground = blueBrush, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            bannerStack.Children.Add(new TextBlock { Text = work.Title, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            sheetCol.Children.Add(new Border
            {
                Background = Brush.Parse("#E6EDF5"),
                BorderBrush = Brush.Parse("#B8CCE0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(20, 14),
                Child = bannerStack
            });

            if (work.CoverImage != "")
            {
                var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                if (Thumbnail(path, 480, 140) is { } pic)
                {
                    pic.MaxHeight = 140;
                    pic.Stretch = Stretch.Uniform;
                    pic.HorizontalAlignment = HorizontalAlignment.Center;
                    sheetCol.Children.Add(new Border { Child = pic, Margin = new Thickness(0, 4) });
                }
            }

            var tableGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*"), RowSpacing = 6, Margin = new Thickness(36, 10, 36, 6) };
            (string Label, string Val)[] rows =
            [
                ("Clase:", $"{asig} ({clave})"),
                ("Profesor:", prof),
                ("Carrera:", carrera),
                ("Clave U:", claveU)
            ];
            for (int r = 0; r < rows.Length; r++)
            {
                tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var lTb = new TextBlock { Text = rows[r].Label, FontSize = 14, FontWeight = FontWeight.Bold, Foreground = blueBrush };
                var vTb = new TextBlock { Text = rows[r].Val, FontSize = 14, Foreground = ink, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(lTb, r); Grid.SetColumn(lTb, 0);
                Grid.SetRow(vTb, r); Grid.SetColumn(vTb, 1);
                tableGrid.Children.Add(lTb);
                tableGrid.Children.Add(vTb);
            }
            sheetCol.Children.Add(tableGrid);

            var studentBox = Column(4);
            studentBox.Margin = new Thickness(36, 4, 36, 4);
            studentBox.Children.Add(new TextBlock { Text = "Alumno:", FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = blueBrush });
            studentBox.Children.Add(new TextBlock { Text = "•  " + alumno, FontSize = 14, Foreground = ink, Margin = new Thickness(12, 0, 0, 0) });
            sheetCol.Children.Add(studentBox);

            sheetCol.Children.Add(new TextBlock { Text = lugarFecha, FontSize = 12.5, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4) });
            AddTocBlock("Índice", uaslpBlue, uaslpGreen);
        }
        else if (work.Type == "Informe")
        {
            bool isPractice = style == "PracticaRedes" || work.CoverStyle == "Portada de Práctica (P3/P4/P5)" || work.DocumentFacts.ContainsKey("Materia y ciclo") && Fact("Materia y ciclo") != "";
            if (isPractice)
            {
                var materia = Fact("Materia y ciclo", Fact("Departamento", "Interacción de Redes (2025-2026/I)"));
                var realizada = Fact("Realizada por", Fact("Alumno", work.Author != "" ? work.Author : "Equipo de Laboratorio"));
                var prof = Fact("Profesor", "Profesor de Laboratorio");
                var alumno = Fact("Alumno", work.Author != "" ? work.Author : realizada);
                var carrera = Fact("Carrera y claves", Fact("Institución", "Ingeniería en Sistemas Inteligentes"));
                var claveMat = Fact("Clave de materia", "2257 · Grupo 1");
                var fecha = Fact("Fecha", Fact("Periodo del informe", "Noviembre de 2026"));

                sheetCol.Children.Add(new TextBlock { Text = materia, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                sheetCol.Children.Add(new TextBlock { Text = "Realizada por:", FontSize = 13.5, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                sheetCol.Children.Add(new TextBlock { Text = realizada, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                sheetCol.Children.Add(new TextBlock { Text = work.Title, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 6) });
                if (work.Subtitle != "")
                    sheetCol.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 14, FontStyle = FontStyle.Italic, Foreground = muted, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });

                var metaCenter = Column(3);
                metaCenter.HorizontalAlignment = HorizontalAlignment.Center;
                metaCenter.Margin = new Thickness(0, 6);
                foreach (var line in new[]
                {
                    "Profesor: " + prof,
                    "Alumno: " + alumno,
                    carrera,
                    "Clave de la materia: " + claveMat,
                    "Fecha: " + fecha
                }.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    metaCenter.Children.Add(new TextBlock { Text = line, FontSize = 13.5, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                }
                sheetCol.Children.Add(metaCenter);
                if (work.CoverImage != "")
                {
                    var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                    if (Thumbnail(path, 520, 160) is { } pic)
                    {
                        pic.MaxHeight = 160;
                        pic.Stretch = Stretch.Uniform;
                        pic.HorizontalAlignment = HorizontalAlignment.Center;
                        sheetCol.Children.Add(pic);
                    }
                }
                AddTocBlock("Contenido", accentHex);
            }
            else
            {
                var banner = Column(6);
                banner.Children.Add(new TextBlock { Text = work.Template.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#D8E6F2"), LetterSpacing = 1.2 });
                banner.Children.Add(new TextBlock { Text = work.Title, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
                if (work.Subtitle != "")
                    banner.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 14, Foreground = Brush.Parse("#E6EFF7"), TextWrapping = TextWrapping.Wrap });
                if (work.Author != "")
                    banner.Children.Add(new TextBlock { Text = work.Author, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
                sheetCol.Children.Add(new Border { Background = accentBrush, CornerRadius = new CornerRadius(4), Padding = new Thickness(22, 18), Child = banner });
                if (work.CoverImage != "")
                {
                    var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                    if (Thumbnail(path, 560, 170) is { } pic)
                    {
                        pic.MaxHeight = 170;
                        pic.Stretch = Stretch.Uniform;
                        pic.HorizontalAlignment = HorizontalAlignment.Center;
                        sheetCol.Children.Add(pic);
                    }
                }
                AddFrontMatterTable(sheetCol, work, accentHex, ink);
                AddTocBlock("Contenido del informe", accentHex);
            }
        }
        else if (work.Type == "CV")
        {
            return BuildResumeSheetCanvas(work, decodeWidth);
        }
        else if (work.Type == "Carta")
        {
            var placeDate = Fact("Lugar y fecha", "Ciudad, a fecha actual");
            var subject = Fact("Asunto", work.Title);
            var recipient = Fact("Destinatario", "Destinatario institucional");
            var address = Fact("Dirección", Fact("Cargo o institución", "Presente"));
            sheetCol.Children.Add(new TextBlock { Text = placeDate, FontSize = 13.5, Foreground = muted, TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Right });
            sheetCol.Children.Add(new TextBlock { Text = "ASUNTO: " + subject, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = accentBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
            var recBlock = Column(2);
            recBlock.Children.Add(new TextBlock { Text = "Dirigido a:", FontSize = 13.5, Foreground = muted });
            recBlock.Children.Add(new TextBlock { Text = recipient, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = ink });
            if (address != "") recBlock.Children.Add(new TextBlock { Text = address, FontSize = 13.5, Foreground = ink });
            recBlock.Children.Add(new TextBlock { Text = "PRESENTE.", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = ink });
            sheetCol.Children.Add(recBlock);
            sheetCol.Children.Add(new Border { Height = 1.5, Background = accentBrush, Margin = new Thickness(0, 6) });
            if (sections.Count > 0)
            {
                var previewBody = Column(10);
                AddStyledBlocks(previewBody, sections[0].PlainText, accentHex, ink, 14);
                sheetCol.Children.Add(previewBody);
            }
        }
        else
        {
            var topHeader = Column(6);
            topHeader.Children.Add(new TextBlock { Text = $"{work.Type.ToUpperInvariant()}  ·  {work.Template}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = accentBrush, LetterSpacing = 1.1 });
            topHeader.Children.Add(new TextBlock { Text = work.Title, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = ink, TextWrapping = TextWrapping.Wrap });
            if (work.Subtitle != "")
                topHeader.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 15, Foreground = muted, TextWrapping = TextWrapping.Wrap });
            if (work.Author != "")
                topHeader.Children.Add(new TextBlock { Text = "Autor / Responsable: " + work.Author, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = ink });
            sheetCol.Children.Add(topHeader);
            sheetCol.Children.Add(new Border { Height = 2, Background = accentBrush });
            if (work.CoverImage != "")
            {
                var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                if (Thumbnail(path, 520, 155) is { } pic)
                {
                    pic.MaxHeight = 155;
                    pic.Stretch = Stretch.Uniform;
                    pic.HorizontalAlignment = HorizontalAlignment.Center;
                    sheetCol.Children.Add(pic);
                }
            }
            AddFrontMatterTable(sheetCol, work, accentHex, ink);
            AddTocBlock("Índice de secciones", accentHex);
        }

        return new Border
        {
            Background = pagePaper,
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            BoxShadow = BoxShadows.Parse("0 12 28 #24000000"),
            Child = sheetCol,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    void AddFrontMatterTable(StackPanel target, Work work, string accentHex, IBrush ink)
    {
        var keys = EditorialTemplate.FrontMatter(work.Type)
            .Where(k => !string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k)))
            .ToArray();
        if (keys.Length == 0) return;
        var tbl = Column(0);
        foreach (var k in keys)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*") };
            row.Children.Add(new Border
            {
                Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.14),
                BorderBrush = Brush.Parse("#CFD7DE"),
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(10, 6),
                Child = new TextBlock { Text = k, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(accentHex) }
            });
            var valBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush.Parse("#CFD7DE"),
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(10, 6),
                Child = new TextBlock { Text = work.DocumentFacts[k], FontSize = 13, Foreground = ink, TextWrapping = TextWrapping.Wrap }
            };
            Grid.SetColumn(valBorder, 1);
            row.Children.Add(valBorder);
            tbl.Children.Add(row);
        }
        target.Children.Add(tbl);
    }

    Border BuildResumeSheetCanvas(Work work, int decodeWidth = 720, Chapter? highlightSection = null)
    {
        var style = ResolveTemplateStyle(work);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var ink = Brush.Parse("#1D2228");
        var muted = Brush.Parse("#4F5964");
        string Contact(string k) => work.Contact.GetValueOrDefault(k, "").Trim();
        var name = Contact("Nombre profesional") != "" ? Contact("Nombre profesional") : work.Title;
        var profession = Contact("Profesión") != "" ? Contact("Profesión") : work.Subtitle;
        var contacts = new[] { "Correo", "Teléfono", "Ciudad", "Portafolio", "Perfil profesional en línea" }
            .Select(Contact).Where(s => s != "").ToArray();
        var ordered = WorldModel.Ordered(work).ToList();

        Control RenderCvSection(Chapter sec, bool compact = false)
        {
            bool highlighted = highlightSection == sec;
            var secStack = Column(6);
            var headRow = new DockPanel();
            headRow.Children.Add(new TextBlock
            {
                Text = sec.Title.ToUpperInvariant(),
                FontSize = compact ? 13 : 14.5,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.8,
                Foreground = style == "Minimalista" ? ink : accentBrush
            });
            secStack.Children.Add(headRow);
            secStack.Children.Add(new Border
            {
                Height = style == "Ejecutiva" ? 2 : 1,
                Background = style == "Minimalista" ? Brush.Parse("#222222") : accentBrush
            });
            if (sec.Detail != "")
                secStack.Children.Add(new TextBlock { Text = sec.Detail, FontSize = 12, FontStyle = FontStyle.Italic, Foreground = muted });
            if (!string.IsNullOrWhiteSpace(sec.PlainText))
                AddStyledBlocks(secStack, sec.PlainText, accentHex, ink, compact ? 12.5 : 13.5);
            foreach (var entry in sec.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Heading) && string.IsNullOrWhiteSpace(entry.Description)) continue;
                var entryCol = Column(2);
                entryCol.Margin = new Thickness(0, 4, 0, 2);
                var topLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                topLine.Children.Add(new TextBlock { Text = entry.Heading, FontSize = compact ? 13 : 14, FontWeight = FontWeight.Bold, Foreground = ink, TextWrapping = TextWrapping.Wrap });
                if (entry.Period != "")
                {
                    var perTb = new TextBlock { Text = entry.Period, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = accentBrush, Margin = new Thickness(8, 0, 0, 0) };
                    Grid.SetColumn(perTb, 1);
                    topLine.Children.Add(perTb);
                }
                entryCol.Children.Add(topLine);
                if (entry.Organization != "")
                    entryCol.Children.Add(new TextBlock { Text = entry.Organization, FontSize = 12.5, FontStyle = FontStyle.Italic, Foreground = muted });
                if (!string.IsNullOrWhiteSpace(entry.Description))
                    AddStyledBlocks(entryCol, entry.Description, accentHex, ink, compact ? 12 : 13);
                secStack.Children.Add(entryCol);
            }
            if (!highlighted) return secStack;
            return new Border
            {
                Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.06),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(10, 6, 4, 6),
                Child = secStack
            };
        }

        if (style == "BandaLateral")
        {
            var grid = new Grid { Width = 740, ColumnDefinitions = new ColumnDefinitions("230,*") };
            var sideCol = Column(12);
            sideCol.Margin = new Thickness(20, 26);
            if (work.CoverImage != "")
            {
                var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                if (Thumbnail(path, 220, 110) is { } avatar)
                {
                    avatar.Width = 104; avatar.Height = 104; avatar.Stretch = Stretch.UniformToFill;
                    sideCol.Children.Add(new Border { Width = 104, Height = 104, CornerRadius = new CornerRadius(52), ClipToBounds = true, BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center, Child = avatar });
                }
            }
            sideCol.Children.Add(new TextBlock { Text = name, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            if (profession != "")
                sideCol.Children.Add(new TextBlock { Text = profession, FontSize = 13, Foreground = Brush.Parse("#DCE6F0"), TextWrapping = TextWrapping.Wrap });
            if (work.Title != name)
                sideCol.Children.Add(new TextBlock { Text = work.Title, FontSize = 11.5, Foreground = Brush.Parse("#BFD0E0"), TextWrapping = TextWrapping.Wrap });
            sideCol.Children.Add(new Border { Height = 1, Background = Brush.Parse("#44FFFFFF") });
            foreach (var c in contacts)
                sideCol.Children.Add(new TextBlock { Text = "• " + c, FontSize = 12, Foreground = Brush.Parse("#EAF0F6"), TextWrapping = TextWrapping.Wrap });

            var sideChapters = ordered.Where(c => c.Title.Contains("Competencia", StringComparison.OrdinalIgnoreCase) || c.Title.Contains("Habilidad", StringComparison.OrdinalIgnoreCase) || c.Title.Contains("Idioma", StringComparison.OrdinalIgnoreCase) || c.Title.Contains("Stack", StringComparison.OrdinalIgnoreCase)).ToList();
            var mainChapters = ordered.Except(sideChapters).ToList();
            foreach (var sc in sideChapters)
            {
                sideCol.Children.Add(new Border { Height = 1, Background = Brush.Parse("#33FFFFFF"), Margin = new Thickness(0, 4) });
                sideCol.Children.Add(new TextBlock { Text = sc.Title.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#F2CC76") });
                if (!string.IsNullOrWhiteSpace(sc.PlainText))
                    sideCol.Children.Add(new TextBlock { Text = sc.PlainText.Replace("**", ""), FontSize = 11.5, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            }

            grid.Children.Add(new Border { Background = Brush.Parse("#223448"), Child = sideCol });
            var rightCol = Column(14);
            rightCol.Margin = new Thickness(26, 26);
            foreach (var mc in mainChapters) rightCol.Children.Add(RenderCvSection(mc));
            var rightHost = new Border { Background = Brushes.White, Child = rightCol };
            Grid.SetColumn(rightHost, 1);
            grid.Children.Add(rightHost);
            return new Border { Child = grid, BorderBrush = Brush.Parse("#C5CFD8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), BoxShadow = BoxShadows.Parse("0 12 28 #24000000"), HorizontalAlignment = HorizontalAlignment.Center };
        }

        var pageCol = Column(14);
        pageCol.Width = 740;
        pageCol.Margin = new Thickness(40, 34);

        if (style is "Moderna" or "Ejecutiva")
        {
            var bannerBg = style == "Ejecutiva" ? Brush.Parse("#1F2A36") : accentBrush;
            var headStack = Column(4);
            headStack.Children.Add(new TextBlock { Text = name, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            if (profession != "")
                headStack.Children.Add(new TextBlock { Text = profession, FontSize = 14.5, Foreground = Brush.Parse("#E6EEF6"), TextWrapping = TextWrapping.Wrap });
            if (work.Title != name)
                headStack.Children.Add(new TextBlock { Text = work.Title, FontSize = 12, Foreground = Brush.Parse("#C8D6E4") });
            if (contacts.Length > 0)
                headStack.Children.Add(new TextBlock { Text = string.Join("   ·   ", contacts), FontSize = 12, Foreground = Brush.Parse("#F5E3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            pageCol.Children.Add(new Border { Background = bannerBg, CornerRadius = new CornerRadius(4), Padding = new Thickness(22, 16), Child = headStack });
        }
        else
        {
            bool centered = style == "Académica";
            var headStack = Column(4);
            headStack.HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            headStack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 27,
                FontWeight = FontWeight.Bold,
                Foreground = style == "Minimalista" ? ink : accentBrush,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
            });
            if (profession != "")
            {
                headStack.Children.Add(new TextBlock
                {
                    Text = profession,
                    FontSize = 15,
                    Foreground = muted,
                    TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
                });
            }
            if (work.Title != name)
            {
                headStack.Children.Add(new TextBlock
                {
                    Text = work.Title,
                    FontSize = 12,
                    Foreground = muted,
                    TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
                });
            }
            if (contacts.Length > 0)
            {
                headStack.Children.Add(new TextBlock
                {
                    Text = string.Join("  ·  ", contacts),
                    FontSize = 12.5,
                    Foreground = muted,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
                });
            }
            headStack.Children.Add(new Border { Height = style == "Académica" ? 1.5 : 1, Background = style == "Minimalista" ? ink : accentBrush, Margin = new Thickness(0, 4, 0, 0) });
            pageCol.Children.Add(headStack);
        }

        if (style == "DosColumnas" && ordered.Count >= 3)
        {
            pageCol.Children.Add(RenderCvSection(ordered[0], true));
            var twoCols = new Grid { ColumnDefinitions = new ColumnDefinitions("1.15*,0.85*"), ColumnSpacing = 22 };
            var leftC = Column(12);
            var rightC = Column(12);
            for (int i = 1; i < ordered.Count; i++)
            {
                if (i <= (ordered.Count / 2)) leftC.Children.Add(RenderCvSection(ordered[i], true));
                else rightC.Children.Add(RenderCvSection(ordered[i], true));
            }
            twoCols.Children.Add(leftC);
            Grid.SetColumn(rightC, 1);
            twoCols.Children.Add(rightC);
            pageCol.Children.Add(twoCols);
        }
        else
        {
            foreach (var sec in ordered)
                pageCol.Children.Add(RenderCvSection(sec));
        }

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            BoxShadow = BoxShadows.Parse("0 12 28 #24000000"),
            Child = pageCol,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    internal Border BuildDocumentSectionCanvas(Work work, Chapter section, int decodeWidth = 780)
    {
        if (work.Type == "CV")
            return BuildResumeSheetCanvas(work, decodeWidth, section);

        var style = ResolveTemplateStyle(work);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var sections = WorldModel.Ordered(work).ToList();
        int pageNum = Math.Max(1, sections.IndexOf(section) + 1);

        bool isNovel = work.IsNovel;
        var paperBg = isNovel ? Brush.Parse("#FAF6EE") : Brushes.White;
        var ink = Brush.Parse("#1D2227");
        var muted = Brush.Parse("#545E68");

        var paper = Column(16);
        paper.Width = isNovel ? 650 : 740;
        paper.Margin = new Thickness(isNovel ? 54 : 44, 36);

        var runningHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        if (work.Type == "Apuntes")
        {
            var asig = work.DocumentFacts.GetValueOrDefault("Asignatura", work.Title);
            var clave = work.DocumentFacts.GetValueOrDefault("Clave de clase", work.Template);
            runningHeader.Children.Add(new TextBlock { Text = asig, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = muted });
            var rightTb = new TextBlock { Text = $"{clave}  ·  {work.Title}", FontSize = 12, Foreground = muted };
            Grid.SetColumn(rightTb, 1);
            runningHeader.Children.Add(rightTb);
            paper.Children.Add(runningHeader);
            paper.Children.Add(new Border { Height = 1.2, Background = Brush.Parse("#004785") });
        }
        else
        {
            runningHeader.Children.Add(new TextBlock
            {
                Text = work.Title,
                FontSize = 13,
                FontStyle = isNovel ? FontStyle.Italic : FontStyle.Normal,
                FontWeight = isNovel ? FontWeight.Normal : FontWeight.SemiBold,
                Foreground = muted
            });
            var rightMeta = new TextBlock
            {
                Text = isNovel ? (work.Author != "" ? work.Author : "Novela") : $"{work.Type}  ·  {work.Template}",
                FontSize = 12,
                Foreground = muted
            };
            Grid.SetColumn(rightMeta, 1);
            runningHeader.Children.Add(rightMeta);
            paper.Children.Add(runningHeader);
            paper.Children.Add(new Border { Height = 1, Background = Brush.Parse("#D8D2C5") });
        }

        if (work.Type == "Carta" && pageNum == 1)
        {
            var placeDate = work.DocumentFacts.GetValueOrDefault("Lugar y fecha", "").Trim();
            if (placeDate != "")
                paper.Children.Add(new TextBlock { Text = placeDate, FontSize = 13.5, Foreground = muted, TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Right });
            var subject = work.DocumentFacts.GetValueOrDefault("Asunto", work.Title).Trim();
            paper.Children.Add(new TextBlock { Text = "ASUNTO: " + subject, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = accentBrush, TextWrapping = TextWrapping.Wrap });
            var rec = work.DocumentFacts.GetValueOrDefault("Destinatario", "").Trim();
            var addr = work.DocumentFacts.GetValueOrDefault("Dirección", "").Trim();
            if (rec != "" || addr != "")
            {
                var recStack = Column(2);
                recStack.Children.Add(new TextBlock { Text = "Dirigido a:", FontSize = 13, Foreground = muted });
                if (rec != "") recStack.Children.Add(new TextBlock { Text = rec, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = ink });
                if (addr != "") recStack.Children.Add(new TextBlock { Text = addr, FontSize = 13.5, Foreground = ink });
                recStack.Children.Add(new TextBlock { Text = "PRESENTE.", FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = ink });
                paper.Children.Add(recStack);
            }
        }

        if (isNovel)
        {
            var chapHeader = Column(6);
            chapHeader.Margin = new Thickness(0, 12, 0, 8);
            chapHeader.HorizontalAlignment = HorizontalAlignment.Center;
            chapHeader.Children.Add(new TextBlock
            {
                Text = section.Level == "Arc" ? "PARTE / ARCO" : $"CAPÍTULO {pageNum}",
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 2,
                Foreground = Brush.Parse("#8C6D3B"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            chapHeader.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = 32,
                FontWeight = FontWeight.Bold,
                Foreground = ink,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            chapHeader.Children.Add(new Border { Width = 96, Height = 1.2, Background = Brush.Parse("#C5A368"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4) });
            paper.Children.Add(chapHeader);
        }
        else if (work.Type == "Apuntes")
        {
            var secHead = Column(4);
            secHead.Children.Add(new TextBlock
            {
                Text = $"{pageNum}.  {section.Title}",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#004785"),
                TextWrapping = TextWrapping.Wrap
            });
            secHead.Children.Add(new Border { Height = 2.4, Background = Brush.Parse("#22714B") });
            paper.Children.Add(secHead);
        }
        else
        {
            var secHead = Column(4);
            secHead.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontSize = 26,
                FontWeight = FontWeight.Bold,
                Foreground = accentBrush,
                TextWrapping = TextWrapping.Wrap
            });
            secHead.Children.Add(new Border { Height = 1.5, Background = accentBrush });
            paper.Children.Add(secHead);
        }

        if (section.Detail != "")
        {
            var detailTitle = work.Type == "Apuntes" ? "Conceptos clave y preguntas guía" : EditorialTemplate.DetailLabel(work.Type);
            var detailBox = Column(3);
            detailBox.Children.Add(new TextBlock { Text = detailTitle, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = accentBrush });
            detailBox.Children.Add(new TextBlock { Text = section.Detail, FontSize = 13.5, FontStyle = FontStyle.Italic, Foreground = ink, TextWrapping = TextWrapping.Wrap });
            paper.Children.Add(new Border
            {
                Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.10),
                BorderBrush = EditionPalette.Mix(accentHex, "#FFFFFF", 0.35),
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(12, 8),
                Child = detailBox
            });
        }

        void Illustration(int index, Panel destination, bool small = false)
        {
            var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(section.Images[index]));
            try
            {
                using var file = File.OpenRead(path);
                var bitmap = Bitmap.DecodeToWidth(file, decodeWidth);
                var layout = Illustrations.Layout(section, index);
                var img = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    MaxHeight = layout == "Página completa" ? 560 : layout.StartsWith("Ajustar") ? 230 : 400,
                    HorizontalAlignment = layout.EndsWith("izquierda") ? HorizontalAlignment.Left : layout.EndsWith("derecha") ? HorizontalAlignment.Right : HorizontalAlignment.Center
                };
                if (small) img.MaxHeight = 165;
                img.DetachedFromVisualTree += (_, _) => bitmap.Dispose();
                destination.Children.Add(img);
            }
            catch (Exception e) when (e is IOException or ArgumentException)
            {
                destination.Children.Add(new TextBlock { Text = "Ilustración no disponible", Foreground = muted });
            }
            if (section.ImageCaptions.TryGetValue(index, out var caption) && caption != "")
            {
                destination.Children.Add(new TextBlock
                {
                    Text = caption,
                    FontSize = 12.5,
                    FontStyle = FontStyle.Italic,
                    Foreground = muted,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        var contentTarget = paper;
        if (!isNovel && work.Type == "Apuntes" && style == "Cornell" && section.Detail != "")
        {
            var cornellGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("190,*"), ColumnSpacing = 16 };
            var cuePanel = Column(6);
            cuePanel.Children.Add(new TextBlock { Text = "COLUMNA DE CLAVES", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#004785") });
            foreach (var cue in section.Detail.Split(['·', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                cuePanel.Children.Add(new TextBlock { Text = "• " + cue, FontSize = 12.5, Foreground = ink, TextWrapping = TextWrapping.Wrap });
            cornellGrid.Children.Add(new Border
            {
                Background = Brush.Parse("#F3F7FB"),
                BorderBrush = Brush.Parse("#C9D9E8"),
                BorderThickness = new Thickness(0, 0, 1.5, 0),
                Padding = new Thickness(10, 10, 12, 10),
                Child = cuePanel
            });
            var notesCol = Column(12);
            Grid.SetColumn(notesCol, 1);
            cornellGrid.Children.Add(notesCol);
            paper.Children.Add(cornellGrid);
            contentTarget = notesCol;
        }

        var rows = Illustrations.Rows(section).ToList();
        var used = new HashSet<int>();
        int start = 0;
        if (rows.Count == 0 && section.Images.Count > 0 && Illustrations.Layout(section, 0).StartsWith("Ajustar"))
        {
            Illustration(0, contentTarget);
            used.Add(0);
        }
        foreach (var row in rows)
        {
            AddStyledBlocks(contentTarget, section.PlainText[start..row[0].Start], accentHex, ink, isNovel ? 16.5 : 14.5, isNovel);
            if (row.Count == 1)
            {
                int index = row[0].Image;
                used.Add(index);
                Illustration(index, contentTarget);
            }
            else
            {
                var strip = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", row.Select(_ => "*"))), ColumnSpacing = 12 };
                for (int col = 0; col < row.Count; col++)
                {
                    int index = row[col].Image;
                    used.Add(index);
                    var cell = Column(4);
                    Illustration(index, cell, true);
                    Grid.SetColumn(cell, col);
                    strip.Children.Add(cell);
                }
                contentTarget.Children.Add(strip);
            }
            start = row[^1].End;
        }
        AddStyledBlocks(contentTarget, section.PlainText[start..], accentHex, ink, isNovel ? 16.5 : 14.5, isNovel);
        for (int i = 0; i < section.Images.Count; i++)
            if (!used.Contains(i)) Illustration(i, contentTarget);

        if (section.Summary != "")
        {
            var sumCol = Column(4);
            sumCol.Children.Add(new TextBlock
            {
                Text = work.Type == "Apuntes" ? "Síntesis de la sesión" : "Síntesis / Conclusión de sección",
                FontSize = 12.5,
                FontWeight = FontWeight.Bold,
                Foreground = accentBrush
            });
            AddStyledBlocks(sumCol, section.Summary, accentHex, ink, 13.5);
            paper.Children.Add(new Border
            {
                Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.08),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(14, 10),
                Child = sumCol
            });
        }

        paper.Children.Add(new Border { Height = 1, Background = Brush.Parse("#E0E4E8"), Margin = new Thickness(0, 8, 0, 2) });
        paper.Children.Add(new TextBlock
        {
            Text = work.Type == "Apuntes" ? $"{pageNum}" : $"—  {pageNum}  —",
            FontSize = 13,
            FontWeight = work.Type == "Apuntes" ? FontWeight.Bold : FontWeight.Normal,
            Foreground = work.Type == "Apuntes" ? Brush.Parse("#004785") : muted,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        Control framedSheet = paper;
        if (work.FrameStyle is "Filete" or "Doble filete" or "Esquinas")
        {
            framedSheet = new Border
            {
                Margin = new Thickness(10),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(work.FrameStyle == "Doble filete" ? 2 : 1),
                Child = paper
            };
        }

        return new Border
        {
            Background = paperBg,
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            BoxShadow = BoxShadows.Parse("0 12 28 #24000000"),
            Child = framedSheet,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    void AddStyledBlocks(Panel paper, string text, string accentHex, IBrush inkBrush, double fontSize, bool literary = false)
    {
        var accentBrush = Brush.Parse(accentHex);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line is "[[Página]]" or "[[Índice]]")
            {
                paper.Children.Add(new Border { Height = 1, Background = Brush.Parse("#D5DCE2"), Margin = new Thickness(0, 6) });
                continue;
            }
            if (line.StartsWith("[[Paleta:") && line.EndsWith("]]"))
            {
                var items = line[9..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (items.Length > 0)
                {
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", items.Select(_ => "*"))), ColumnSpacing = 8, Margin = new Thickness(0, 6) };
                    for (int c = 0; c < items.Length; c++)
                    {
                        var m = Regex.Match(items[c], @"^#?([0-9A-Fa-f]{6})(?:\s+(.*))?$");
                        var hex = "#" + (m.Success ? m.Groups[1].Value.ToUpperInvariant() : "666666");
                        var lbl = m.Success ? m.Groups[2].Value.Trim() : items[c];
                        var cell = Column(3);
                        cell.Children.Add(new Border { Height = 32, Background = Color.TryParse(hex, out var col) ? new SolidColorBrush(col) : Brushes.Gray, CornerRadius = new CornerRadius(3), BorderBrush = Brush.Parse("#BFC7CE"), BorderThickness = new Thickness(1) });
                        cell.Children.Add(new TextBlock { Text = hex, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = inkBrush, HorizontalAlignment = HorizontalAlignment.Center });
                        if (lbl != "") cell.Children.Add(new TextBlock { Text = lbl, FontSize = 11, Foreground = Brush.Parse("#525C66"), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
                        Grid.SetColumn(cell, c);
                        grid.Children.Add(cell);
                    }
                    paper.Children.Add(grid);
                }
                continue;
            }
            if (line.StartsWith("[[Firmas:") && line.EndsWith("]]"))
            {
                var signers = line[9..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (signers.Length > 0)
                {
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", signers.Select(_ => "*"))), ColumnSpacing = 20, Margin = new Thickness(0, 18, 0, 6) };
                    for (int c = 0; c < signers.Length; c++)
                    {
                        var cell = Column(4);
                        cell.Children.Add(new Border { Height = 1, Background = inkBrush });
                        cell.Children.Add(new TextBlock { Text = signers[c], FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = inkBrush, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
                        Grid.SetColumn(cell, c);
                        grid.Children.Add(cell);
                    }
                    paper.Children.Add(grid);
                }
                continue;
            }
            if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
            {
                var rows = new List<string[]>();
                do
                {
                    var cells = lines[i].Trim().Trim('|').Split('|');
                    if (!cells.All(c => Regex.IsMatch(c.Trim(), @"^:?-+:?$"))) rows.Add(cells);
                    i++;
                } while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--;
                if (rows.Count == 0) continue;
                int columns = rows.Max(r => r.Length);
                var table = Column(0);
                for (int r = 0; r < rows.Count; r++)
                {
                    bool header = r == 0 && rows.Count > 1;
                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns))),
                        Background = header ? EditionPalette.Mix(accentHex, "#FFFFFF", 0.16) : (r % 2 == 1 ? Brush.Parse("#F7F9FB") : Brushes.White)
                    };
                    for (int c = 0; c < columns; c++)
                    {
                        var tb = StyledRichLine(c < rows[r].Length ? rows[r][c].Trim() : "", header ? 13 : 12.5, header, header ? accentBrush : inkBrush, literary);
                        var cell = new Border
                        {
                            BorderBrush = Brush.Parse("#CFD7DE"),
                            BorderThickness = new Thickness(0.6),
                            Padding = new Thickness(8, 5),
                            Child = tb
                        };
                        Grid.SetColumn(cell, c);
                        row.Children.Add(cell);
                    }
                    table.Children.Add(row);
                }
                paper.Children.Add(table);
                continue;
            }
            var quote = line.StartsWith("> ");
            var sub3 = line.StartsWith("### ");
            var heading = line.StartsWith("## ");
            var bullet = line.StartsWith("- ");
            var numMatch = Regex.Match(line, @"^([1-9][0-9]*)\.\s+(.*)$");
            var content = sub3 ? line[4..] : heading ? line[3..] : (quote || bullet) ? line[2..] : numMatch.Success ? numMatch.Groups[2].Value : line;
            var block = StyledRichLine(
                bullet ? "•  " + content : numMatch.Success ? numMatch.Groups[1].Value + ".  " + content : content,
                heading ? fontSize + 5 : sub3 ? fontSize + 2 : fontSize,
                heading || sub3,
                heading || sub3 ? accentBrush : inkBrush,
                literary);
            if (quote) block.FontStyle = FontStyle.Italic;
            Control item = quote
                ? new Border { BorderBrush = accentBrush, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 4, 0, 4), Child = block }
                : block;
            paper.Children.Add(item);
        }
    }

    TextBlock StyledRichLine(string content, double size, bool bold, IBrush fg, bool literary)
    {
        var family = literary ? new FontFamily("Georgia, Liberation Serif, serif") : WritingFamily;
        var block = new TextBlock
        {
            FontFamily = family,
            FontSize = size,
            LineHeight = size * 1.54,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
        };
        foreach (var part in Regex.Split(content, @"(\*\*[^*]+\*\*|\*[^*]+\*)"))
        {
            if (part.Length > 4 && part.StartsWith("**") && part.EndsWith("**")) block.Inlines!.Add(new Run(part[2..^2]) { FontWeight = FontWeight.Bold });
            else if (part.Length > 2 && part.StartsWith('*') && part.EndsWith('*')) block.Inlines!.Add(new Run(part[1..^1]) { FontStyle = FontStyle.Italic });
            else block.Inlines!.Add(new Run(part));
        }
        return block;
    }

    void ReadSection(Chapter? selected)
    {
        Navigate("Lectura");
        var work = current!;
        var sections = WorldModel.Ordered(work).ToList();
        if (work.Type == "Presentación")
        {
            ReadPresentation(work, sections, selected);
            return;
        }
        var section = selected ?? sections.FirstOrDefault();
        var root = new DockPanel();
        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        top.Children.Add(Action("‹ Editar", () => { chapter = section; Editor(); }));
        top.Children.Add(Action("Vista previa de impresión", async () => await Preview()));
        top.Children.Add(QuietAction("Exportar PDF…", async () => await Export(true)));

        if (section != null && sections.Count > 1)
        {
            int idx = sections.IndexOf(section);
            var prev = QuietAction("‹ Anterior", () => { if (idx > 0) ReadSection(sections[idx - 1]); });
            prev.IsEnabled = idx > 0;
            var next = QuietAction("Siguiente ›", () => { if (idx + 1 < sections.Count) ReadSection(sections[idx + 1]); });
            next.IsEnabled = idx + 1 < sections.Count;
            top.Children.Add(prev);
            var picker = new ComboBox { ItemsSource = sections, SelectedItem = section, MaxWidth = 300 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is Chapter target && target != section) ReadSection(target); };
            top.Children.Add(picker);
            top.Children.Add(next);
        }
        else if (section != null)
        {
            var picker = new ComboBox { ItemsSource = sections, SelectedItem = section, MaxWidth = 300 };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is Chapter target && target != section) ReadSection(target); };
            top.Children.Add(picker);
        }

        var badgeText = work.IsNovel
            ? $"  Vista de libro ({work.Publication.Paper})  ·  Portada: {(EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0])}"
            : $"  Vista final de {work.Type}  ·  Plantilla: {work.Template}";
        top.Children.Add(Kicker(badgeText));

        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var stage = Column(24);
        stage.Margin = new Thickness(12, 8, 12, 32);
        stage.HorizontalAlignment = HorizontalAlignment.Center;

        if (work.Type == "CV")
        {
            stage.Children.Add(BuildResumeSheetCanvas(work, 780, section));
        }
        else
        {
            var coverExpander = new Expander
            {
                Header = work.IsNovel
                    ? $"Portada de novela a hoja completa con blur ({(EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0])})"
                    : $"Portada e índice del formato ({work.Template})",
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 760,
                Content = BuildDocumentCoverCanvas(work, 780, ch => { chapter = ch; ReadSection(ch); })
            };
            stage.Children.Add(coverExpander);
            if (section != null)
                stage.Children.Add(BuildDocumentSectionCanvas(work, section, 780));
        }

        root.Children.Add(new ScrollViewer { Content = stage });
        page.Content = root;
    }
}
