using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Nimbo.Core;
using System.Text.RegularExpressions;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    internal static readonly string[] SlideLayouts =
    [
        "Portada",
        "Agenda / Índice",
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
        "Mosaico de 4 láminas",
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
        var (navyHex, tealHex, goldHex) = work.PrintAccent switch
        {
            "Vino" => ("#3A1F2D", "#7A304D", "#E6B472"),
            "Oliva" => ("#243528", "#486844", "#DFB06A"),
            "Grafito" => ("#22272E", "#485460", "#D8A868"),
            "Petróleo" => ("#1C3342", "#245966", "#E6AA68"),
            "Color de portada" when Color.TryParse(work.CoverColor, out _) => (work.CoverColor, "#2C6467", "#E6AA68"),
            _ => ("#243750", "#2C6467", "#E6AA68")
        };
        var navyBrush = Brush.Parse(navyHex);
        var creamBrush = Brush.Parse("#E4DAD3");
        var cardBrush = Brush.Parse("#EFECE7");
        var goldBrush = Brush.Parse(goldHex);
        var tealBrush = Brush.Parse(tealHex);
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

        Control FitBox(Control content, double maxWidth, double maxHeight, VerticalAlignment vAlign = VerticalAlignment.Top, HorizontalAlignment hAlign = HorizontalAlignment.Left) => new Border
        {
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = hAlign,
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

        if (layout == "Agenda / Índice")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Objetivo de la sesión", slide.Summary);
                bottomNote.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            double areaH = slide.Summary != "" ? 358 : 418;
            var agendaItems = ParseStructuredCards(slide.PlainText);
            if (agendaItems.Count == 0)
            {
                var orderedSlides = WorldModel.Ordered(work).Where(s => s != slide && s.Layout != "Portada").Take(6).ToList();
                for (int i = 0; i < orderedSlides.Count; i++)
                    agendaItems.Add((orderedSlides[i].Title, orderedSlides[i].Detail != "" ? orderedSlides[i].Detail : orderedSlides[i].Layout, orderedSlides[i].Summary != "" ? [orderedSlides[i].Summary] : []));
            }
            if (agendaItems.Count == 0)
                agendaItems.Add(("1. Introducción y alcance", "Apertura", ["Planteamiento de objetivos y estructura general."]));

            int aCount = Math.Clamp(agendaItems.Count, 1, 6);
            int aCols = aCount >= 4 ? 2 : 1;
            int aRows = (int)Math.Ceiling(aCount / (double)aCols);
            var aGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", aCols))),
                RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", aRows))),
                ColumnSpacing = 16,
                RowSpacing = 10
            };
            double cellW = (884 - 16 * (aCols - 1)) / aCols - 24;
            double cellH = (areaH - 10 * (aRows - 1)) / aRows - 16;
            for (int i = 0; i < aCount; i++)
            {
                var item = agendaItems[i];
                var rowDock = new Grid { ColumnDefinitions = new ColumnDefinitions("52,*"), ColumnSpacing = 12 };
                rowDock.Children.Add(new Border
                {
                    Width = 48,
                    Height = 44,
                    Background = i % 2 == 0 ? navyBrush : tealBrush,
                    CornerRadius = new CornerRadius(5),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = $"{i + 1:D2}", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                });
                var textCol = Column(2);
                textCol.VerticalAlignment = VerticalAlignment.Center;
                textCol.Children.Add(new TextBlock { Text = item.Title, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextWrapping = TextWrapping.Wrap });
                var desc = item.Subtitle != "" ? item.Subtitle + (item.Lines.Count > 0 ? " · " + string.Join(" ", item.Lines) : "") : string.Join(" · ", item.Lines);
                if (desc != "")
                    textCol.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = mutedBrush, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(textCol, 1);
                rowDock.Children.Add(textCol);

                var card = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = goldBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8),
                    ClipToBounds = true,
                    Child = FitBox(rowDock, cellW, cellH, VerticalAlignment.Center)
                };
                Grid.SetRow(card, i / aCols);
                Grid.SetColumn(card, i % aCols);
                aGrid.Children.Add(card);
            }
            slideGrid.Children.Add(aGrid);
            root.Children.Add(slideGrid);
            return new Border { Child = root, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        }

        if (layout is "Tres columnas" or "Cuatro columnas" or "Cuadrícula 2x2" or "Métricas e indicadores (KPIs)" or "Línea de tiempo / Roadmap" or "Flujo de proceso (Pipeline)" or "Arquitectura en capas" or "Embudo o pirámide estratégica" or "Checklist de validación" or "Comparativa (Antes / Después)" || (layout == "Dos columnas" && slide.Images.Count == 0 && ParseStructuredCards(slide.PlainText).Count >= 2))
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
                int colCount = layout is "Comparativa (Antes / Después)" or "Dos columnas" ? 2 : (layout == "Cuatro columnas" ? 4 : 3);
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

        if (layout == "Galería triple" || layout == "Mosaico de 4 láminas")
        {
            if (slide.Summary != "")
            {
                var bottomNote = SummaryBanner("Síntesis visual", slide.Summary);
                bottomNote.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(bottomNote, Dock.Bottom);
                slideGrid.Children.Add(bottomNote);
            }
            if (layout == "Mosaico de 4 láminas")
            {
                double cellH = slide.Summary != "" ? 168 : 196;
                double picH = cellH - 28;
                var mGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("*,*"),
                    ColumnSpacing = 14,
                    RowSpacing = 10
                };
                for (int i = 0; i < 4; i++)
                {
                    var mCard = Column(4);
                    if (i < slide.Images.Count && SlidePicture(i, 424, picH, false, "#FFFFFF", 0.9) is { } pic)
                        mCard.Children.Add(pic);
                    else
                        mCard.Children.Add(new Border { Width = 424, Height = picH, Background = Brushes.White, BorderBrush = frameBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
                    var cap = slide.ImageCaptions.GetValueOrDefault(i, $"Lámina {i + 1}");
                    mCard.Children.Add(new TextBlock { Text = cap, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                    var wrap = FitBox(mCard, 430, cellH, VerticalAlignment.Center, HorizontalAlignment.Center);
                    Grid.SetRow(wrap, i / 2);
                    Grid.SetColumn(wrap, i % 2);
                    mGrid.Children.Add(wrap);
                }
                slideGrid.Children.Add(mGrid);
            }
            else
            {
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
                    var wrap = FitBox(gCard, 278, picH + 34, VerticalAlignment.Center, HorizontalAlignment.Center);
                    Grid.SetColumn(wrap, i);
                    gGrid.Children.Add(wrap);
                }
                slideGrid.Children.Add(gGrid);
            }
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
            if (SlidePicture(idx, 884, 366, false, "#243750", 1.2) is { } heroPic)
                stage.Children.Add(heroPic);
            var cap = slide.ImageCaptions.GetValueOrDefault(idx, "");
            var footer = string.Join(" — ", new[] { cap, slide.PlainText.Replace('\n', ' ').Trim(), slide.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (footer != "")
                stage.Children.Add(new TextBlock { Text = footer, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
            slideGrid.Children.Add(FitBox(stage, 884, 418, VerticalAlignment.Center, HorizontalAlignment.Center));
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
            var leftBlocks = RenderRichBlocks(slide.PlainText, 380);
            cols.Children.Add(FitBox(leftBlocks, 424, 414));

            var rightCol = new DockPanel();
            if (slide.Summary != "")
            {
                var syn = SummaryBanner("Síntesis visual", slide.Summary);
                syn.Margin = new Thickness(0, 8, 0, 0);
                DockPanel.SetDock(syn, Dock.Bottom);
                rightCol.Children.Add(syn);
            }

            var imgs = remainingImages.Take(6).ToList();
            double availW = 436;
            double availH = slide.Summary != "" ? 336 : 408;

            if (layout == "Moodboard y landmark" && imgs.Count >= 2)
            {
                var landStack = Column(8);
                int heroIdx = imgs[0];
                var heroCap = slide.ImageCaptions.GetValueOrDefault(heroIdx, "Landmark principal");
                double heroH = availH * 0.56;
                var heroCard = Column(3);
                if (SlidePicture(heroIdx, availW - 8, heroH - 22, false, "#FFFFFF", 0.9) is { } hPic)
                    heroCard.Children.Add(hPic);
                heroCard.Children.Add(new TextBlock { Text = heroCap, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = navyBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                landStack.Children.Add(new Border { Background = Brushes.White, BorderBrush = goldBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(3), Child = heroCard });

                var subImgs = imgs.Skip(1).Take(3).ToList();
                var subGrid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", subImgs.Count))), ColumnSpacing = 8 };
                double subW = (availW - 8 * (subImgs.Count - 1)) / subImgs.Count;
                double subH = availH - heroH - 12;
                for (int s = 0; s < subImgs.Count; s++)
                {
                    int sIdx = subImgs[s];
                    var sCap = slide.ImageCaptions.GetValueOrDefault(sIdx, "");
                    var sCol = Column(2);
                    if (SlidePicture(sIdx, subW - 6, subH - (sCap != "" ? 22 : 8), false, "#FFFFFF", 0) is { } sPic)
                        sCol.Children.Add(sPic);
                    if (sCap != "")
                        sCol.Children.Add(new TextBlock { Text = sCap, Width = subW - 8, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                    var sBorder = new Border { Background = Brushes.White, BorderBrush = frameBorderBrush, BorderThickness = new Thickness(0.8), CornerRadius = new CornerRadius(4), Padding = new Thickness(2), Child = sCol };
                    Grid.SetColumn(sBorder, s);
                    subGrid.Children.Add(sBorder);
                }
                landStack.Children.Add(subGrid);
                rightCol.Children.Add(FitBox(landStack, availW, availH));
            }
            else
            {
                int count = Math.Max(1, imgs.Count);
                int gCols = count >= 5 ? 3 : 2;
                int gRows = Math.Max(1, (int)Math.Ceiling(count / (double)gCols));
                double gap = 8;
                double cellW = (availW - gap * (gCols - 1)) / gCols;
                double cellH = (availH - gap * (gRows - 1)) / gRows;

                var mosaicGrid = new Grid
                {
                    Width = availW,
                    ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", gCols))),
                    RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", gRows))),
                    ColumnSpacing = gap,
                    RowSpacing = gap
                };
                for (int i = 0; i < imgs.Count; i++)
                {
                    int imgIdx = imgs[i];
                    var cap = slide.ImageCaptions.GetValueOrDefault(imgIdx, "");
                    double picH = cap != "" ? Math.Max(36, cellH - 26) : Math.Max(36, cellH - 10);
                    var tileStack = Column(2);
                    tileStack.HorizontalAlignment = HorizontalAlignment.Center;
                    tileStack.VerticalAlignment = VerticalAlignment.Center;
                    if (SlidePicture(imgIdx, cellW - 8, picH, false, "#FFFFFF", 0) is { } pic)
                        tileStack.Children.Add(pic);
                    if (cap != "")
                        tileStack.Children.Add(new TextBlock { Text = cap, Width = cellW - 10, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = mutedBrush, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
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
                rightCol.Children.Add(FitBox(mosaicGrid, availW, availH));
            }
            Grid.SetColumn(rightCol, 1);
            cols.Children.Add(rightCol);
            slideGrid.Children.Add(cols);
        }
        else if (layout == "Plano y leyenda" || remainingImages.Count > 0 || (layout == "Dos columnas" && slide.Summary != ""))
        {
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("0.96*,1.04*"), ColumnSpacing = 22 };
            var leftBlocks = RenderRichBlocks(slide.PlainText, 380);
            cols.Children.Add(FitBox(leftBlocks, 424, 414));
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
                double boxH = slide.Summary != "" ? 332 : 404;
                double imgH = cap != "" ? boxH - 32 : boxH - 12;
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
                rightCol.Children.Add(FitBox(imgFrame, 434, boxH));
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

    readonly Dictionary<string, double> filmstripHorizontalOffsets = [];

    void CaptureCurrentFilmstripOffset(string workId)
    {
        if (page.Content is Control rootCtrl)
        {
            var existing = rootCtrl.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(s => s.Name == "FilmstripScrollViewer");
            if (existing != null && Equals(existing.Tag, "Restored"))
                filmstripHorizontalOffsets[workId] = existing.Offset.X;
        }
    }

    static void RestoreFilmstripOffset(ScrollViewer viewer, double targetX)
    {
        if (targetX <= 0)
        {
            viewer.Tag = "Restored";
            return;
        }
        viewer.Offset = new Vector(targetX, 0);
        bool restored = false;
        void Apply()
        {
            if (restored) return;
            if (viewer.Extent.Width > 0 && viewer.Viewport.Width > 0)
            {
                double maxOff = Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width);
                viewer.Offset = new Vector(Math.Min(targetX, maxOff), 0);
                viewer.Tag = "Restored";
                restored = true;
            }
        }
        viewer.AttachedToVisualTree += (_, _) => Apply();
        viewer.LayoutUpdated += (_, _) => Apply();
    }

    void ReadPresentation(Work work, List<Chapter> sections, Chapter? selected)
    {
        CaptureCurrentFilmstripOffset(work.Id);
        var section = selected ?? sections.FirstOrDefault();
        int currentIndex = section != null ? Math.Max(0, sections.IndexOf(section)) : 0;
        var root = new DockPanel { Focusable = true };
        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        top.Children.Add(Action("‹ Editar diapositiva", () => { if (sections.Count > 0) chapter = sections[currentIndex]; Editor(); }));
        top.Children.Add(Action("Vista previa PDF", async () => await Preview()));
        top.Children.Add(QuietAction("Exportar PDF…", async () => await Export(true)));

        Button? prevBtn = null, nextBtn = null;
        ComboBox? pickerBox = null;
        TextBlock? kickerTb = null;
        var stageBox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            MaxWidth = 1240,
            Margin = new Thickness(8, 4, 8, 8)
        };
        var thumbFrames = new List<(Border Frame, TextBlock Label)>();
        ScrollViewer? stripBar = null;
        bool suppressPickerEvent = false;

        void SelectSlide(int newIdx)
        {
            if (sections.Count == 0) return;
            double keepX = (stripBar != null && Equals(stripBar.Tag, "Restored"))
                ? stripBar.Offset.X
                : filmstripHorizontalOffsets.GetValueOrDefault(work.Id, 0);
            currentIndex = Math.Clamp(newIdx, 0, sections.Count - 1);
            var active = sections[currentIndex];
            chapter = active;
            RememberSection(work, active);
            if (prevBtn != null) prevBtn.IsEnabled = currentIndex > 0;
            if (nextBtn != null) nextBtn.IsEnabled = currentIndex + 1 < sections.Count;
            if (pickerBox != null && pickerBox.SelectedIndex != currentIndex)
            {
                suppressPickerEvent = true;
                pickerBox.SelectedIndex = currentIndex;
                suppressPickerEvent = false;
            }
            if (kickerTb != null)
            {
                var lay = SlideLayouts.Contains(active.Layout) ? active.Layout : "Texto e imagen";
                kickerTb.Text = $"  MODO PRESENTACIÓN 16:9  ·  DIAPOSITIVA {currentIndex + 1} DE {sections.Count}  ·  {lay.ToUpperInvariant()}";
            }
            for (int i = 0; i < thumbFrames.Count; i++)
            {
                bool isCur = i == currentIndex;
                thumbFrames[i].Frame.BorderBrush = isCur ? Accent : Edge;
                thumbFrames[i].Frame.BorderThickness = new Thickness(isCur ? 2.5 : 1);
                thumbFrames[i].Label.Foreground = isCur ? Accent : Foreground;
                thumbFrames[i].Label.FontWeight = isCur ? FontWeight.SemiBold : FontWeight.Normal;
            }
            stageBox.Child = BuildSlideCanvas(work, active, 960);
            if (stripBar != null)
            {
                filmstripHorizontalOffsets[work.Id] = keepX;
                stripBar.Offset = new Vector(keepX, 0);
            }
        }

        root.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Left or Key.PageUp && currentIndex > 0) { SelectSlide(currentIndex - 1); e.Handled = true; }
            else if (e.Key is Key.Right or Key.PageDown && currentIndex + 1 < sections.Count) { SelectSlide(currentIndex + 1); e.Handled = true; }
        };

        if (sections.Count > 0)
        {
            prevBtn = Action("‹ Anterior", () => SelectSlide(currentIndex - 1));
            nextBtn = Action("Siguiente ›", () => SelectSlide(currentIndex + 1));
            top.Children.Add(prevBtn);
            pickerBox = new ComboBox { ItemsSource = sections, SelectedIndex = currentIndex, MaxWidth = 300 };
            pickerBox.SelectionChanged += (_, _) =>
            {
                if (!suppressPickerEvent && pickerBox.SelectedIndex >= 0 && pickerBox.SelectedIndex != currentIndex)
                    SelectSlide(pickerBox.SelectedIndex);
            };
            top.Children.Add(pickerBox);
            top.Children.Add(nextBtn);
            kickerTb = Kicker("");
            top.Children.Add(kickerTb);
        }
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        if (sections.Count > 0)
        {
            var filmstrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(4, 10, 4, 4) };
            for (int i = 0; i < sections.Count; i++)
            {
                int slideIdx = i;
                var item = sections[i];
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
                    BorderBrush = Edge,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(1)
                };
                miniCard.Children.Add(frame);
                var lbl = Label($"{i + 1:D2} · {item.Title}", 11.5);
                lbl.MaxWidth = 176; lbl.TextWrapping = TextWrapping.NoWrap; lbl.TextTrimming = TextTrimming.CharacterEllipsis;
                miniCard.Children.Add(lbl);
                thumbFrames.Add((frame, lbl));
                var btn = new Button
                {
                    Content = miniCard,
                    Focusable = false,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                btn.Click += (_, _) => SelectSlide(slideIdx);
                filmstrip.Children.Add(btn);
            }
            stripBar = new ScrollViewer
            {
                Name = "FilmstripScrollViewer",
                BringIntoViewOnFocusChange = false,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = filmstrip
            };
            var initialX = filmstripHorizontalOffsets.GetValueOrDefault(work.Id, 0);
            RestoreFilmstripOffset(stripBar, initialX);
            stripBar.ScrollChanged += (_, _) =>
            {
                if (stripBar.IsLoaded && Equals(stripBar.Tag, "Restored"))
                    filmstripHorizontalOffsets[work.Id] = stripBar.Offset.X;
            };
            DockPanel.SetDock(stripBar, Dock.Bottom);
            root.Children.Add(stripBar);
            root.Children.Add(stageBox);
            SelectSlide(currentIndex);
        }

        page.Content = root;
    }

    internal sealed record DocumentPageItem(int PageNumber, string Label, Chapter? Section, Func<Border> BuildSheet);

    int documentReadingMode = 2; // 1 = 1 hoja, 2 = 2 hojas (doble página), 3 = todas las hojas
    double documentZoomScale = 1.0;

    static string ResolveTemplateStyle(Work work)
    {
        var format = Format.All.FirstOrDefault(f => f.Name == work.Type);
        return EditorialTemplate.Resolve(format, work.Template).Style;
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
            "Color de portada" when Color.TryParse(work.CoverColor, out _) => work.CoverColor,
            _ => raw
        };
    }

    static string ToRoman(int number)
    {
        if (number <= 0) return "I";
        (int Val, string Sym)[] map = [(10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new System.Text.StringBuilder();
        foreach (var (val, sym) in map)
            while (number >= val) { sb.Append(sym); number -= val; }
        return sb.ToString();
    }

    internal Border BuildNovelCoverCanvas(Work work, bool backCover = false, int decodeWidth = 760)
    {
        const double W = 520, H = 735;
        var clothHex = Color.TryParse(work.CoverColor, out _) ? work.CoverColor : "#284B52";
        var coverCanvas = new Canvas { Width = W, Height = H, ClipToBounds = true, Background = Brush.Parse(clothHex) };
        var imageKey = backCover ? (work.BackImage != "" ? work.BackImage : work.CoverImage) : work.CoverImage;

        Control BuildArtLayer(double blurRadius)
        {
            Bitmap? bmp = null;
            if (imageKey != "")
            {
                var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(imageKey));
                bmp = GetCachedBitmap(path, decodeWidth);
            }
            bmp ??= Observatorio.Value;
            var layerGrid = new Grid { Width = W, Height = H, ClipToBounds = true };
            var pic = new Image
            {
                Source = bmp,
                Width = W,
                Height = H,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            layerGrid.Children.Add(pic);
            if (imageKey == "")
            {
                layerGrid.Children.Add(new Border
                {
                    Background = Brush.Parse(clothHex),
                    Opacity = 0.42
                });
            }
            if (blurRadius > 0) layerGrid.Effect = new BlurEffect { Radius = blurRadius };
            return layerGrid;
        }

        var sharpBg = BuildArtLayer(0);
        Canvas.SetLeft(sharpBg, 0);
        Canvas.SetTop(sharpBg, 0);
        coverCanvas.Children.Add(sharpBg);

        var coverMode = work.CoverStyle switch
        {
            "Centrada" or "Cartela central difuminada" => "Centrada",
            "Cabecera" or "Franja superior de autor" => "Cabecera",
            "Marco áureo" or "Marco áureo con velo" => "Marco áureo",
            "Banda completa" or "Banda diagonal moderna" or "Banda completa translúcida" => "Banda completa",
            "Tejuelo clásico" or "Tejuelo clásico translúcido" => "Tejuelo clásico",
            _ => "Al pie"
        };

        if (!backCover && coverMode == "Marco áureo")
        {
            var outerFrame = new Border
            {
                Width = W - 36,
                Height = H - 36,
                BorderBrush = Brush.Parse("#D0E8C878"),
                BorderThickness = new Thickness(1.25),
                CornerRadius = new CornerRadius(2)
            };
            Canvas.SetLeft(outerFrame, 18);
            Canvas.SetTop(outerFrame, 18);
            coverCanvas.Children.Add(outerFrame);
        }

        bool fullBand = !backCover && coverMode == "Banda completa";
        double plateW = fullBand ? W : 434;
        double plateH = backCover
            ? 224
            : (work.ShowCoverSubtitle && work.Subtitle != "" ? 220 : 184);
        double plateX = fullBand ? 0 : (W - plateW) / 2;
        double plateY = backCover
            ? H - plateH - 62
            : coverMode switch
            {
                "Centrada" => (H - plateH) / 2,
                "Cabecera" => 52,
                _ => H - plateH - 66
            };

        var plateRoot = new Grid { Width = plateW, Height = plateH, ClipToBounds = true };
        var blurSliceCanvas = new Canvas { Width = plateW, Height = plateH, ClipToBounds = true };
        var blurredArt = BuildArtLayer(18);
        Canvas.SetLeft(blurredArt, -plateX);
        Canvas.SetTop(blurredArt, -plateY);
        blurSliceCanvas.Children.Add(blurredArt);
        plateRoot.Children.Add(blurSliceCanvas);

        plateRoot.Children.Add(new Border
        {
            Background = Brush.Parse("#7C101820")
        });

        if (!fullBand)
        {
            plateRoot.Children.Add(new Border
            {
                Margin = new Thickness(3.5),
                BorderBrush = Brush.Parse("#80E8C878"),
                BorderThickness = new Thickness(0.75),
                CornerRadius = new CornerRadius(3)
            });
        }

        var textStack = Column(backCover ? 10 : 8);
        textStack.Margin = new Thickness(26, 18);
        textStack.HorizontalAlignment = HorizontalAlignment.Center;
        textStack.VerticalAlignment = VerticalAlignment.Center;

        if (backCover)
        {
            textStack.Children.Add(new TextBlock
            {
                Text = "SINOPSIS",
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 1.6,
                Foreground = Brush.Parse("#E8D28A"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            var synText = work.Description.Trim() != ""
                ? work.Description.Trim()
                : "Escribe la sinopsis de tu novela en Ficha → Sinopsis de contraportada para mostrarla aquí.";
            textStack.Children.Add(new TextBlock
            {
                Text = synText,
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = synText.Length > 340 ? 12.5 : 14,
                LineHeight = synText.Length > 340 ? 18 : 20,
                Foreground = Brush.Parse("#FAF6EE"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxHeight = 156
            });
        }
        else
        {
            textStack.Children.Add(new TextBlock
            {
                Text = "NOVELA",
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 1.8,
                Foreground = Brush.Parse("#E8D28A"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            textStack.Children.Add(new TextBlock
            {
                Text = work.Title,
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = work.Title.Length > 32 ? 26 : 30,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#FAF6EE"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxLines = 2
            });
            if (work.ShowCoverSubtitle && work.Subtitle != "")
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = work.Subtitle,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                    FontSize = 15,
                    FontStyle = FontStyle.Italic,
                    Foreground = Brush.Parse("#EAE2D2"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MaxLines = 2
                });
            }
            textStack.Children.Add(new Border
            {
                Width = 136,
                Height = 1.1,
                Background = Brush.Parse("#E2C675"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4)
            });
            if (work.Author != "")
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = work.Author,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                    FontSize = 15.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#FAF5EA"),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
        }

        plateRoot.Children.Add(textStack);
        var plateBorder = new Border
        {
            Width = plateW,
            Height = plateH,
            ClipToBounds = true,
            CornerRadius = new CornerRadius(fullBand ? 0 : 5),
            BorderBrush = Brush.Parse("#DFE8C878"),
            BorderThickness = new Thickness(fullBand ? 0 : 1.25),
            BoxShadow = BoxShadows.Parse("0 8 24 #55000000"),
            Child = plateRoot
        };
        Canvas.SetLeft(plateBorder, plateX);
        Canvas.SetTop(plateBorder, plateY);
        coverCanvas.Children.Add(plateBorder);

        return new Border
        {
            Width = W,
            Height = H,
            Child = coverCanvas,
            BorderBrush = Edge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            BoxShadow = BoxShadows.Parse("0 14 32 #38000000"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    internal Border BuildDocumentCoverCanvas(Work work, int decodeWidth = 720, Action<Chapter>? onSelectChapter = null)
    {
        if (work.IsNovel) return BuildNovelCoverCanvas(work, false, decodeWidth);
        var pages = BuildDocumentPages(work, decodeWidth, onSelectChapter);
        return pages.Count > 0 ? pages[0].BuildSheet() : new Border();
    }

    internal Border BuildDocumentSectionCanvas(Work work, Chapter section, int decodeWidth = 780)
    {
        var pages = BuildDocumentPages(work, decodeWidth);
        var match = pages.FirstOrDefault(p => p.Section == section) ?? pages.FirstOrDefault();
        return match != null ? match.BuildSheet() : new Border();
    }

    Border WrapPaperSheet(Control inner, Work work, bool isNovel, int? pageNumber = null, bool showFooterRule = false, VerticalAlignment bodyAlignment = VerticalAlignment.Top)
    {
        double sheetW = isNovel ? 520 : 680;
        double sheetH = isNovel ? 735 : 880;
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var paperBg = isNovel ? Brush.Parse("#FAF6EE") : Brushes.White;
        var pad = inner.Margin.Left > 0 || inner.Margin.Top > 0
            ? inner.Margin
            : new Thickness(isNovel ? 44 : 40, 32);
        inner.Margin = new Thickness(0);

        bool hasFrame = work.FrameStyle is "Filete" or "Doble filete" or "Esquinas";
        double frameInset = hasFrame ? 24 : 0;
        double innerW = Math.Max(200, sheetW - pad.Left - pad.Right - frameInset);

        var pageDock = new DockPanel
        {
            Margin = pad,
            LastChildFill = true
        };

        if (pageNumber is int pg && work.Publication.PageNumbers)
        {
            var footer = Column(4);
            footer.Margin = new Thickness(0, 8, 0, 0);
            if (showFooterRule)
                footer.Children.Add(new Border { Height = 1, Background = Brush.Parse("#E2E6EA"), Margin = new Thickness(0, 0, 0, 2) });
            footer.Children.Add(new TextBlock
            {
                Text = pg.ToString(),
                FontFamily = isNovel ? new FontFamily("Georgia, Liberation Serif, serif") : FontFamily.Default,
                FontSize = isNovel ? 13 : 12.5,
                FontWeight = work.Type == "Apuntes" ? FontWeight.Bold : FontWeight.Normal,
                Foreground = work.Type == "Apuntes" ? Brush.Parse("#004785") : Brush.Parse("#545E68"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            DockPanel.SetDock(footer, Dock.Bottom);
            pageDock.Children.Add(footer);
        }

        var fittedBody = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = bodyAlignment,
            Child = new Border
            {
                Width = innerW,
                Child = inner
            }
        };
        pageDock.Children.Add(fittedBody);

        Control content = pageDock;
        if (hasFrame)
        {
            content = new Border
            {
                Margin = new Thickness(10),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(work.FrameStyle == "Doble filete" ? 2 : 1),
                Child = pageDock
            };
        }
        return new Border
        {
            Width = sheetW,
            Height = sheetH,
            Background = paperBg,
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            BoxShadow = BoxShadows.Parse("0 12 28 #24000000"),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    internal List<DocumentPageItem> BuildDocumentPages(Work work, int decodeWidth = 760, Action<Chapter>? onSelectChapter = null)
    {
        var pages = new List<DocumentPageItem>();
        var sections = WorldModel.Ordered(work).ToList();
        var style = ResolveTemplateStyle(work);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var ink = Brush.Parse("#1D2227");
        var muted = Brush.Parse("#545E68");
        bool isNovel = work.IsNovel;
        bool isLibreta = work.Type == "Apuntes" && style == "Libreta";
        bool isReporte = work.Type == "Informe" && style == "Reporte";

        string Fact(string key, string fallback = "") =>
            work.DocumentFacts.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

        if (work.Type == "CV")
        {
            return BuildPaginatedResumePages(work, decodeWidth);
        }

        var chapterStartPages = new Dictionary<Chapter, int>();

        if (isNovel)
        {
            pages.Add(new DocumentPageItem(1, "Portada", null, () => BuildNovelCoverCanvas(work, false, decodeWidth)));
            if (work.Publication.Contents && sections.Count > 0)
            {
                int tocPageNum = pages.Count + 1;
                pages.Add(new DocumentPageItem(tocPageNum, "Contenido", null, () =>
                {
                    var tocCol = Column(12);
                    tocCol.Margin = new Thickness(48, 52);
                    tocCol.Children.Add(new TextBlock
                    {
                        Text = "Contenido",
                        FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                        FontSize = 29,
                        FontWeight = FontWeight.Bold,
                        Foreground = ink,
                        Margin = new Thickness(0, 14, 0, 16)
                    });
                    int arcCount = 0, chapCount = 0;
                    foreach (var ch in sections)
                    {
                        string prefix = ch.Level == "Arc"
                            ? ToRoman(++arcCount) + "   "
                            : ch.Level == "Chapter"
                                ? (++chapCount) + "   "
                                : "      ";
                        int targetPg = chapterStartPages.GetValueOrDefault(ch, 3);
                        var row = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                            Margin = new Thickness(ch.Level == "Subchapter" ? 18 : 0, 4, 0, 4)
                        };
                        row.Children.Add(new TextBlock
                        {
                            Text = prefix,
                            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                            FontSize = 14.5,
                            FontWeight = FontWeight.Bold,
                            Foreground = ink
                        });
                        var titleTb = new TextBlock
                        {
                            Text = ch.Title,
                            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                            FontSize = ch.Level == "Arc" ? 15.5 : 14.5,
                            FontWeight = ch.Level == "Arc" ? FontWeight.Bold : FontWeight.SemiBold,
                            Foreground = ink,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        Grid.SetColumn(titleTb, 1);
                        row.Children.Add(titleTb);
                        var pTb = new TextBlock
                        {
                            Text = targetPg.ToString(),
                            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                            FontSize = 14.5,
                            FontWeight = FontWeight.Bold,
                            Foreground = ink
                        };
                        Grid.SetColumn(pTb, 2);
                        row.Children.Add(pTb);
                        var btn = new Button
                        {
                            Content = row,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(4, 2),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };
                        var targetSection = ch;
                        btn.Click += (_, _) =>
                        {
                            if (onSelectChapter != null) onSelectChapter(targetSection);
                            else ReadSection(targetSection);
                        };
                        tocCol.Children.Add(btn);
                    }
                    return WrapPaperSheet(tocCol, work, true, tocPageNum, false);
                }));
            }
        }
        else if (isLibreta)
        {
            pages.Add(new DocumentPageItem(1, "Portada", null, () =>
            {
                var uaslpBlue = "#004785";
                var blueBrush = Brush.Parse(uaslpBlue);
                var univ = Fact("Universidad", "Universidad");
                var fac = Fact("Facultad", "");
                var asig = Fact("Asignatura", work.Subtitle != "" ? work.Subtitle : work.Title);
                var clave = Fact("Clave de clase", "");
                var prof = Fact("Docente", "");
                var carrera = Fact("Carrera", "");
                var claveU = Fact("Clave U", "");
                var alumno = Fact("Alumno", work.Author);
                var lugarFecha = Fact("Lugar y fecha", Fact("Periodo", ""));

                var coverCol = Column(12);
                coverCol.Margin = new Thickness(44, 42);
                coverCol.Children.Add(new TextBlock { Text = univ, FontSize = 21, FontWeight = FontWeight.Bold, Foreground = blueBrush, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
                if (fac != "")
                    coverCol.Children.Add(new TextBlock { Text = fac, FontSize = 15.5, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                coverCol.Children.Add(new TextBlock { Text = asig, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) });

                var bannerStack = Column(6);
                bannerStack.HorizontalAlignment = HorizontalAlignment.Center;
                bannerStack.Children.Add(new TextBlock { Text = "Apuntes:", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = blueBrush, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                bannerStack.Children.Add(new TextBlock { Text = work.Title, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
                if (work.Subtitle != "" && work.Subtitle != asig)
                    bannerStack.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 13, Foreground = muted, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });
                coverCol.Children.Add(new Border
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
                    if (Thumbnail(path, 460, 150) is { } pic)
                    {
                        pic.MaxHeight = 150;
                        pic.Stretch = Stretch.Uniform;
                        pic.HorizontalAlignment = HorizontalAlignment.Center;
                        coverCol.Children.Add(new Border { Child = pic, Margin = new Thickness(0, 6) });
                    }
                }

                var rowsList = new List<(string Label, string Val)>
                {
                    ("Clase:", asig + (clave != "" ? $" ({clave})" : ""))
                };
                if (prof != "") rowsList.Add(("Profesor:", prof));
                if (carrera != "") rowsList.Add(("Carrera:", carrera));
                if (claveU != "") rowsList.Add(("Clave U:", claveU));

                var tableGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*"), RowSpacing = 6, Margin = new Thickness(28, 10, 28, 6) };
                for (int r = 0; r < rowsList.Count; r++)
                {
                    tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    var lTb = new TextBlock { Text = rowsList[r].Label, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = blueBrush };
                    var vTb = new TextBlock { Text = rowsList[r].Val, FontSize = 13.5, Foreground = ink, TextWrapping = TextWrapping.Wrap };
                    Grid.SetRow(lTb, r); Grid.SetColumn(lTb, 0);
                    Grid.SetRow(vTb, r); Grid.SetColumn(vTb, 1);
                    tableGrid.Children.Add(lTb);
                    tableGrid.Children.Add(vTb);
                }
                coverCol.Children.Add(tableGrid);

                if (alumno != "")
                {
                    var studentBox = Column(4);
                    studentBox.Margin = new Thickness(28, 6, 28, 10);
                    studentBox.Children.Add(new TextBlock { Text = "Alumno:", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = blueBrush });
                    studentBox.Children.Add(new TextBlock { Text = "•  " + alumno, FontSize = 13.5, Foreground = ink, Margin = new Thickness(12, 0, 0, 0) });
                    coverCol.Children.Add(studentBox);
                }
                if (lugarFecha != "")
                    coverCol.Children.Add(new TextBlock { Text = lugarFecha, FontSize = 12, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) });
                return WrapPaperSheet(coverCol, work, false);
            }));

            if (work.Publication.Contents || sections.Count > 0)
            {
                pages.Add(new DocumentPageItem(2, "Índice", null, () =>
                {
                    var blueBrush = Brush.Parse("#004785");
                    var idxCol = Column(10);
                    idxCol.Margin = new Thickness(44, 42);
                    idxCol.Children.Add(new TextBlock { Text = "Índice", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = blueBrush });
                    idxCol.Children.Add(new Border { Height = 2.2, Background = Brush.Parse("#22714B"), Margin = new Thickness(0, 2, 0, 10) });
                    for (int i = 0; i < sections.Count; i++)
                    {
                        var ch = sections[i];
                        int targetPg = chapterStartPages.GetValueOrDefault(ch, i + 3);
                        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 3) };
                        row.Children.Add(new TextBlock { Text = $"{i + 1}   ", FontWeight = FontWeight.Bold, Foreground = blueBrush, FontSize = 14 });
                        var tTb = new TextBlock { Text = ch.Title, Foreground = blueBrush, FontWeight = FontWeight.SemiBold, FontSize = 14 };
                        Grid.SetColumn(tTb, 1);
                        row.Children.Add(tTb);
                        var pTb = new TextBlock { Text = $"{targetPg}", Foreground = blueBrush, FontWeight = FontWeight.Bold, FontSize = 14 };
                        Grid.SetColumn(pTb, 2);
                        row.Children.Add(pTb);
                        var btn = new Button
                        {
                            Content = row,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(4, 2),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };
                        var targetSection = ch;
                        btn.Click += (_, _) =>
                        {
                            if (onSelectChapter != null) onSelectChapter(targetSection);
                            else ReadSection(targetSection);
                        };
                        idxCol.Children.Add(btn);
                    }
                    return WrapPaperSheet(idxCol, work, false, 2, true);
                }));
            }
        }
        else if (isReporte)
        {
            pages.Add(new DocumentPageItem(1, "Portada", null, () =>
            {
                var coverCol = Column(10);
                coverCol.Margin = new Thickness(44, 46);
                var materia = Fact("Materia o área", Fact("Departamento", Fact("Institución", "")));
                var realizada = Fact("Realizada por", Fact("Alumno", work.Author));
                if (materia != "")
                    coverCol.Children.Add(new TextBlock { Text = materia, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                if (realizada != "")
                {
                    coverCol.Children.Add(new TextBlock { Text = "Realizada por:", FontSize = 13, Foreground = muted, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
                    coverCol.Children.Add(new TextBlock { Text = realizada, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                }
                coverCol.Children.Add(new TextBlock { Text = work.Title, FontSize = 25, FontWeight = FontWeight.Bold, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 6) });
                if (work.Subtitle != "")
                    coverCol.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 14.5, FontStyle = FontStyle.Italic, Foreground = muted, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center });

                var metaStack = Column(4);
                metaStack.HorizontalAlignment = HorizontalAlignment.Center;
                metaStack.Margin = new Thickness(0, 10, 0, 8);
                foreach (var k in EditorialTemplate.FrontMatter(work.Type))
                {
                    if (k is "Materia o área" or "Realizada por") continue;
                    var fv = Fact(k);
                    if (fv != "")
                        metaStack.Children.Add(new TextBlock { Text = $"{k}: {fv}", FontSize = 13.5, Foreground = ink, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                }
                coverCol.Children.Add(metaStack);

                if (work.CoverImage != "")
                {
                    var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(work.CoverImage));
                    if (Thumbnail(path, 480, 170) is { } pic)
                    {
                        pic.MaxHeight = 170;
                        pic.Stretch = Stretch.Uniform;
                        pic.HorizontalAlignment = HorizontalAlignment.Center;
                        coverCol.Children.Add(new Border { Child = pic, Margin = new Thickness(0, 10) });
                    }
                }
                return WrapPaperSheet(coverCol, work, false);
            }));
        }

        List<(double Weight, Func<Control> Build)> BuildSectionBlockFactories(Chapter section)
        {
            var items = new List<(double Weight, Func<Control> Build)>();

            if (section.Detail != "")
            {
                var detailTitle = work.Type == "Apuntes" ? "Conceptos y preguntas" : EditorialTemplate.DetailLabel(work.Type);
                var detailText = section.Detail;
                items.Add((3.5, () =>
                {
                    var detailBox = Column(3);
                    detailBox.Children.Add(new TextBlock { Text = detailTitle, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = accentBrush });
                    detailBox.Children.Add(new TextBlock { Text = detailText, FontSize = 13, FontStyle = FontStyle.Italic, Foreground = ink, TextWrapping = TextWrapping.Wrap });
                    return new Border
                    {
                        Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.10),
                        BorderBrush = EditionPalette.Mix(accentHex, "#FFFFFF", 0.35),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(12, 8),
                        Child = detailBox
                    };
                }));
            }

            Func<Control> IllustrationFactory(int index, bool small = false)
            {
                return () =>
                {
                    var holder = Column(4);
                    var path = System.IO.Path.Combine(shelf.Media, System.IO.Path.GetFileName(section.Images[index]));
                    var bitmap = GetCachedBitmap(path, decodeWidth);
                    if (bitmap != null)
                    {
                        var layout = Illustrations.Layout(section, index);
                        var img = new Image
                        {
                            Source = bitmap,
                            Stretch = Stretch.Uniform,
                            MaxHeight = small ? 150 : layout == "Página completa" ? 420 : layout.StartsWith("Ajustar") ? 200 : 300,
                            HorizontalAlignment = layout.EndsWith("izquierda") ? HorizontalAlignment.Left : layout.EndsWith("derecha") ? HorizontalAlignment.Right : HorizontalAlignment.Center
                        };
                        holder.Children.Add(img);
                    }
                    else
                    {
                        holder.Children.Add(new TextBlock { Text = "Ilustración no disponible", Foreground = muted });
                    }
                    if (section.ImageCaptions.TryGetValue(index, out var caption) && caption != "")
                    {
                        holder.Children.Add(new TextBlock
                        {
                            Text = caption,
                            FontSize = 12,
                            FontStyle = FontStyle.Italic,
                            Foreground = muted,
                            TextAlignment = TextAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                    return holder;
                };
            }

            void CollectTextSegmentFactories(string segment)
            {
                var lines = segment.ReplaceLineEndings("\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line == "[[Página]]")
                    {
                        items.Add((999, () => new Border { Height = 1 }));
                        continue;
                    }
                    if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
                    {
                        int startLine = i;
                        int rowCount = 0;
                        while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'))
                        {
                            rowCount++;
                            i++;
                        }
                        i--;
                        var tableSlice = string.Join("\n", lines[startLine..(i + 1)]);
                        items.Add((Math.Max(4.0, rowCount * 1.8), () =>
                        {
                            var p = Column(0);
                            AddStyledBlocks(p, tableSlice, accentHex, ink, isNovel ? 15.5 : 13.8, isNovel);
                            return p;
                        }));
                        continue;
                    }
                    if (line.StartsWith("> "))
                    {
                        int startLine = i;
                        int qCount = 0;
                        while (i < lines.Length && lines[i].StartsWith("> "))
                        {
                            qCount += Math.Max(1, (int)Math.Ceiling(lines[i].Length / 70.0));
                            i++;
                        }
                        i--;
                        var quoteSlice = string.Join("\n", lines[startLine..(i + 1)]);
                        items.Add((Math.Max(2.5, qCount * 1.5), () =>
                        {
                            var p = Column(4);
                            AddStyledBlocks(p, quoteSlice, accentHex, ink, isNovel ? 15.5 : 13.8, isNovel);
                            return p;
                        }));
                        continue;
                    }
                    var singleLine = line;
                    double w = Math.Max(1.6, Math.Ceiling(singleLine.Length / 76.0) * 1.45);
                    items.Add((w, () =>
                    {
                        var p = Column(0);
                        AddStyledBlocks(p, singleLine, accentHex, ink, isNovel ? 15.5 : 13.8, isNovel);
                        return p;
                    }));
                }
            }

            var rows = Illustrations.Rows(section).ToList();
            var used = new HashSet<int>();
            int start = 0;
            if (rows.Count == 0 && section.Images.Count > 0 && Illustrations.Layout(section, 0).StartsWith("Ajustar"))
            {
                items.Add((6.5, IllustrationFactory(0)));
                used.Add(0);
            }
            for (int rIdx = 0; rIdx < rows.Count; rIdx++)
            {
                var row = rows[rIdx];
                CollectTextSegmentFactories(section.PlainText[start..row[0].Start]);
                if (row.Count == 1)
                {
                    int index = row[0].Image;
                    used.Add(index);
                    var layout = Illustrations.Layout(section, index);
                    if (layout.StartsWith("Ajustar") && rIdx + 1 <= rows.Count)
                    {
                        int nextEnd = rIdx + 1 < rows.Count ? rows[rIdx + 1][0].Start : section.PlainText.Length;
                        var nextText = section.PlainText[row[0].End..nextEnd];
                        var firstParaEnd = nextText.IndexOf("\n\n", StringComparison.Ordinal);
                        var wrappedSlice = (firstParaEnd > 0 ? nextText[..firstParaEnd] : nextText).Trim();
                        if (wrappedSlice != "" && !wrappedSlice.StartsWith("[["))
                        {
                            bool leftSide = layout.EndsWith("izquierda");
                            var imgFn = IllustrationFactory(index);
                            items.Add((7.5, () =>
                            {
                                var wrapGrid = new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions(leftSide ? "185,*" : "*,185"),
                                    ColumnSpacing = 14
                                };
                                var textCol = Column(6);
                                AddStyledBlocks(textCol, wrappedSlice, accentHex, ink, isNovel ? 15.5 : 13.8, isNovel);
                                var imgBox = imgFn();
                                Grid.SetColumn(imgBox, leftSide ? 0 : 1);
                                Grid.SetColumn(textCol, leftSide ? 1 : 0);
                                wrapGrid.Children.Add(textCol);
                                wrapGrid.Children.Add(imgBox);
                                return wrapGrid;
                            }));
                            start = firstParaEnd > 0 ? row[0].End + firstParaEnd : nextEnd;
                            continue;
                        }
                    }
                    items.Add((layout == "Lámina ilustrada" || layout == "Página completa" ? 10.0 : 7.0, IllustrationFactory(index)));
                }
                else
                {
                    var rowIndices = row.Select(s => s.Image).ToArray();
                    foreach (var ri in rowIndices) used.Add(ri);
                    items.Add((7.0, () =>
                    {
                        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", rowIndices.Select(_ => "*"))), ColumnSpacing = 12 };
                        for (int col = 0; col < rowIndices.Length; col++)
                        {
                            var cell = IllustrationFactory(rowIndices[col], true)();
                            Grid.SetColumn(cell, col);
                            strip.Children.Add(cell);
                        }
                        return strip;
                    }));
                }
                start = row[^1].End;
            }
            CollectTextSegmentFactories(section.PlainText[start..]);
            for (int i = 0; i < section.Images.Count; i++)
                if (!used.Contains(i)) items.Add((7.0, IllustrationFactory(i)));

            if (section.Summary != "")
            {
                var summaryText = section.Summary;
                items.Add((4.0, () =>
                {
                    var sumCol = Column(4);
                    sumCol.Children.Add(new TextBlock
                    {
                        Text = work.Type == "Apuntes" ? "Síntesis" : "Síntesis / Conclusión",
                        FontSize = 12,
                        FontWeight = FontWeight.Bold,
                        Foreground = accentBrush
                    });
                    AddStyledBlocks(sumCol, summaryText, accentHex, ink, 13);
                    return new Border
                    {
                        Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.08),
                        BorderBrush = accentBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(12, 8),
                        Child = sumCol
                    };
                }));
            }

            if (isReporte && work.Publication.Contents && section.Title.Equals("Resumen", StringComparison.OrdinalIgnoreCase))
            {
                items.Add((6.0, () =>
                {
                    var tocBox = Column(6);
                    tocBox.Margin = new Thickness(0, 10, 0, 4);
                    tocBox.Children.Add(new TextBlock { Text = "Contenido", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = accentBrush });
                    tocBox.Children.Add(new Border { Height = 1.2, Background = accentBrush });
                    int numSec = 0;
                    foreach (var ch in sections)
                    {
                        if (ch.Title.Equals("Resumen", StringComparison.OrdinalIgnoreCase)) continue;
                        bool unnum = ch.Title.StartsWith("Conclusi", StringComparison.OrdinalIgnoreCase) || ch.Title.StartsWith("Bibliograf", StringComparison.OrdinalIgnoreCase) || ch.Title.StartsWith("Referencia", StringComparison.OrdinalIgnoreCase);
                        string prefix = unnum ? "" : $"{++numSec}.  ";
                        int targetPg = chapterStartPages.GetValueOrDefault(ch, 2);
                        var rGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                        if (prefix != "")
                            rGrid.Children.Add(new TextBlock { Text = prefix, FontWeight = FontWeight.Bold, Foreground = accentBrush, FontSize = 13.5 });
                        var tTb = new TextBlock { Text = ch.Title, Foreground = ink, FontSize = 13.5, FontWeight = unnum ? FontWeight.SemiBold : FontWeight.Normal };
                        Grid.SetColumn(tTb, 1);
                        rGrid.Children.Add(tTb);
                        var pTb = new TextBlock { Text = targetPg.ToString(), Foreground = muted, FontSize = 13 };
                        Grid.SetColumn(pTb, 2);
                        rGrid.Children.Add(pTb);
                        var btn = new Button
                        {
                            Content = rGrid,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(4, 2),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };
                        var targetSection = ch;
                        btn.Click += (_, _) =>
                        {
                            if (onSelectChapter != null) onSelectChapter(targetSection);
                            else ReadSection(targetSection);
                        };
                        tocBox.Children.Add(btn);
                    }
                    return tocBox;
                }));
            }

            return items;
        }

        void AddInlineFirstPageMasthead(StackPanel paper)
        {
            if (work.Type == "Carta")
            {
                var placeDate = Fact("Lugar y fecha", "Ciudad, a fecha actual");
                var subject = Fact("Asunto", work.Title);
                var recipient = Fact("Destinatario", "");
                var address = Fact("Dirección", "");
                paper.Children.Add(new TextBlock { Text = placeDate, FontSize = 13, Foreground = muted, TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Right });
                paper.Children.Add(new TextBlock { Text = "ASUNTO: " + subject, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = accentBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) });
                if (recipient != "" || address != "")
                {
                    var recBlock = Column(2);
                    recBlock.Children.Add(new TextBlock { Text = "Dirigido a:", FontSize = 13, Foreground = muted });
                    if (recipient != "") recBlock.Children.Add(new TextBlock { Text = recipient, FontSize = 14.5, FontWeight = FontWeight.Bold, Foreground = ink });
                    if (address != "") recBlock.Children.Add(new TextBlock { Text = address, FontSize = 13, Foreground = ink });
                    recBlock.Children.Add(new TextBlock { Text = "PRESENTE.", FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = ink });
                    paper.Children.Add(recBlock);
                }
                paper.Children.Add(new Border { Height = 1.2, Background = accentBrush, Margin = new Thickness(0, 4, 0, 8) });
            }
            else
            {
                var topHead = Column(4);
                topHead.Children.Add(new TextBlock { Text = work.Title, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = accentBrush, TextWrapping = TextWrapping.Wrap });
                if (work.Subtitle != "")
                    topHead.Children.Add(new TextBlock { Text = work.Subtitle, FontSize = 14, Foreground = muted, TextWrapping = TextWrapping.Wrap });
                if (work.Author != "")
                    topHead.Children.Add(new TextBlock { Text = work.Author, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = ink });
                paper.Children.Add(topHead);
                paper.Children.Add(new Border { Height = 1.4, Background = accentBrush, Margin = new Thickness(0, 2, 0, 4) });
                AddFrontMatterTable(paper, work, accentHex, ink);
            }
        }

        void AddRunningHeader(StackPanel paper)
        {
            var runningHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            if (work.Type == "Apuntes")
            {
                var asig = Fact("Asignatura", work.Title);
                var clave = Fact("Clave de clase", work.Template);
                runningHeader.Children.Add(new TextBlock { Text = asig, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Foreground = muted });
                var rightTb = new TextBlock { Text = clave != "" ? $"{clave}  ·  {work.Title}" : work.Title, FontSize = 11.5, Foreground = muted };
                Grid.SetColumn(rightTb, 1);
                runningHeader.Children.Add(rightTb);
                paper.Children.Add(runningHeader);
                paper.Children.Add(new Border { Height = 1.1, Background = Brush.Parse("#004785") });
            }
            else
            {
                runningHeader.Children.Add(new TextBlock
                {
                    Text = work.Title,
                    FontSize = 12,
                    FontStyle = isNovel ? FontStyle.Italic : FontStyle.Normal,
                    FontWeight = isNovel ? FontWeight.Normal : FontWeight.SemiBold,
                    Foreground = muted
                });
                var rightMeta = new TextBlock
                {
                    Text = isNovel ? (work.Author != "" ? work.Author : "Novela") : work.Type,
                    FontSize = 11.5,
                    Foreground = muted
                };
                Grid.SetColumn(rightMeta, 1);
                runningHeader.Children.Add(rightMeta);
                paper.Children.Add(runningHeader);
                paper.Children.Add(new Border { Height = 1, Background = Brush.Parse("#D8D2C5") });
            }
        }

        bool needsInlineMastheadOnFirstSection = !isNovel && !isLibreta && !isReporte;
        int arcOrdinal = 0, novelChapOrdinal = 0, reporteNumOrdinal = 0;

        for (int sIdx = 0; sIdx < sections.Count; sIdx++)
        {
            var section = sections[sIdx];
            chapterStartPages[section] = pages.Count + 1;

            if (isNovel && section.Level == "Arc" && string.IsNullOrWhiteSpace(section.PlainText) && section.Images.Count == 0)
            {
                int partNum = ++arcOrdinal;
                int partPageNum = pages.Count + 1;
                pages.Add(new DocumentPageItem(partPageNum, section.Title, section, () =>
                {
                    var partCol = Column(12);
                    partCol.Margin = new Thickness(48, 36);
                    partCol.HorizontalAlignment = HorizontalAlignment.Center;
                    partCol.Children.Add(new TextBlock
                    {
                        Text = $"Parte {ToRoman(partNum)}",
                        FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                        FontSize = 22,
                        FontWeight = FontWeight.Bold,
                        Foreground = ink,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    partCol.Children.Add(new TextBlock
                    {
                        Text = section.Title,
                        FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                        FontSize = 28,
                        FontWeight = FontWeight.Bold,
                        Foreground = ink,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    return WrapPaperSheet(partCol, work, true, partPageNum, false, VerticalAlignment.Center);
                }));
                continue;
            }

            int thisNovelChapNum = isNovel && section.Level != "Arc" ? ++novelChapOrdinal : 0;
            bool isUnnumberedReporteSec = isReporte && (section.Title.Equals("Resumen", StringComparison.OrdinalIgnoreCase) || section.Title.StartsWith("Conclusi", StringComparison.OrdinalIgnoreCase) || section.Title.StartsWith("Bibliograf", StringComparison.OrdinalIgnoreCase) || section.Title.StartsWith("Referencia", StringComparison.OrdinalIgnoreCase));
            int thisReporteSecNum = isReporte && !isUnnumberedReporteSec ? ++reporteNumOrdinal : 0;

            var allItems = BuildSectionBlockFactories(section);
            var chunks = new List<List<Func<Control>>>();
            var currentChunk = new List<Func<Control>>();
            double currentWeight = 0;
            double maxWeight = (sIdx == 0 && needsInlineMastheadOnFirstSection) ? 13.5 : (isNovel ? 15.0 : 17.5);

            foreach (var (w, factory) in allItems)
            {
                if (w >= 900)
                {
                    if (currentChunk.Count > 0) chunks.Add(currentChunk);
                    currentChunk = [];
                    currentWeight = 0;
                    maxWeight = isNovel ? 17.0 : 19.0;
                    continue;
                }
                if (currentChunk.Count > 0 && currentWeight + w > maxWeight)
                {
                    chunks.Add(currentChunk);
                    currentChunk = [];
                    currentWeight = 0;
                    maxWeight = isNovel ? 17.0 : 19.0;
                }
                currentChunk.Add(factory);
                currentWeight += w;
            }
            if (currentChunk.Count > 0 || chunks.Count == 0)
                chunks.Add(currentChunk);

            for (int cIdx = 0; cIdx < chunks.Count; cIdx++)
            {
                int pageNum = pages.Count + 1;
                int capturedSectionIdx = sIdx;
                int capturedChunkIdx = cIdx;
                var chunkFactories = chunks[cIdx];
                var pageLabel = chunks.Count > 1
                    ? $"{section.Title} ({cIdx + 1}/{chunks.Count})"
                    : section.Title;

                pages.Add(new DocumentPageItem(pageNum, pageLabel, section, () =>
                {
                    var paper = Column(12);
                    paper.Margin = new Thickness(isNovel ? 44 : 40, 32);

                    if (capturedSectionIdx == 0 && capturedChunkIdx == 0 && needsInlineMastheadOnFirstSection)
                        AddInlineFirstPageMasthead(paper);
                    else
                        AddRunningHeader(paper);

                    if (capturedChunkIdx == 0)
                    {
                        if (isNovel)
                        {
                            var chapHeader = Column(4);
                            chapHeader.Margin = new Thickness(0, 12, 0, 8);
                            chapHeader.HorizontalAlignment = HorizontalAlignment.Left;
                            bool hasExplicitPrefix = section.Title.StartsWith("Capítulo", StringComparison.OrdinalIgnoreCase) || section.Title.StartsWith("Parte", StringComparison.OrdinalIgnoreCase) || section.Title.StartsWith("Arco", StringComparison.OrdinalIgnoreCase);
                            if (!hasExplicitPrefix)
                            {
                                chapHeader.Children.Add(new TextBlock
                                {
                                    Text = section.Level == "Arc" ? "Parte" : $"Capítulo {thisNovelChapNum}",
                                    FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                                    FontSize = 20,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = ink
                                });
                            }
                            chapHeader.Children.Add(new TextBlock
                            {
                                Text = section.Title,
                                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                                FontSize = 26,
                                FontWeight = FontWeight.Bold,
                                Foreground = ink,
                                TextWrapping = TextWrapping.Wrap
                            });
                            paper.Children.Add(chapHeader);
                        }
                        else if (work.Type == "Apuntes")
                        {
                            var secHead = Column(3);
                            secHead.Children.Add(new TextBlock
                            {
                                Text = $"{capturedSectionIdx + 1}   {section.Title}",
                                FontSize = 20,
                                FontWeight = FontWeight.Bold,
                                Foreground = Brush.Parse("#004785"),
                                TextWrapping = TextWrapping.Wrap
                            });
                            secHead.Children.Add(new Border { Height = 2.2, Background = Brush.Parse("#22714B") });
                            paper.Children.Add(secHead);
                        }
                        else if (work.Type != "Carta")
                        {
                            var titlePrefix = isReporte && thisReporteSecNum > 0 ? $"{thisReporteSecNum}.  " : "";
                            var secHead = Column(3);
                            secHead.Children.Add(new TextBlock
                            {
                                Text = titlePrefix + section.Title,
                                FontSize = 20,
                                FontWeight = FontWeight.Bold,
                                Foreground = accentBrush,
                                TextWrapping = TextWrapping.Wrap
                            });
                            secHead.Children.Add(new Border { Height = 1.2, Background = accentBrush });
                            paper.Children.Add(secHead);
                        }
                    }
                    else
                    {
                        paper.Children.Add(new TextBlock
                        {
                            Text = $"{section.Title} (continuación)",
                            FontSize = 12.5,
                            FontStyle = FontStyle.Italic,
                            Foreground = muted
                        });
                    }

                    foreach (var buildCtrl in chunkFactories)
                        paper.Children.Add(buildCtrl());

                    return WrapPaperSheet(paper, work, isNovel, pageNum, true);
                }));
            }
        }

        if (isNovel && work.Publication.BackCover)
        {
            pages.Add(new DocumentPageItem(pages.Count + 1, "Contraportada", null, () => BuildNovelCoverCanvas(work, true, decodeWidth)));
        }

        return pages;
    }

    List<DocumentPageItem> BuildPaginatedResumePages(Work work, int decodeWidth)
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
        static bool HasCvContent(Chapter c) => c.PlainText != "" || c.Detail != "" || c.Summary != "" || c.Entries.Count > 0 || c.Images.Count > 0;
        var allOrdered = WorldModel.Ordered(work).ToList();
        var populated = allOrdered.Any(HasCvContent) ? allOrdered.Where(HasCvContent).ToList() : allOrdered;

        Control RenderCvSection(Chapter sec)
        {
            var secStack = Column(5);
            secStack.Children.Add(new TextBlock
            {
                Text = sec.Title.ToUpperInvariant(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.8,
                Foreground = style == "Minimalista" ? ink : accentBrush
            });
            secStack.Children.Add(new Border
            {
                Height = style == "Ejecutiva" ? 1.8 : 1,
                Background = style == "Minimalista" ? Brush.Parse("#222222") : accentBrush
            });
            if (sec.Detail != "")
                secStack.Children.Add(new TextBlock { Text = sec.Detail, FontSize = 12, FontStyle = FontStyle.Italic, Foreground = muted });
            if (!string.IsNullOrWhiteSpace(sec.PlainText))
                AddStyledBlocks(secStack, sec.PlainText, accentHex, ink, 13);
            foreach (var entry in sec.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Heading) && string.IsNullOrWhiteSpace(entry.Description)) continue;
                var entryCol = Column(2);
                entryCol.Margin = new Thickness(0, 3, 0, 2);
                var topLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                topLine.Children.Add(new TextBlock { Text = entry.Heading, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = ink, TextWrapping = TextWrapping.Wrap });
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
                    AddStyledBlocks(entryCol, entry.Description, accentHex, ink, 12.5);
                secStack.Children.Add(entryCol);
            }
            return secStack;
        }

        Control BuildCvHeader()
        {
            if (style is "Moderna" or "Ejecutiva" or "BandaLateral")
            {
                var bannerBg = style == "Ejecutiva" ? Brush.Parse("#1F2A36") : style == "BandaLateral" ? Brush.Parse("#223448") : accentBrush;
                var headStack = Column(3);
                headStack.Children.Add(new TextBlock { Text = name, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
                if (profession != "")
                    headStack.Children.Add(new TextBlock { Text = profession, FontSize = 14, Foreground = Brush.Parse("#E6EEF6"), TextWrapping = TextWrapping.Wrap });
                if (work.Title != name)
                    headStack.Children.Add(new TextBlock { Text = work.Title, FontSize = 11.5, Foreground = Brush.Parse("#C8D6E4") });
                if (contacts.Length > 0)
                    headStack.Children.Add(new TextBlock { Text = string.Join("   ·   ", contacts), FontSize = 11.5, Foreground = Brush.Parse("#F5E3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                return new Border { Background = bannerBg, CornerRadius = new CornerRadius(4), Padding = new Thickness(20, 14), Child = headStack };
            }
            bool centered = style == "Académica";
            var classicHead = Column(3);
            classicHead.HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            classicHead.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 25,
                FontWeight = FontWeight.Bold,
                Foreground = style == "Minimalista" ? ink : accentBrush,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
            });
            if (profession != "")
                classicHead.Children.Add(new TextBlock { Text = profession, FontSize = 14.5, Foreground = muted, TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left });
            if (work.Title != name)
                classicHead.Children.Add(new TextBlock { Text = work.Title, FontSize = 11.5, Foreground = muted, TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left });
            if (contacts.Length > 0)
                classicHead.Children.Add(new TextBlock { Text = string.Join("  ·  ", contacts), FontSize = 12, Foreground = muted, TextWrapping = TextWrapping.Wrap, TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left });
            classicHead.Children.Add(new Border { Height = 1.2, Background = style == "Minimalista" ? ink : accentBrush, Margin = new Thickness(0, 4, 0, 0) });
            return classicHead;
        }

        var cvPages = new List<DocumentPageItem>();
        var pageGroups = new List<List<Chapter>>();
        var currentGroup = new List<Chapter>();
        double weight = 0;
        foreach (var sec in populated)
        {
            double secW = 3.0 + Math.Max(1, sec.Entries.Count * 3.5) + Math.Ceiling(sec.PlainText.Length / 90.0);
            if (currentGroup.Count > 0 && weight + secW > (pageGroups.Count == 0 ? 18.0 : 22.0))
            {
                pageGroups.Add(currentGroup);
                currentGroup = [];
                weight = 0;
            }
            currentGroup.Add(sec);
            weight += secW;
        }
        if (currentGroup.Count > 0 || pageGroups.Count == 0)
            pageGroups.Add(currentGroup);

        for (int gIdx = 0; gIdx < pageGroups.Count; gIdx++)
        {
            int capturedG = gIdx;
            var groupSections = pageGroups[gIdx];
            var firstSec = groupSections.FirstOrDefault();
            var label = firstSec != null ? firstSec.Title : work.Title;
            cvPages.Add(new DocumentPageItem(gIdx + 1, label, firstSec, () =>
            {
                var pCol = Column(12);
                pCol.Margin = new Thickness(38, 32);
                if (capturedG == 0) pCol.Children.Add(BuildCvHeader());
                foreach (var cs in groupSections) pCol.Children.Add(RenderCvSection(cs));
                return WrapPaperSheet(pCol, work, false, capturedG + 1, pageGroups.Count > 1);
            }));
        }
        return cvPages;
    }

    void AddFrontMatterTable(StackPanel target, Work work, string accentHex, IBrush ink)
    {
        var keys = EditorialTemplate.AllFactKeys(work)
            .Where(k => !string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k)))
            .ToArray();
        if (keys.Length == 0) return;
        var tbl = Column(0);
        foreach (var k in keys)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
            row.Children.Add(new Border
            {
                Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.14),
                BorderBrush = Brush.Parse("#CFD7DE"),
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(10, 5),
                Child = new TextBlock { Text = k, FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(accentHex) }
            });
            var valBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush.Parse("#CFD7DE"),
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(10, 5),
                Child = new TextBlock { Text = work.DocumentFacts[k], FontSize = 12.5, Foreground = ink, TextWrapping = TextWrapping.Wrap }
            };
            Grid.SetColumn(valBorder, 1);
            row.Children.Add(valBorder);
            tbl.Children.Add(row);
        }
        target.Children.Add(tbl);
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
                        cell.Children.Add(new Border { Height = 30, Background = Color.TryParse(hex, out var col) ? new SolidColorBrush(col) : Brushes.Gray, CornerRadius = new CornerRadius(3), BorderBrush = Brush.Parse("#BFC7CE"), BorderThickness = new Thickness(1) });
                        cell.Children.Add(new TextBlock { Text = hex, FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = inkBrush, HorizontalAlignment = HorizontalAlignment.Center });
                        if (lbl != "") cell.Children.Add(new TextBlock { Text = lbl, FontSize = 10.5, Foreground = Brush.Parse("#525C66"), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
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
                        cell.Children.Add(new TextBlock { Text = signers[c], FontSize = 12, FontWeight = FontWeight.Bold, Foreground = inkBrush, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
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
                        var tb = StyledRichLine(c < rows[r].Length ? rows[r][c].Trim() : "", header ? 12.5 : 12, header, header ? accentBrush : inkBrush, literary);
                        var cell = new Border
                        {
                            BorderBrush = Brush.Parse("#CFD7DE"),
                            BorderThickness = new Thickness(0.6),
                            Padding = new Thickness(7, 4.5),
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
                heading ? fontSize + 4.5 : sub3 ? fontSize + 1.8 : fontSize,
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
            LineHeight = size * 1.5,
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
        var pages = BuildDocumentPages(work, 760);
        int startIndex = 0;
        if (selected != null)
        {
            int found = pages.FindIndex(p => p.Section == selected);
            if (found >= 0) startIndex = found;
        }
        ReadDocumentPage(work, pages, startIndex);
    }

    internal void ReadDocumentPage(Work work, List<DocumentPageItem> pages, int pageIndex)
    {
        if (pages.Count == 0) return;
        CaptureCurrentFilmstripOffset(work.Id);
        int currentIndex = Math.Clamp(pageIndex, 0, pages.Count - 1);

        var root = new DockPanel { Focusable = true };
        Button? prevBtn = null, nextBtn = null, mode1Btn = null, mode2Btn = null, mode3Btn = null;
        ComboBox? pickerBox = null;
        ScrollViewer? stripViewer = null;
        var stageHost = new ContentControl();
        var thumbItems = new List<(Border Card, TextBlock Header, Border Rule, TextBlock Title)>();
        bool suppressPicker = false;

        Control ScaleSheet(Border sheet)
        {
            if (Math.Abs(documentZoomScale - 1.0) < 0.01) return sheet;
            double targetW = (sheet.Width > 0 ? sheet.Width : 680) * documentZoomScale;
            return new Viewbox
            {
                Width = targetW,
                Stretch = Stretch.Uniform,
                Child = sheet
            };
        }

        void RenderPageState(int targetIndex)
        {
            double keepX = (stripViewer != null && Equals(stripViewer.Tag, "Restored"))
                ? stripViewer.Offset.X
                : filmstripHorizontalOffsets.GetValueOrDefault(work.Id, 0);
            currentIndex = Math.Clamp(targetIndex, 0, pages.Count - 1);
            var activePage = pages[currentIndex];
            if (activePage.Section != null)
            {
                chapter = activePage.Section;
                RememberSection(work, activePage.Section);
            }
            int step = (documentReadingMode == 2 && pages.Count > 1) ? 2 : 1;
            if (prevBtn != null) prevBtn.IsEnabled = currentIndex > 0;
            if (nextBtn != null) nextBtn.IsEnabled = currentIndex + step < pages.Count;
            if (pickerBox != null && pickerBox.SelectedIndex != currentIndex)
            {
                suppressPicker = true;
                pickerBox.SelectedIndex = currentIndex;
                suppressPicker = false;
            }
            if (mode1Btn != null) mode1Btn.Foreground = documentReadingMode == 1 ? Accent : Quiet;
            if (mode2Btn != null) mode2Btn.Foreground = documentReadingMode == 2 ? Accent : Quiet;
            if (mode3Btn != null) mode3Btn.Foreground = documentReadingMode == 3 ? Accent : Quiet;

            for (int i = 0; i < thumbItems.Count; i++)
            {
                bool isCurrent = i == currentIndex || (documentReadingMode == 2 && i == currentIndex + 1);
                var (card, headTb, ruleBorder, titleTb) = thumbItems[i];
                card.Background = isCurrent ? EditionPalette.Mix(palette.Foil, "#FFFFFF", 0.85) : Brushes.White;
                card.BorderBrush = isCurrent ? Accent : Edge;
                card.BorderThickness = new Thickness(isCurrent ? 2.2 : 1);
                headTb.Foreground = isCurrent ? Accent : Brush.Parse("#5A6570");
                ruleBorder.Background = isCurrent ? Accent : Brush.Parse("#CFD7DE");
                titleTb.FontWeight = isCurrent ? FontWeight.SemiBold : FontWeight.Normal;
            }

            if (documentReadingMode == 3)
            {
                var allStack = Column(24);
                allStack.Margin = new Thickness(16, 8, 16, 28);
                allStack.HorizontalAlignment = HorizontalAlignment.Center;
                foreach (var p in pages)
                {
                    var pWrap = Column(6);
                    pWrap.HorizontalAlignment = HorizontalAlignment.Center;
                    pWrap.Children.Add(Kicker($"Hoja {p.PageNumber} de {pages.Count}  ·  {p.Label}"));
                    pWrap.Children.Add(ScaleSheet(p.BuildSheet()));
                    allStack.Children.Add(pWrap);
                }
                stageHost.Content = new ScrollViewer { Content = allStack };
            }
            else if (documentReadingMode == 2 && currentIndex + 1 < pages.Count)
            {
                var spreadGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(16, 8, 16, 24)
                };
                var leftPane = Column(6);
                leftPane.HorizontalAlignment = HorizontalAlignment.Center;
                leftPane.Children.Add(Kicker($"Hoja {activePage.PageNumber} de {pages.Count}  ·  {activePage.Label}"));
                leftPane.Children.Add(ScaleSheet(activePage.BuildSheet()));
                spreadGrid.Children.Add(leftPane);

                var nextPage = pages[currentIndex + 1];
                var rightPane = Column(6);
                rightPane.HorizontalAlignment = HorizontalAlignment.Center;
                rightPane.Children.Add(Kicker($"Hoja {nextPage.PageNumber} de {pages.Count}  ·  {nextPage.Label}"));
                rightPane.Children.Add(ScaleSheet(nextPage.BuildSheet()));
                Grid.SetColumn(rightPane, 1);
                spreadGrid.Children.Add(rightPane);

                stageHost.Content = new ScrollViewer { Content = spreadGrid };
            }
            else
            {
                var singleStack = Column(8);
                singleStack.Margin = new Thickness(16, 8, 16, 24);
                singleStack.HorizontalAlignment = HorizontalAlignment.Center;
                singleStack.Children.Add(Kicker($"Hoja {activePage.PageNumber} de {pages.Count}  ·  {activePage.Label}"));
                singleStack.Children.Add(ScaleSheet(activePage.BuildSheet()));
                stageHost.Content = new ScrollViewer { Content = singleStack };
            }

            if (stripViewer != null)
            {
                filmstripHorizontalOffsets[work.Id] = keepX;
                stripViewer.Offset = new Vector(keepX, 0);
            }
        }

        root.KeyDown += (_, e) =>
        {
            int step = (documentReadingMode == 2 && pages.Count > 1) ? 2 : 1;
            if (e.Key is Key.Left or Key.PageUp && currentIndex > 0)
            {
                RenderPageState(Math.Max(0, currentIndex - step));
                e.Handled = true;
            }
            else if (e.Key is Key.Right or Key.PageDown && currentIndex + step < pages.Count)
            {
                RenderPageState(Math.Min(pages.Count - 1, currentIndex + step));
                e.Handled = true;
            }
        };

        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(Action("‹ Editar", () => Editor()));
        top.Children.Add(Action("Vista previa PDF", async () => await Preview()));
        top.Children.Add(QuietAction("Exportar PDF…", async () => await Export(true)));

        prevBtn = Action("‹ Hoja anterior", () =>
        {
            int step = (documentReadingMode == 2 && pages.Count > 1) ? 2 : 1;
            RenderPageState(Math.Max(0, currentIndex - step));
        });
        nextBtn = Action("Hoja siguiente ›", () =>
        {
            int step = (documentReadingMode == 2 && pages.Count > 1) ? 2 : 1;
            RenderPageState(Math.Min(pages.Count - 1, currentIndex + step));
        });
        top.Children.Add(prevBtn);

        var pageLabels = pages.Select(p => $"Hoja {p.PageNumber} de {pages.Count} · {p.Label}").ToArray();
        pickerBox = new ComboBox { ItemsSource = pageLabels, SelectedIndex = currentIndex, MaxWidth = 300 };
        pickerBox.SelectionChanged += (_, _) =>
        {
            if (!suppressPicker && pickerBox.SelectedIndex >= 0 && pickerBox.SelectedIndex != currentIndex)
                RenderPageState(pickerBox.SelectedIndex);
        };
        top.Children.Add(pickerBox);
        top.Children.Add(nextBtn);

        mode1Btn = QuietAction("1 hoja", () => { documentReadingMode = 1; RenderPageState(currentIndex); });
        mode2Btn = QuietAction("2 hojas", () => { documentReadingMode = 2; RenderPageState(currentIndex); });
        mode3Btn = QuietAction("Todas las hojas", () => { documentReadingMode = 3; RenderPageState(currentIndex); });
        top.Children.Add(mode1Btn);
        if (pages.Count > 1) top.Children.Add(mode2Btn);
        if (pages.Count > 1) top.Children.Add(mode3Btn);

        var zoomOptions = new[] { "90 %", "100 %", "115 %" };
        var currentZoomLabel = documentZoomScale < 0.95 ? "90 %" : documentZoomScale > 1.05 ? "115 %" : "100 %";
        var zoomBox = new ComboBox { ItemsSource = zoomOptions, SelectedItem = currentZoomLabel, Width = 92 };
        zoomBox.SelectionChanged += (_, _) =>
        {
            if (zoomBox.SelectedItem is string z)
            {
                double newZoom = z == "90 %" ? 0.9 : z == "115 %" ? 1.15 : 1.0;
                if (Math.Abs(newZoom - documentZoomScale) > 0.01)
                {
                    documentZoomScale = newZoom;
                    RenderPageState(currentIndex);
                }
            }
        };
        top.Children.Add(zoomBox);

        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        if (pages.Count > 1)
        {
            var filmstrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 8, 4, 4) };
            for (int i = 0; i < pages.Count; i++)
            {
                int idx = i;
                var miniSheet = Column(4);
                miniSheet.VerticalAlignment = VerticalAlignment.Center;
                miniSheet.HorizontalAlignment = HorizontalAlignment.Center;
                var headTb = new TextBlock
                {
                    Text = $"HOJA {idx + 1}",
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#5A6570"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var ruleBorder = new Border { Width = 44, Height = 1.2, Background = Brush.Parse("#CFD7DE"), HorizontalAlignment = HorizontalAlignment.Center };
                var titleTb = new TextBlock
                {
                    Text = pages[idx].Label,
                    FontSize = 11,
                    Foreground = Brush.Parse("#1F262D"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxLines = 2,
                    MaxWidth = 96,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                miniSheet.Children.Add(headTb);
                miniSheet.Children.Add(ruleBorder);
                miniSheet.Children.Add(titleTb);
                var thumbCard = new Border
                {
                    Width = 108,
                    Height = 68,
                    Background = Brushes.White,
                    BorderBrush = Edge,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4),
                    Child = miniSheet
                };
                thumbItems.Add((thumbCard, headTb, ruleBorder, titleTb));
                var btn = new Button
                {
                    Content = thumbCard,
                    Focusable = false,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                btn.Click += (_, _) => { if (isAutomatedVerification) RenderPageState(idx); else Run(() => RenderPageState(idx)); };
                filmstrip.Children.Add(btn);
            }
            stripViewer = new ScrollViewer
            {
                Name = "FilmstripScrollViewer",
                BringIntoViewOnFocusChange = false,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = filmstrip
            };
            var initialX = filmstripHorizontalOffsets.GetValueOrDefault(work.Id, 0);
            RestoreFilmstripOffset(stripViewer, initialX);
            stripViewer.ScrollChanged += (_, _) =>
            {
                if (stripViewer.IsLoaded && Equals(stripViewer.Tag, "Restored"))
                    filmstripHorizontalOffsets[work.Id] = stripViewer.Offset.X;
            };
            DockPanel.SetDock(stripViewer, Dock.Bottom);
            root.Children.Add(stripViewer);
        }

        root.Children.Add(stageHost);
        RenderPageState(currentIndex);
        page.Content = root;
    }
}
