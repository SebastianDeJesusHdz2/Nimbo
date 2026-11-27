using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
using Nimbo.Storage;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    sealed class TurningSheet : Button
    {
        readonly DocumentModel model;
        readonly Action open;
        double start;
        Point? origin;
        bool moved;

        public TurningSheet(Entry entry, string media, EditionPalette palette, Action open)
        {
            this.open = open;
            model = new DocumentModel(entry, media, palette);
            Content = model;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        internal void ShowFace(bool reverse) => model.ShowFace(reverse);
        internal void Turn(double value) => model.Turn(value);

        protected override void OnClick() => open();
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { base.OnPointerPressed(e); return; }
            Focus(); origin = e.GetPosition(this); start = model.Angle; moved = false; e.Pointer.Capture(this); e.Handled = true;
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (origin is not Point p) return;
            var delta = e.GetPosition(this) - p; if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5) moved = true;
            if (moved) Turn(start + .018 * delta.X);
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (origin == null) { base.OnPointerReleased(e); return; }
            bool click = !moved && new Rect(Bounds.Size).Contains(e.GetPosition(this)); origin = null; e.Pointer.Capture(null); e.Handled = true; if (click) OnClick();
        }
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { origin = null; base.OnPointerCaptureLost(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key is Key.Home or Key.End) { ShowFace(e.Key == Key.End); e.Handled = true; return; }
            if (e.Key is Key.Left or Key.Right) { Turn(model.Angle + (e.Key == Key.Left ? -1 : 1) * Math.PI / 8); e.Handled = true; }
            else if (e.Key is Key.Enter or Key.Space) { open(); e.Handled = true; }
            else base.OnKeyDown(e);
        }
    }

    sealed class DocumentModel : Control
    {
        const double Rest = .20;
        readonly Entry entry;
        readonly string media;
        readonly EditionPalette palette;
        Bitmap? art;
        FormattedText? blurb;
        Geometry? blurbGeometry;
        internal double Angle { get; private set; } = Rest;

        public DocumentModel(Entry entry, string media, EditionPalette palette)
        {
            this.entry = entry; this.media = media; this.palette = palette;
            Width = 151; Height = 222;
            RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
            AttachedToVisualTree += (_, _) =>
            {
                if (entry.Cover.Length == 0) return;
                try { using var f = File.OpenRead(Path.Combine(media, Path.GetFileName(entry.Cover))); art = Bitmap.DecodeToWidth(f, 360); }
                catch (IOException) { }
                catch (ArgumentException) { }
                InvalidateVisual();
            };
            DetachedFromVisualTree += (_, _) => { art?.Dispose(); art = null; };
        }

        internal void ShowFace(bool reverse) => Turn(Rest + (reverse ? Math.PI : 0));

        internal void Turn(double value)
        {
            Angle = value % Math.Tau;
            InvalidateVisual();
        }

        static void DrawTextVector(DrawingContext dc, string text, Rect bounds, double size, IBrush ink, FontWeight weight = FontWeight.Normal, TextAlignment align = TextAlignment.Left, int maxLines = 2, bool serif = false, IBrush? shadow = null)
        {
            if (bounds.Width < 2 || string.IsNullOrWhiteSpace(text)) return;
            var family = serif ? new FontFamily("Georgia, Liberation Serif, serif") : new FontFamily("Segoe UI, Liberation Sans, sans-serif");
            var typeface = new Typeface(family, FontStyle.Normal, weight);
            FormattedText Build(double s) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, s, Brushes.White)
            {
                MaxTextWidth = bounds.Width,
                MaxTextHeight = bounds.Height,
                MaxLineCount = maxLines,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = align
            };
            var fmt = Build(size);
            if (maxLines == 1)
            {
                for (double s = size; s >= 6.5 && fmt.WidthIncludingTrailingWhitespace > bounds.Width - 1; s -= .5)
                    fmt = Build(s);
            }
            var geo = fmt.BuildGeometry(bounds.TopLeft);
            using (dc.PushClip(bounds.Inflate(1.5)))
            {
                if (geo != null)
                {
                    if (shadow != null)
                    {
                        using (dc.PushTransform(Matrix.CreateTranslation(.45, .55)))
                            dc.DrawGeometry(shadow, null, geo);
                    }
                    dc.DrawGeometry(ink, null, geo);
                }
                else dc.DrawText(fmt, bounds.TopLeft);
            }
        }

        static void DrawCoverSlice(DrawingContext dc, Bitmap img, Rect dest, double radius = 2)
        {
            using (dc.PushClip(dest))
            {
                double scale = Math.Max(dest.Width / img.Size.Width, dest.Height / img.Size.Height);
                var crop = new Size(dest.Width / scale, dest.Height / scale);
                dc.DrawImage(img, new Rect((img.Size.Width - crop.Width) / 2, (img.Size.Height - crop.Height) / 2, crop.Width, crop.Height), dest);
            }
            if (radius > 0) dc.DrawRectangle(null, new Pen(Brush.Parse("#30000000"), .6), dest, radius, radius);
        }

        static void DrawCheckmark(DrawingContext dc, Point origin, IBrush stroke)
        {
            var pen = new Pen(stroke, 1.35, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            dc.DrawLine(pen, new Point(origin.X + 1.5, origin.Y + 4.2), new Point(origin.X + 3.4, origin.Y + 6.2));
            dc.DrawLine(pen, new Point(origin.X + 3.4, origin.Y + 6.2), new Point(origin.X + 6.8, origin.Y + 1.8));
        }

        void DrawBackSynopsis(DrawingContext dc, Rect box, IBrush ink)
        {
            if (blurb == null) (blurb, blurbGeometry) = BookCover.FitSynopsis(entry.Description, entry.Title, box);
            using (dc.PushClip(box))
            {
                if (blurbGeometry != null) dc.DrawGeometry(ink, null, blurbGeometry);
                else dc.DrawText(blurb, box.TopLeft);
            }
        }

        public override void Render(DrawingContext dc)
        {
            var cos = Math.Cos(Angle); var sin = Math.Sin(Angle);
            bool slide = entry.Type == "Presentación";
            var defaultHex = entry.Type switch
            {
                "CV" => "#7A3E65",
                "Informe" => "#1B5272",
                "Artículo" => "#4A3C6E",
                "Carta" => "#9B342E",
                "Acta" => "#475E4B",
                "Apuntes" => "#376B48",
                "Propuesta" => "#364A8A",
                "Presentación" => "#1E5B88",
                _ => "#2B5B78"
            };
            bool hasCustomColor = !string.IsNullOrEmpty(entry.Color) && !entry.Color.Equals("#43676B", StringComparison.OrdinalIgnoreCase) && Color.TryParse(entry.Color, out _);
            var accentColor = hasCustomColor ? Color.Parse(entry.Color) : Color.Parse(defaultHex);
            var accentHex = accentColor.ToString();
            var accent = new SolidColorBrush(accentColor);
            var softAccent = EditionPalette.Mix(accentHex, "#FFFFFF", .76);
            var midAccent = EditionPalette.Mix(accentHex, "#FFFFFF", .38);
            var deepAccent = EditionPalette.Mix(accentHex, "#0E1318", .46);

            (double w, double h, double d, double topY, string paperHex, string rimHex) = entry.Type switch
            {
                "Presentación" => (138d, 94d, 8d, 103d, "#152535", "#435C70"),
                "Carta" => (124d, 148d, 6.5d, 49d, "#FBF1E1", "#D8C6AA"),
                "Apuntes" => (118d, 166d, 8.5d, 31d, "#FDF7DE", "#D5CBA8"),
                "Informe" => (118d, 168d, 8d, 29d, "#F2F6F9", "#C5D2DC"),
                "Artículo" => (116d, 168d, 6.5d, 29d, "#FBF8EE", "#D4CEBE"),
                "CV" => (116d, 168d, 6.5d, 29d, "#FAF8FA", "#D3C8D0"),
                "Propuesta" => (118d, 168d, 8d, 29d, "#F4F5FA", "#C8CCE0"),
                "Acta" => (116d, 168d, 8d, 29d, "#F3F6F0", "#C6CEC2"),
                _ => (116d, 166d, 7d, 31d, "#F9F7F1", "#D2CEC2")
            };

            bool darkFinish = entry.Lettering is "Ejecutiva" or "Carbón" or "Tejuelo";
            if (!slide)
            {
                paperHex = entry.Lettering switch
                {
                    "Ejecutiva" or "Carbón" => "#1B232C",
                    "Tejuelo" => "#212832",
                    "Académica" or "Oro" or "Cobre" => "#FAF1DF",
                    "Minimalista" or "Plata" => "#FFFFFF",
                    _ => paperHex
                };
            }
            var foilHex = entry.Lettering switch
            {
                "Oro" or "Tejuelo" or "Ejecutiva" => "#E5BC68",
                "Plata" => "#C6D3DF",
                "Cobre" => "#DD8D64",
                _ => accentHex
            };
            var foilBrush = Brush.Parse(foilHex);
            var paper = Brush.Parse(paperHex);
            var rimBrush = Brush.Parse(rimHex);
            var darkInk = darkFinish ? Brush.Parse("#F5F1E8") : Brush.Parse("#1D252C");
            var mutedInk = darkFinish ? Brush.Parse("#B8C3CE") : Brush.Parse("#5A6570");
            var ruleBrush = EditionPalette.Mix(accentHex, "#B8B4AA", .72);
            var secs = new[] { entry.FirstSection, entry.SecondSection, entry.ThirdSection }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var tplTag = !string.IsNullOrWhiteSpace(entry.Template) && !entry.Template.Equals("Clásica", StringComparison.OrdinalIgnoreCase)
                ? entry.Template.ToUpperInvariant()
                : entry.Type.ToUpperInvariant();
            string Initials(string source)
            {
                var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0])).ToArray();
                return parts.Length > 0 ? new string(parts) : "CV";
            }

            double depth(double x, double z) => z * cos - x * sin;
            Point Project(double x, double y, double z) => new(Bounds.Width / 2 + x * cos + z * sin, topY + y + depth(x, z) * .16);
            void Face(double x1, double z1, double x2, double z2, double length, Action draw)
            {
                var a = Project(x1, 0, z1); var b = Project(x2, 0, z2);
                if (b.X - a.X < .015) return;
                using (dc.PushTransform(new Matrix((b.X - a.X) / length, (b.Y - a.Y) / length, 0, 1, a.X, a.Y))) draw();
            }

            for (int i = 0; i < 4; i++)
                dc.DrawEllipse(Brush.Parse("#14000000"), null, new Point(Bounds.Width / 2 + 3, 201), Math.Abs(cos) * (w * .34) + 14 - i * 2, 6 - i);

            var topCap = new StreamGeometry();
            using (var ctx = topCap.Open())
            {
                ctx.BeginFigure(Project(-w / 2, 0, d / 2), true);
                ctx.LineTo(Project(w / 2, 0, d / 2));
                ctx.LineTo(Project(w / 2, 0, -d / 2));
                ctx.LineTo(Project(-w / 2, 0, -d / 2));
                ctx.EndFigure(true);
            }
            dc.DrawGeometry(rimBrush, new Pen(deepAccent, .5), topCap);

            var faces = new List<(double Z, Action Draw)>();
            void Edge(double x1, double z1, double x2, double z2, double width, Action paint)
                => faces.Add(((depth(x1, z1) + depth(x2, z2)) / 2, () => Face(x1, z1, x2, z2, width, paint)));

            Edge(-w / 2, -d / 2, -w / 2, d / 2, d, () =>
            {
                bool spineBound = entry.Type is "Informe" or "Propuesta" or "CV" or "Presentación";
                dc.DrawRectangle(spineBound ? deepAccent : rimBrush, null, new Rect(0, 0, d, h));
                if (entry.Type == "Apuntes")
                {
                    for (double y = 12; y < h - 10; y += 17)
                        dc.DrawRectangle(Brush.Parse("#8E99A2"), null, new Rect(0, y, d, 2.4), 1, 1);
                }
                else if (!spineBound)
                {
                    for (double n = 1.5; n < d - 1; n += 2)
                        dc.DrawLine(new Pen(Brush.Parse("#AEA799"), .45), new Point(n, 1), new Point(n, h - 1));
                }
            });

            Edge(w / 2, d / 2, w / 2, -d / 2, d, () =>
            {
                dc.DrawRectangle(rimBrush, null, new Rect(0, 0, d, h));
                for (double n = 1.5; n < d - 1; n += 2)
                    dc.DrawLine(new Pen(Brush.Parse("#9C9688"), .45), new Point(n, 1), new Point(n, h - 1));
            });

            void DrawFront()
            {
                var box = new Rect(0, 0, w, h);
                using (dc.PushClip(box))
                {
                    dc.DrawRectangle(slide ? EditionPalette.Mix(accentHex, "#132230", .78) : paper, null, box);

                    switch (entry.Type)
                    {
                        case "CV":
                        {
                            double sideW = 36;
                            dc.DrawRectangle(accent, null, new Rect(0, 0, sideW, h));
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#0F1419", .28), null, new Rect(0, 0, sideW, 48));
                            var avatarRect = new Rect(6, 10, 24, 24);
                            var avatarCenter = avatarRect.Center;
                            if (art != null)
                            {
                                using (dc.PushGeometryClip(new EllipseGeometry(avatarRect)))
                                    DrawCoverSlice(dc, art, avatarRect, 0);
                            }
                            else
                            {
                                dc.DrawEllipse(EditionPalette.Mix(accentHex, "#FFFFFF", .24), null, avatarCenter, 12, 12);
                                DrawTextVector(dc, Initials(entry.Author != "" ? entry.Author : entry.Title), new Rect(6, 16.5, 24, 12), 8.2, Brushes.Ivory, FontWeight.Bold, TextAlignment.Center, 1);
                            }
                            dc.DrawEllipse(null, new Pen(foilBrush, 1.2), avatarCenter, 12, 12);

                            var cityOrTag = entry.SecondaryFact != "" ? entry.SecondaryFact : "PERFIL";
                            DrawTextVector(dc, cityOrTag.ToUpperInvariant(), new Rect(4, 53, sideW - 8, 8), 5.0, softAccent, FontWeight.Bold, maxLines: 1);
                            for (int i = 0; i < 3; i++)
                            {
                                double cy = 64 + i * 8;
                                dc.DrawEllipse(softAccent, null, new Point(7, cy + 1.5), 1.5, 1.5);
                                dc.DrawRectangle(softAccent, null, new Rect(11, cy + .5, 18 - (i % 2) * 4, 2), 1, 1);
                            }
                            DrawTextVector(dc, $"{entry.ChapterCount} SECC.", new Rect(4, 94, sideW - 8, 8), 5.2, softAccent, FontWeight.Bold, maxLines: 1);
                            double[] skills = [22, 17, 24, 19];
                            for (int i = 0; i < skills.Length; i++)
                            {
                                double sy = 105 + i * 11;
                                dc.DrawRectangle(EditionPalette.Mix(accentHex, "#000000", .35), null, new Rect(5, sy, 26, 3.5), 1.5, 1.5);
                                dc.DrawRectangle(Brushes.Ivory, null, new Rect(5, sy, skills[i], 3.5), 1.5, 1.5);
                            }

                            double mainX = sideW + 7, mainW = w - sideW - 13;
                            dc.DrawRectangle(softAccent, null, new Rect(mainX, 9, mainW, 10), 2, 2);
                            var cvHeaderTag = entry.Template != "" ? "CV · " + entry.Template.ToUpperInvariant() : "CURRÍCULUM VITAE";
                            DrawTextVector(dc, cvHeaderTag, new Rect(mainX + 3, 10.5, mainW - 6, 8), 5.2, deepAccent, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(mainX, 22, mainW, 27), 11, darkInk, FontWeight.Bold, maxLines: 2);
                            var profLine = entry.PrimaryFact != "" ? entry.PrimaryFact : (entry.Subtitle != "" ? entry.Subtitle : entry.Author);
                            if (profLine != "")
                                DrawTextVector(dc, profLine, new Rect(mainX, 50, mainW, 11), 7.2, foilBrush, FontWeight.SemiBold, maxLines: 1);
                            dc.DrawLine(new Pen(foilBrush, 1.1), new Point(mainX, 63), new Point(w - 7, 63));

                            dc.DrawLine(new Pen(midAccent, 1), new Point(mainX + 3, 72), new Point(mainX + 3, h - 12));
                            string[] cvDefaultSecs = ["Perfil", "Experiencia", "Formación"];
                            for (int n = 0; n < 3; n++)
                            {
                                double ey = 71 + n * 29;
                                var secName = n < secs.Length ? secs[n] : cvDefaultSecs[n];
                                dc.DrawEllipse(accent, new Pen(Brushes.White, .8), new Point(mainX + 3, ey + 4), 3, 3);
                                dc.DrawRectangle(darkFinish ? Brush.Parse("#293440") : EditionPalette.Mix(accentHex, "#E8E2E6", .85), new Pen(foilBrush, .4), new Rect(mainX + 9, ey, mainW - 9, 23), 2, 2);
                                DrawTextVector(dc, secName.ToUpperInvariant(), new Rect(mainX + 12, ey + 2.5, mainW - 15, 8), 5.6, darkInk, FontWeight.Bold, maxLines: 1);
                                dc.DrawRectangle(foilBrush, null, new Rect(mainX + 12, ey + 11.5, Math.Min(mainW - 18, 22 + (secName.Length % 4) * 5), 2), 1, 1);
                                dc.DrawRectangle(mutedInk, null, new Rect(mainX + 12, ey + 16.5, mainW - 18, 1.8), 1, 1);
                            }
                            break;
                        }

                        case "Presentación":
                        {
                            dc.DrawRectangle(deepAccent, null, new Rect(0, 0, w, 14));
                            dc.DrawEllipse(Brush.Parse("#E06C60"), null, new Point(8, 7), 2.2, 2.2);
                            dc.DrawEllipse(Brush.Parse("#E8B858"), null, new Point(15, 7), 2.2, 2.2);
                            dc.DrawEllipse(Brush.Parse("#5CB878"), null, new Point(22, 7), 2.2, 2.2);
                            dc.DrawRectangle(accent, null, new Rect(w - 50, 2.5, 44, 9), 2, 2);
                            DrawTextVector(dc, $"16:9 · {entry.ChapterCount}P", new Rect(w - 49, 3.2, 42, 7.5), 5.3, Brushes.White, FontWeight.Bold, TextAlignment.Center, 1);

                            var slideKicker = entry.PrimaryFact != "" ? entry.PrimaryFact.ToUpperInvariant() : tplTag;
                            DrawTextVector(dc, slideKicker, new Rect(9, 17, 72, 9), 5.6, Brush.Parse("#E6AA68"), FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(9, 26, 70, 35), 11.2, Brushes.Ivory, FontWeight.Bold, maxLines: 3, shadow: Brush.Parse("#AA080E14"));
                            var slideSub = entry.Author != "" ? entry.Author : entry.Subtitle;
                            if (slideSub != "")
                                DrawTextVector(dc, slideSub, new Rect(9, 64, 70, 11), 7.2, softAccent, FontWeight.Normal, maxLines: 1);

                            var cardRect = new Rect(83, 18, 47, 58);
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#1E3448", .62), new Pen(Brush.Parse("#E6AA68"), .85), cardRect, 3, 3);
                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, cardRect.Deflate(2), 2);
                            }
                            else
                            {
                                var ringCenter = new Point(cardRect.Center.X, cardRect.Top + 16);
                                dc.DrawEllipse(null, new Pen(Brush.Parse("#36536B"), 3.5), ringCenter, 9, 9);
                                dc.DrawEllipse(null, new Pen(Brush.Parse("#F0B450"), 3.5), ringCenter, 9, 9);
                                dc.DrawEllipse(EditionPalette.Mix(accentHex, "#1E3448", .62), null, ringCenter, 5.5, 5.5);
                                (double bh, string col)[] bars = [(11, "#50B4D8"), (17, "#F0B450"), (14, "#E2735E"), (21, "#62C894")];
                                for (int b = 0; b < bars.Length; b++)
                                    dc.DrawRectangle(Brush.Parse(bars[b].col), null, new Rect(cardRect.Left + 6 + b * 9.5, cardRect.Bottom - 6 - bars[b].bh, 6.5, bars[b].bh), 1.5, 1.5);
                            }

                            dc.DrawRectangle(Brush.Parse("#0F1B26"), null, new Rect(0, h - 11, w, 11));
                            int dots = Math.Clamp(entry.ChapterCount, 2, 7);
                            double dotX = 8;
                            for (int dIdx = 0; dIdx < dots; dIdx++)
                            {
                                double dw = dIdx == 0 ? 13 : 5;
                                dc.DrawRectangle(dIdx == 0 ? Brush.Parse("#E6AA68") : Brush.Parse("#48647A"), null, new Rect(dotX, h - 7, dw, 3), 1.5, 1.5);
                                dotX += dw + 3;
                            }
                            if (entry.SecondaryFact != "")
                                DrawTextVector(dc, entry.SecondaryFact, new Rect(w - 50, h - 9, 44, 8), 5.2, softAccent, FontWeight.SemiBold, TextAlignment.Right, 1);
                            break;
                        }

                        case "Informe":
                        {
                            bool isReporte = entry.Template.Contains("Reporte", StringComparison.OrdinalIgnoreCase);
                            if (isReporte)
                            {
                                dc.DrawRectangle(accent, null, new Rect(0, 0, 7, h));
                                dc.DrawRectangle(foilBrush, null, new Rect(14, 11, w - 22, 1.4));
                                var inst = entry.PrimaryFact != "" ? entry.PrimaryFact.ToUpperInvariant() : "REPORTE TÉCNICO";
                                DrawTextVector(dc, inst, new Rect(14, 15, w - 22, 14), 5.6, foilBrush, FontWeight.Bold, TextAlignment.Center, 2);
                                dc.DrawRectangle(foilBrush, null, new Rect(26, 31, w - 46, .8));
                                DrawTextVector(dc, entry.Title, new Rect(14, 36, w - 22, 30), 11, darkInk, FontWeight.Bold, TextAlignment.Center, 2, true);
                                if (entry.Author != "")
                                    DrawTextVector(dc, "Por: " + entry.Author, new Rect(14, 66, w - 22, 10), 6.8, mutedInk, FontWeight.SemiBold, TextAlignment.Center, 1);

                                var midBox = new Rect(14, 80, w - 22, 46);
                                dc.DrawRectangle(darkFinish ? Brush.Parse("#25303B") : Brushes.White, new Pen(foilBrush, .75), midBox, 2.5, 2.5);
                                if (art != null)
                                {
                                    DrawCoverSlice(dc, art, midBox.Deflate(2), 2);
                                }
                                else
                                {
                                    DrawTextVector(dc, "CONTENIDO", new Rect(midBox.Left + 5, midBox.Top + 3, midBox.Width - 10, 8), 5.4, foilBrush, FontWeight.Bold, maxLines: 1);
                                    for (int s = 0; s < Math.Min(3, Math.Max(1, secs.Length)); s++)
                                    {
                                        var sName = s < secs.Length ? $"{s + 1}. {secs[s]}" : $"{s + 1}. Sección";
                                        DrawTextVector(dc, sName, new Rect(midBox.Left + 6, midBox.Top + 13 + s * 10, midBox.Width - 12, 9), 5.8, darkInk, FontWeight.Normal, maxLines: 1);
                                    }
                                }
                                var footFact = entry.SecondaryFact != "" ? entry.SecondaryFact : $"{entry.ChapterCount} secciones · Índice";
                                DrawTextVector(dc, footFact, new Rect(14, h - 18, w - 22, 10), 5.8, mutedInk, FontWeight.SemiBold, TextAlignment.Center, 1);
                            }
                            else
                            {
                                dc.DrawRectangle(accent, null, new Rect(0, 0, w, 25));
                                dc.DrawRectangle(deepAccent, null, new Rect(0, 0, 8, h));
                                DrawTextVector(dc, tplTag, new Rect(13, 5, 68, 9), 6.0, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                                dc.DrawRectangle(Brush.Parse("#48B884"), null, new Rect(w - 30, 6, 22, 8), 4, 4);
                                DrawTextVector(dc, $"{entry.ChapterCount} SEC", new Rect(w - 29, 6.5, 20, 7), 4.9, Brushes.White, FontWeight.Bold, TextAlignment.Center, 1);
                                dc.DrawRectangle(softAccent, null, new Rect(13, 16, 38, 2.2), 1, 1);

                                DrawTextVector(dc, entry.Title, new Rect(13, 29, w - 21, 29), 11.2, darkInk, FontWeight.Bold, maxLines: 2);
                                var infSub = entry.PrimaryFact != "" ? entry.PrimaryFact : entry.Author;
                                if (infSub != "")
                                    DrawTextVector(dc, infSub, new Rect(13, 58, w - 21, 11), 7.3, foilBrush, FontWeight.SemiBold, maxLines: 1);

                                for (int k = 0; k < 2; k++)
                                {
                                    var kpi = new Rect(13 + k * 49, 72, 45, 23);
                                    dc.DrawRectangle(darkFinish ? Brush.Parse("#26313C") : Brushes.White, new Pen(Brush.Parse("#CBD8E2"), .8), kpi, 2.5, 2.5);
                                    var kTitle = k < secs.Length ? secs[k] : (k == 0 ? "Resumen" : "Análisis");
                                    DrawTextVector(dc, kTitle.ToUpperInvariant(), new Rect(kpi.Left + 3, kpi.Top + 3, kpi.Width - 6, 8), 5.0, k == 0 ? foilBrush : Brush.Parse("#E69D45"), FontWeight.Bold, maxLines: 1);
                                    dc.DrawRectangle(Brush.Parse("#8397A5"), null, new Rect(kpi.Left + 4, kpi.Top + 12, 28, 2.2), 1, 1);
                                    dc.DrawRectangle(Brush.Parse("#B4C4CE"), null, new Rect(kpi.Left + 4, kpi.Top + 17, 20, 2), 1, 1);
                                }

                                var chartBox = new Rect(13, 100, w - 21, 57);
                                dc.DrawRectangle(darkFinish ? Brush.Parse("#26313C") : Brushes.White, new Pen(Brush.Parse("#C4D3DE"), .8), chartBox, 3, 3);
                                if (art != null)
                                {
                                    DrawCoverSlice(dc, art, chartBox.Deflate(2), 2);
                                }
                                else
                                {
                                    for (int g = 1; g <= 3; g++)
                                        dc.DrawLine(new Pen(Brush.Parse("#E6EEF3"), .7), new Point(chartBox.Left + 5, chartBox.Top + g * 12), new Point(chartBox.Right - 5, chartBox.Top + g * 12));
                                    (double hBar, IBrush col)[] bars = [(18, midAccent), (29, accent), (23, Brush.Parse("#E69D45")), (37, accent), (32, Brush.Parse("#3FA796"))];
                                    var pts = new Point[bars.Length];
                                    for (int b = 0; b < bars.Length; b++)
                                    {
                                        double bx = chartBox.Left + 8 + b * 16.5;
                                        double by = chartBox.Bottom - 6 - bars[b].hBar;
                                        dc.DrawRectangle(bars[b].col, null, new Rect(bx, by, 10.5, bars[b].hBar), 1.5, 1.5);
                                        pts[b] = new Point(bx + 5.2, by - 3);
                                    }
                                    var trendPen = new Pen(Brush.Parse("#D95F43"), 1.3);
                                    for (int b = 0; b < pts.Length - 1; b++) dc.DrawLine(trendPen, pts[b], pts[b + 1]);
                                    foreach (var pt in pts) dc.DrawEllipse(Brushes.White, trendPen, pt, 2, 2);
                                }
                            }
                            break;
                        }

                        case "Apuntes":
                        {
                            bool isLibreta = entry.Template.Contains("Libreta", StringComparison.OrdinalIgnoreCase);
                            for (double y = 26; y < h - 10; y += 10.5)
                                dc.DrawLine(new Pen(Brush.Parse(darkFinish ? "#2E3A30" : "#D8DFC2"), .75), new Point(12, y), new Point(w - 6, y));
                            dc.DrawLine(new Pen(Brush.Parse("#D96459"), 1.15), new Point(29, 0), new Point(29, h));
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#1F3325", .55), null, new Rect(0, 0, 11, h));
                            for (double y = 13; y < h - 10; y += 16)
                            {
                                dc.DrawEllipse(Brush.Parse("#363A32"), null, new Point(14.5, y), 2.4, 2.4);
                                dc.DrawRectangle(Brush.Parse("#C8D0D8"), new Pen(Brush.Parse("#6E7880"), .5), new Rect(1, y - 1.4, 13.5, 2.8), 1.2, 1.2);
                            }

                            var apBadge = isLibreta ? "LIBRETA" : (entry.Template.Contains("Cornell", StringComparison.OrdinalIgnoreCase) ? "CORNELL" : "APUNTES");
                            dc.DrawRectangle(accent, null, new Rect(33, 8, 52, 11), 2, 2);
                            DrawTextVector(dc, apBadge, new Rect(35, 9.5, 48, 8), 5.8, Brushes.Ivory, FontWeight.Bold, TextAlignment.Center, 1);

                            if (entry.SecondaryFact != "")
                                DrawTextVector(dc, entry.SecondaryFact, new Rect(33, 21, w - 39, 9), 5.6, foilBrush, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(33, 30, w - 39, 28), 11, darkInk, FontWeight.Bold, maxLines: 2);
                            var apSub = entry.PrimaryFact != "" ? entry.PrimaryFact : (entry.Author != "" ? entry.Author : entry.Subtitle);
                            if (apSub != "")
                                DrawTextVector(dc, apSub, new Rect(33, 58, w - 39, 10), 7.0, foilBrush, FontWeight.SemiBold, maxLines: 1);

                            for (int i = 0; i < 3; i++)
                            {
                                double ry = 73 + i * 11;
                                dc.DrawEllipse(accent, null, new Point(21, ry + 3), 2, 2);
                                if (i < secs.Length)
                                    DrawTextVector(dc, $"{i + 1}. {secs[i]}", new Rect(33, ry, w - 40, 9), 6.2, darkInk, FontWeight.SemiBold, maxLines: 1);
                                else
                                    dc.DrawRectangle(mutedInk, null, new Rect(33, ry + 2, w - 45 - (i % 2) * 12, 2), 1, 1);
                            }

                            var sticky = new Rect(33, 108, w - 41, 48);
                            if (art != null)
                            {
                                dc.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#C8C0A8"), .8), sticky, 2, 2);
                                DrawCoverSlice(dc, art, sticky.Deflate(2.5), 1.5);
                            }
                            else
                            {
                                dc.DrawRectangle(Brush.Parse("#22000000"), null, sticky.Translate(new Vector(1.5, 2)), 2, 2);
                                dc.DrawRectangle(Brush.Parse("#FFE98A"), new Pen(Brush.Parse("#DFC458"), .8), sticky, 2, 2);
                                dc.DrawEllipse(Brush.Parse("#D9534F"), new Pen(Brushes.White, .6), new Point(sticky.Center.X, sticky.Top + 4), 2.8, 2.8);
                                var noteText = entry.Description != "" ? entry.Description : $"{entry.ChapterCount} temas registrados en el cuaderno.";
                                DrawTextVector(dc, noteText, new Rect(sticky.Left + 5, sticky.Top + 10, sticky.Width - 10, sticky.Height - 13), 6.2, Brush.Parse("#3A3218"), FontWeight.SemiBold, maxLines: 4);
                            }
                            break;
                        }

                        case "Artículo":
                        {
                            dc.DrawLine(new Pen(foilBrush, 1.8), new Point(9, 10), new Point(w - 9, 10));
                            dc.DrawLine(new Pen(foilBrush, .7), new Point(9, 13), new Point(w - 9, 13));
                            var artHeader = entry.Template != "" && entry.Template != "Clásica" ? entry.Template.ToUpperInvariant() : "ARTÍCULO · SEPARATA";
                            DrawTextVector(dc, artHeader, new Rect(9, 15.5, w - 18, 9), 5.5, foilBrush, FontWeight.Bold, TextAlignment.Center, 1);
                            DrawTextVector(dc, entry.Title, new Rect(10, 25, w - 20, 28), 10.8, darkInk, FontWeight.Bold, TextAlignment.Center, 2, true);
                            var artAuthor = entry.Author != "" ? entry.Author : entry.PrimaryFact;
                            if (artAuthor != "")
                                DrawTextVector(dc, artAuthor, new Rect(10, 54, w - 20, 10), 7.2, mutedInk, FontWeight.Normal, TextAlignment.Center, 1, true);

                            var abstractBox = new Rect(11, 66, w - 22, 23);
                            dc.DrawRectangle(darkFinish ? Brush.Parse("#26303A") : EditionPalette.Mix(accentHex, "#F2EFE6", .85), null, abstractBox);
                            dc.DrawLine(new Pen(foilBrush, 1.2), abstractBox.TopLeft, abstractBox.BottomLeft);
                            dc.DrawLine(new Pen(foilBrush, 1.2), abstractBox.TopRight, abstractBox.BottomRight);
                            var absText = entry.PrimaryFact != "" ? entry.PrimaryFact : (entry.Description != "" ? entry.Description : "Resumen y metodología.");
                            DrawTextVector(dc, absText, new Rect(15, 68.5, w - 30, 18), 5.8, darkInk, FontWeight.Normal, maxLines: 2, serif: true);

                            double colTop = 94, colMid = w / 2;
                            dc.DrawLine(new Pen(Brush.Parse("#D4CEBE"), .8), new Point(colMid, colTop), new Point(colMid, h - 12));
                            var figRect = new Rect(10, colTop, colMid - 14, 28);
                            if (art != null)
                                DrawCoverSlice(dc, art, figRect, 1.5);
                            else
                            {
                                dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E8E4DA", .75), new Pen(accent, .7), figRect, 1.5, 1.5);
                                dc.DrawLine(new Pen(accent, 1.1), new Point(figRect.Left + 4, figRect.Bottom - 6), new Point(figRect.Left + 16, figRect.Top + 10));
                                dc.DrawLine(new Pen(accent, 1.1), new Point(figRect.Left + 16, figRect.Top + 10), new Point(figRect.Left + 26, figRect.Bottom - 11));
                                dc.DrawLine(new Pen(accent, 1.1), new Point(figRect.Left + 26, figRect.Bottom - 11), new Point(figRect.Right - 4, figRect.Top + 6));
                            }
                            if (secs.Length > 0)
                                DrawTextVector(dc, "1. " + secs[0], new Rect(10, colTop + 31, colMid - 14, 9), 5.5, foilBrush, FontWeight.Bold, maxLines: 1);
                            if (secs.Length > 1)
                                DrawTextVector(dc, "2. " + secs[1], new Rect(colMid + 4, colTop, colMid - 14, 9), 5.5, foilBrush, FontWeight.Bold, maxLines: 1);
                            for (int r = 0; r < 7; r++)
                            {
                                double ry = colTop + 11 + r * 6.8;
                                if (r >= 4)
                                    dc.DrawRectangle(mutedInk, null, new Rect(10, ry, colMid - 15 - (r % 2) * 6, 1.7), 1, 1);
                                dc.DrawRectangle(mutedInk, null, new Rect(colMid + 4, ry, colMid - 14 - ((r + 1) % 3) * 5, 1.7), 1, 1);
                            }
                            break;
                        }

                        case "Carta":
                        {
                            dc.DrawRectangle(null, new Pen(EditionPalette.Mix(accentHex, "#C8B498", .62), 1), new Rect(6, 6, w - 12, h - 12));
                            var stampBox = new Rect(w - 31, 11, 19, 23);
                            dc.DrawRectangle(Brushes.White, new Pen(accent, .9), stampBox);
                            if (art != null)
                                DrawCoverSlice(dc, art, stampBox.Deflate(2), 0);
                            else
                            {
                                dc.DrawRectangle(softAccent, null, stampBox.Deflate(2.5));
                                dc.DrawEllipse(accent, null, stampBox.Center, 4.5, 4.5);
                            }
                            var postmarkPen = new Pen(Brush.Parse("#88524438"), .7);
                            dc.DrawEllipse(null, postmarkPen, new Point(w - 33, 24), 7, 7);
                            for (int wv = 0; wv < 3; wv++)
                                dc.DrawLine(postmarkPen, new Point(w - 50, 20 + wv * 4), new Point(w - 34, 20 + wv * 4));

                            var cartaTag = entry.Template != "" && entry.Template != "Clásica" ? entry.Template.ToUpperInvariant() : "CARTA FORMAL";
                            DrawTextVector(dc, cartaTag, new Rect(13, 12, 68, 9), 5.6, foilBrush, FontWeight.Bold, maxLines: 1);
                            var dest = entry.PrimaryFact != "" ? "Para: " + entry.PrimaryFact : (entry.SecondaryFact != "" ? entry.SecondaryFact : "Correspondencia oficial");
                            DrawTextVector(dc, dest, new Rect(13, 22, 72, 15), 6.0, mutedInk, FontWeight.SemiBold, maxLines: 2);

                            DrawTextVector(dc, entry.Title, new Rect(13, 39, w - 26, 29), 11.2, darkInk, FontWeight.Bold, maxLines: 2, serif: true);
                            if (entry.Description != "")
                            {
                                DrawTextVector(dc, entry.Description, new Rect(13, 70, w - 26, 42), 6.2, mutedInk, FontWeight.Normal, maxLines: 4, serif: true);
                            }
                            else
                            {
                                for (int l = 0; l < 5; l++)
                                    dc.DrawRectangle(Brush.Parse("#B8A68E"), null, new Rect(l == 0 ? 22 : 13, 74 + l * 7.5, w - (l == 0 ? 35 : 26) - (l == 4 ? 18 : 0), 1.6), 1, 1);
                            }

                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(13, h - 25, w - 46, 12), 8.0, foilBrush, FontWeight.SemiBold, maxLines: 1, serif: true);
                            else
                                dc.DrawLine(new Pen(foilBrush, 1.2), new Point(13, h - 18), new Point(54, h - 18));

                            var sealCenter = new Point(w - 23, h - 22);
                            dc.DrawEllipse(Brush.Parse("#30000000"), null, sealCenter + new Vector(1, 1.5), 11, 11);
                            dc.DrawEllipse(accent, new Pen(deepAccent, 1), sealCenter, 10.5, 10.5);
                            dc.DrawEllipse(null, new Pen(Brush.Parse("#F2D29B"), .8), sealCenter, 7.2, 7.2);
                            var sealInit = Initials(entry.Author != "" ? entry.Author : entry.Title)[..1];
                            DrawTextVector(dc, sealInit, new Rect(sealCenter.X - 6, sealCenter.Y - 4.5, 12, 9), 6.5, Brush.Parse("#F2D29B"), FontWeight.Bold, TextAlignment.Center, 1, true);
                            break;
                        }

                        case "Propuesta":
                        {
                            dc.DrawRectangle(accent, null, new Rect(0, 0, 10, h));
                            foreach (var ry in new[] { 22d, h / 2, h - 22d })
                                dc.DrawEllipse(Brushes.Ivory, new Pen(deepAccent, .7), new Point(5, ry), 2, 2);

                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E4E7F4", .78), null, new Rect(10, 0, w - 10, 22));
                            var propTag = entry.Template.Contains("GDD", StringComparison.OrdinalIgnoreCase) ? "PROPUESTA · GDD" : (entry.Template.Contains("Cotización", StringComparison.OrdinalIgnoreCase) ? "COTIZACIÓN" : "PROPUESTA");
                            DrawTextVector(dc, propTag, new Rect(16, 6.5, w - 22, 9), 5.8, accent, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(16, 26, w - 24, 29), 11.2, darkInk, FontWeight.Bold, maxLines: 2);
                            var clientOrAuthor = entry.PrimaryFact != "" ? entry.PrimaryFact : entry.Author;
                            if (clientOrAuthor != "")
                                DrawTextVector(dc, clientOrAuthor, new Rect(16, 55, w - 24, 11), 7.2, foilBrush, FontWeight.SemiBold, maxLines: 1);

                            var gantt = new Rect(16, 70, w - 24, 64);
                            dc.DrawRectangle(darkFinish ? Brush.Parse("#26303C") : Brushes.White, new Pen(Brush.Parse("#C8CEE0"), .8), gantt, 3, 3);
                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, gantt.Deflate(2), 2);
                            }
                            else
                            {
                                for (int c = 1; c <= 3; c++)
                                    dc.DrawLine(new Pen(Brush.Parse("#E8EBF4"), .8), new Point(gantt.Left + c * (gantt.Width / 4), gantt.Top + 4), new Point(gantt.Left + c * (gantt.Width / 4), gantt.Bottom - 4));
                                (double left, double widthBar, string col)[] milestones = [(6, 54, accentHex), (14, 58, "#E06D53"), (24, 56, "#E5A93C"), (36, 48, "#38988E")];
                                for (int m = 0; m < milestones.Length; m++)
                                {
                                    double my = gantt.Top + 7 + m * 14;
                                    var barRect = new Rect(gantt.Left + milestones[m].left, my, milestones[m].widthBar, 10);
                                    dc.DrawRectangle(Brush.Parse(milestones[m].col), null, barRect, 3.5, 3.5);
                                    var mLabel = m < secs.Length ? secs[m] : $"Fase {m + 1}";
                                    DrawTextVector(dc, mLabel, barRect.Deflate(new Thickness(2, 1)), 5.2, Brushes.White, FontWeight.Bold, maxLines: 1);
                                }
                            }

                            var budgetPill = new Rect(16, h - 24, w - 24, 14);
                            dc.DrawRectangle(accent, null, budgetPill, 3, 3);
                            var pillText = entry.SecondaryFact != "" ? entry.SecondaryFact : $"{entry.ChapterCount} bloques · Alcance";
                            DrawTextVector(dc, pillText, new Rect(budgetPill.Left + 5, budgetPill.Top + 3, budgetPill.Width - 28, 9), 5.5, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                            dc.DrawRectangle(Brush.Parse("#F5C862"), null, new Rect(budgetPill.Right - 20, budgetPill.Top + 4, 15, 6), 3, 3);
                            break;
                        }

                        case "Acta":
                        {
                            dc.DrawRectangle(null, new Pen(foilBrush, 1.1), new Rect(7, 10, w - 14, h - 17));
                            dc.DrawRectangle(Brush.Parse("#747E78"), new Pen(Brush.Parse("#48504B"), .8), new Rect(w / 2 - 18, 3, 36, 9), 2, 2);
                            dc.DrawRectangle(Brush.Parse("#D8E0DA"), null, new Rect(w / 2 - 10, 5.5, 20, 3.5), 1.5, 1.5);

                            var headBox = new Rect(12, 16, w - 24, 15);
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E8ECE4", .82), new Pen(accent, .8), headBox, 1.5, 1.5);
                            var actaTag = entry.Template != "" && entry.Template != "Clásica" ? entry.Template.ToUpperInvariant() : "ACTA DE SESIÓN";
                            DrawTextVector(dc, actaTag, new Rect(14, 19.5, w - 28, 9), 5.5, accent, FontWeight.Bold, TextAlignment.Center, 1);

                            DrawTextVector(dc, entry.Title, new Rect(13, 35, w - 26, 28), 11, darkInk, FontWeight.Bold, maxLines: 2);
                            var actaMeta = entry.PrimaryFact != "" ? entry.PrimaryFact : entry.Author;
                            if (actaMeta != "")
                                DrawTextVector(dc, actaMeta, new Rect(13, 63, w - 26, 11), 7.0, foilBrush, FontWeight.SemiBold, maxLines: 1);

                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, new Rect(13, 77, w - 26, 49), 2);
                            }
                            else
                            {
                                string[] defaultPts = ["Quórum y apertura", "Orden del día", "Acuerdos firmados"];
                                for (int a = 0; a < 3; a++)
                                {
                                    double ay = 79 + a * 16;
                                    var chk = new Rect(13, ay, 10, 10);
                                    dc.DrawRectangle(softAccent, new Pen(accent, 1.1), chk, 1.5, 1.5);
                                    DrawCheckmark(dc, chk.TopLeft, accent);
                                    var ptTitle = a < secs.Length ? secs[a] : defaultPts[a];
                                    DrawTextVector(dc, ptTitle, new Rect(27, ay + 1, w - 40, 9), 6.2, darkInk, FontWeight.SemiBold, maxLines: 1);
                                }
                            }

                            dc.DrawLine(new Pen(Brush.Parse("#7C857A"), 1), new Point(13, h - 18), new Point(44, h - 18));
                            dc.DrawLine(new Pen(Brush.Parse("#7C857A"), 1), new Point(w - 44, h - 18), new Point(w - 13, h - 18));
                            var stampCenter = new Point(w / 2, h - 22);
                            dc.DrawEllipse(null, new Pen(foilBrush, 1.2), stampCenter, 11, 11);
                            dc.DrawEllipse(null, new Pen(foilBrush, .6), stampCenter, 8, 8);
                            dc.DrawRectangle(foilBrush, null, new Rect(stampCenter.X - 6, stampCenter.Y - 1.2, 12, 2.4), 1, 1);
                            break;
                        }

                        default:
                        {
                            double fold = 18;
                            dc.DrawRectangle(accent, null, new Rect(0, 0, w, 22));
                            var foldBg = new StreamGeometry();
                            using (var g = foldBg.Open())
                            {
                                g.BeginFigure(new Point(w - fold, 0), true);
                                g.LineTo(new Point(w, 0));
                                g.LineTo(new Point(w, fold));
                                g.EndFigure(true);
                            }
                            dc.DrawGeometry(EditionPalette.Mix(accentHex, "#10161C", .55), null, foldBg);
                            var ear = new StreamGeometry();
                            using (var g = ear.Open())
                            {
                                g.BeginFigure(new Point(w - fold, 0), true);
                                g.LineTo(new Point(w - fold, fold));
                                g.LineTo(new Point(w, fold));
                                g.EndFigure(true);
                            }
                            dc.DrawGeometry(Brush.Parse("#E5E0D5"), new Pen(Brush.Parse("#B8B2A4"), .7), ear);

                            var docTag = entry.Template != "" && entry.Template != "Clásica" ? entry.Template.ToUpperInvariant() : "DOCUMENTO";
                            DrawTextVector(dc, docTag, new Rect(11, 7, w - 31, 9), 5.6, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(11, 27, w - 22, 30), 11.4, darkInk, FontWeight.Bold, maxLines: 2);
                            var docSub = entry.PrimaryFact != "" ? entry.PrimaryFact : (entry.Author != "" ? entry.Author : entry.Subtitle);
                            if (docSub != "")
                                DrawTextVector(dc, docSub, new Rect(11, 57, w - 22, 11), 7.2, foilBrush, FontWeight.SemiBold, maxLines: 1);
                            dc.DrawLine(new Pen(foilBrush, 1), new Point(11, 70), new Point(w - 11, 70));

                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, new Rect(11, 76, w - 22, 48), 2);
                            }
                            else
                            {
                                var quoteBox = new Rect(11, 76, w - 22, 27);
                                dc.DrawRectangle(darkFinish ? Brush.Parse("#25303A") : EditionPalette.Mix(accentHex, "#EAE6DC", .84), null, quoteBox, 2, 2);
                                dc.DrawRectangle(foilBrush, null, new Rect(11, 76, 3, 27), 1, 1);
                                var snippet = entry.Description != "" ? entry.Description : (secs.Length > 0 ? string.Join(" · ", secs) : entry.Title);
                                DrawTextVector(dc, snippet, new Rect(18, 79, w - 34, 21), 6.0, darkInk, FontWeight.Normal, maxLines: 3);
                            }

                            for (int n = 0; n < 3; n++)
                            {
                                double ry = 111 + n * 11;
                                if (n < secs.Length)
                                    DrawTextVector(dc, $"{n + 1}. {secs[n]}", new Rect(11, ry, w - 22, 9), 6.2, darkInk, FontWeight.SemiBold, maxLines: 1);
                                else
                                    dc.DrawRectangle(mutedInk, null, new Rect(11, ry + 3, w - 22 - (n % 2) * 14, 1.8), 1, 1);
                            }
                            DrawTextVector(dc, $"— 1 / {entry.ChapterCount} —", new Rect(11, h - 13, w - 22, 9), 6.0, mutedInk, FontWeight.SemiBold, TextAlignment.Center, 1);
                            break;
                        }
                    }

                    dc.DrawRectangle(BookCover.ClothLight, null, box);
                    dc.DrawRectangle(null, new Pen(EditionPalette.Mix(accentHex, "#7A766E", .5), .85), box);
                }
            }

            void DrawBack()
            {
                var box = new Rect(0, 0, w, h);
                using (dc.PushClip(box))
                {
                    dc.DrawRectangle(slide ? EditionPalette.Mix(accentHex, "#132230", .78) : paper, null, box);
                    if (entry.Type == "CV")
                        dc.DrawRectangle(accent, null, new Rect(w - 12, 0, 12, h));
                    else if (entry.Type == "Apuntes")
                    {
                        for (double y = 24; y < h - 10; y += 10.5)
                            dc.DrawLine(new Pen(Brush.Parse("#D8DFC2"), .75), new Point(8, y), new Point(w - 14, y));
                        dc.DrawRectangle(EditionPalette.Mix(accentHex, "#1F3325", .55), null, new Rect(w - 11, 0, 11, h));
                        for (double y = 13; y < h - 10; y += 16)
                            dc.DrawRectangle(Brush.Parse("#C8D0D8"), null, new Rect(w - 14, y - 1.4, 13, 2.8), 1.2, 1.2);
                    }
                    else if (entry.Type is "Informe" or "Propuesta")
                        dc.DrawRectangle(accent, null, new Rect(w - 8, 0, 8, h));

                    double padL = 10, padR = entry.Type is "CV" or "Apuntes" or "Informe" or "Propuesta" ? 18 : 10;
                    if (entry.Type == "Carta")
                    {
                        var flap = new StreamGeometry();
                        using (var g = flap.Open())
                        {
                            g.BeginFigure(new Point(2, 2), false);
                            g.LineTo(new Point(w / 2, 46));
                            g.LineTo(new Point(w - 2, 2));
                            g.EndFigure(false);
                        }
                        dc.DrawGeometry(EditionPalette.Mix(accentHex, "#EFE2CC", .88), new Pen(Brush.Parse("#C4B296"), 1.1), flap);
                        var wax = new Point(w / 2, 44);
                        dc.DrawEllipse(accent, new Pen(deepAccent, 1), wax, 10, 10);
                        dc.DrawEllipse(null, new Pen(Brush.Parse("#F2D29B"), .8), wax, 6.8, 6.8);
                    }
                    else
                    {
                        dc.DrawRectangle(accent, null, new Rect(padL, 10, w - padL - padR, 3), 1, 1);
                        DrawTextVector(dc, entry.Type.ToUpperInvariant() + " · SÍNTESIS", new Rect(padL, 16, w - padL - padR, 9), 5.8, slide ? softAccent : accent, FontWeight.Bold, maxLines: 1);
                    }

                    double synTop = entry.Type == "Carta" ? 58 : (slide ? 27 : 29);
                    double synH = Math.Max(24, h - synTop - (slide ? 18 : 24));
                    var synBox = new Rect(padL, synTop, w - padL - padR, synH);
                    DrawBackSynopsis(dc, synBox, slide ? Brushes.Ivory : darkInk);

                    if (entry.Author != "")
                        DrawTextVector(dc, entry.Author, new Rect(padL, h - (slide ? 15 : 19), w - padL - padR, 11), slide ? 7.5 : 8.2, slide ? softAccent : accent, FontWeight.SemiBold, TextAlignment.Center, 1);

                    dc.DrawRectangle(BookCover.ClothLight, null, box);
                    dc.DrawRectangle(null, new Pen(EditionPalette.Mix(accentHex, "#7A766E", .5), .85), box);
                }
            }

            Edge(-w / 2, d / 2, w / 2, d / 2, w, DrawFront);
            Edge(w / 2, -d / 2, -w / 2, -d / 2, w, DrawBack);

            foreach (var face in faces.OrderBy(f => f.Z)) face.Draw();
        }
    }

    Control Miniature(Entry entry, Action open)
    {
        if (entry.Type is "Novela" or "Historia") return new BookCover(entry, shelf.Media, palette, open);
        bool slide = entry.Type == "Presentación";
        var button = new TurningSheet(entry, shelf.Media, palette, open)
        {
            Name = slide ? "MiniDiapositiva" : "MiniDocumento",
            Tag = entry.Type,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        Avalonia.Automation.AutomationProperties.SetName(button, entry.Title);
        ToolTip.SetTip(button, entry.Title + " (" + entry.Type + ") · Arrastra para girar o usa las flechas. Inicio y Fin muestran cada cara.");
        return button;
    }
}



