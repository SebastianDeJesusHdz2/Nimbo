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

            var paper = Brush.Parse(paperHex);
            var rimBrush = Brush.Parse(rimHex);
            var darkInk = Brush.Parse("#1D252C");
            var mutedInk = Brush.Parse("#5A6570");
            var ruleBrush = EditionPalette.Mix(accentHex, "#B8B4AA", .72);

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
                                dc.DrawEllipse(Brushes.Ivory, null, new Point(avatarCenter.X, avatarCenter.Y - 3.5), 4.2, 4.2);
                                dc.DrawGeometry(Brushes.Ivory, null, Geometry.Parse($"M {avatarCenter.X - 7.5},{avatarCenter.Y + 9} C {avatarCenter.X - 7.5},{avatarCenter.Y + 3} {avatarCenter.X + 7.5},{avatarCenter.Y + 3} {avatarCenter.X + 7.5},{avatarCenter.Y + 9} Z"));
                            }
                            dc.DrawEllipse(null, new Pen(Brushes.Ivory, 1.2), avatarCenter, 12, 12);

                            DrawTextVector(dc, "PERFIL", new Rect(5, 53, sideW - 10, 8), 5.5, softAccent, FontWeight.Bold, maxLines: 1);
                            for (int i = 0; i < 3; i++)
                            {
                                double cy = 64 + i * 8;
                                dc.DrawEllipse(softAccent, null, new Point(7, cy + 1.5), 1.5, 1.5);
                                dc.DrawRectangle(softAccent, null, new Rect(11, cy + .5, 18 - (i % 2) * 4, 2), 1, 1);
                            }
                            DrawTextVector(dc, "APTITUDES", new Rect(5, 94, sideW - 10, 8), 5.2, softAccent, FontWeight.Bold, maxLines: 1);
                            double[] skills = [21, 16, 24, 18];
                            for (int i = 0; i < skills.Length; i++)
                            {
                                double sy = 105 + i * 11;
                                dc.DrawRectangle(EditionPalette.Mix(accentHex, "#000000", .35), null, new Rect(5, sy, 26, 3.5), 1.5, 1.5);
                                dc.DrawRectangle(Brushes.Ivory, null, new Rect(5, sy, skills[i], 3.5), 1.5, 1.5);
                            }

                            double mainX = sideW + 7, mainW = w - sideW - 13;
                            dc.DrawRectangle(softAccent, null, new Rect(mainX, 10, 48, 10), 2, 2);
                            DrawTextVector(dc, "CURRÍCULUM", new Rect(mainX + 3, 11.5, 44, 8), 5.8, deepAccent, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(mainX, 24, mainW, 30), 11.5, darkInk, FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(mainX, 53, mainW, 11), 7.8, accent, FontWeight.SemiBold, maxLines: 1);
                            dc.DrawLine(new Pen(accent, 1.1), new Point(mainX, 66), new Point(w - 7, 66));

                            dc.DrawLine(new Pen(midAccent, 1), new Point(mainX + 3, 75), new Point(mainX + 3, h - 14));
                            for (int n = 0; n < 3; n++)
                            {
                                double ey = 74 + n * 28;
                                dc.DrawEllipse(accent, new Pen(Brushes.White, .8), new Point(mainX + 3, ey + 3), 3, 3);
                                dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E8E2E6", .85), null, new Rect(mainX + 9, ey, mainW - 9, 21), 2, 2);
                                dc.DrawRectangle(darkInk, null, new Rect(mainX + 13, ey + 4, mainW - 22, 2.5), 1, 1);
                                dc.DrawRectangle(accent, null, new Rect(mainX + 13, ey + 9.5, 24, 2), 1, 1);
                                dc.DrawRectangle(mutedInk, null, new Rect(mainX + 13, ey + 14.5, mainW - 18, 1.8), 1, 1);
                            }
                            break;
                        }

                        case "Presentación":
                        {
                            dc.DrawRectangle(deepAccent, null, new Rect(0, 0, w, 14));
                            dc.DrawEllipse(Brush.Parse("#E06C60"), null, new Point(8, 7), 2.2, 2.2);
                            dc.DrawEllipse(Brush.Parse("#E8B858"), null, new Point(15, 7), 2.2, 2.2);
                            dc.DrawEllipse(Brush.Parse("#5CB878"), null, new Point(22, 7), 2.2, 2.2);
                            dc.DrawRectangle(accent, null, new Rect(w - 34, 3, 28, 8), 2, 2);
                            DrawTextVector(dc, "16:9", new Rect(w - 33, 3.5, 26, 7), 5.8, Brushes.White, FontWeight.Bold, TextAlignment.Center, 1);

                            DrawTextVector(dc, "PRESENTACIÓN", new Rect(9, 18, 72, 9), 6.2, midAccent, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(9, 28, 70, 36), 12, Brushes.Ivory, FontWeight.Bold, maxLines: 3, shadow: Brush.Parse("#AA080E14"));
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(9, 66, 70, 11), 7.8, softAccent, FontWeight.Normal, maxLines: 1);

                            var cardRect = new Rect(83, 19, 47, 56);
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#1E3448", .62), new Pen(midAccent, .8), cardRect, 3, 3);
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

                            dc.DrawRectangle(Brush.Parse("#0F1B26"), null, new Rect(0, h - 10, w, 10));
                            dc.DrawRectangle(accent, null, new Rect(8, h - 6.5, 14, 3), 1.5, 1.5);
                            dc.DrawRectangle(Brush.Parse("#48647A"), null, new Rect(25, h - 6.5, 5, 3), 1.5, 1.5);
                            dc.DrawRectangle(Brush.Parse("#48647A"), null, new Rect(33, h - 6.5, 5, 3), 1.5, 1.5);
                            break;
                        }

                        case "Informe":
                        {
                            dc.DrawRectangle(accent, null, new Rect(0, 0, w, 25));
                            dc.DrawRectangle(deepAccent, null, new Rect(0, 0, 8, h));
                            DrawTextVector(dc, "INFORME", new Rect(13, 5, 64, 9), 6.5, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                            dc.DrawRectangle(Brush.Parse("#48B884"), null, new Rect(w - 28, 6, 20, 8), 4, 4);
                            DrawTextVector(dc, "KPI", new Rect(w - 27, 6.5, 18, 7), 5.2, Brushes.White, FontWeight.Bold, TextAlignment.Center, 1);
                            dc.DrawRectangle(softAccent, null, new Rect(13, 16, 36, 2.2), 1, 1);

                            DrawTextVector(dc, entry.Title, new Rect(13, 30, w - 21, 30), 11.5, darkInk, FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(13, 59, w - 21, 11), 7.8, accent, FontWeight.SemiBold, maxLines: 1);

                            for (int k = 0; k < 2; k++)
                            {
                                var kpi = new Rect(13 + k * 49, 73, 45, 22);
                                dc.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#CBD8E2"), .8), kpi, 2.5, 2.5);
                                dc.DrawRectangle(k == 0 ? accent : Brush.Parse("#E69D45"), null, new Rect(kpi.Left + 4, kpi.Top + 4, 16, 4), 1.5, 1.5);
                                dc.DrawRectangle(Brush.Parse("#8397A5"), null, new Rect(kpi.Left + 4, kpi.Top + 11, 28, 2.2), 1, 1);
                                dc.DrawRectangle(Brush.Parse("#B4C4CE"), null, new Rect(kpi.Left + 4, kpi.Top + 16, 20, 2), 1, 1);
                            }

                            var chartBox = new Rect(13, 101, w - 21, 56);
                            dc.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#C4D3DE"), .8), chartBox, 3, 3);
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
                            break;
                        }

                        case "Apuntes":
                        {
                            for (double y = 26; y < h - 10; y += 10.5)
                                dc.DrawLine(new Pen(Brush.Parse("#D8DFC2"), .75), new Point(12, y), new Point(w - 6, y));
                            dc.DrawLine(new Pen(Brush.Parse("#D96459"), 1.15), new Point(31, 0), new Point(31, h));
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#1F3325", .55), null, new Rect(0, 0, 11, h));
                            for (double y = 13; y < h - 10; y += 16)
                            {
                                dc.DrawEllipse(Brush.Parse("#363A32"), null, new Point(14.5, y), 2.4, 2.4);
                                dc.DrawRectangle(Brush.Parse("#C8D0D8"), new Pen(Brush.Parse("#6E7880"), .5), new Rect(1, y - 1.4, 13.5, 2.8), 1.2, 1.2);
                            }

                            dc.DrawRectangle(accent, null, new Rect(35, 9, 44, 11), 2, 2);
                            DrawTextVector(dc, "APUNTES", new Rect(38, 10.5, 38, 8), 6, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(35, 24, w - 42, 30), 11.5, Brush.Parse("#223026"), FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(35, 53, w - 42, 11), 7.8, accent, FontWeight.SemiBold, maxLines: 1);

                            for (int i = 0; i < 4; i++)
                            {
                                double ry = 70 + i * 10.5;
                                dc.DrawEllipse(accent, null, new Point(21, ry - 2), 2, 2);
                                dc.DrawRectangle(Brush.Parse("#6F7A68"), null, new Rect(35, ry - 3, w - 45 - (i % 2) * 12, 2), 1, 1);
                            }

                            var sticky = new Rect(34, 110, w - 42, 46);
                            if (art != null)
                            {
                                dc.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#C8C0A8"), .8), sticky, 2, 2);
                                DrawCoverSlice(dc, art, sticky.Deflate(3), 1.5);
                            }
                            else
                            {
                                dc.DrawRectangle(Brush.Parse("#22000000"), null, sticky.Translate(new Vector(1.5, 2)), 2, 2);
                                dc.DrawRectangle(Brush.Parse("#FFE98A"), new Pen(Brush.Parse("#DFC458"), .8), sticky, 2, 2);
                                dc.DrawEllipse(Brush.Parse("#D9534F"), new Pen(Brushes.White, .6), new Point(sticky.Center.X, sticky.Top + 4), 2.8, 2.8);
                                dc.DrawRectangle(accent, null, new Rect(sticky.Left + 7, sticky.Top + 12, 26, 8), 1.5, 1.5);
                                dc.DrawRectangle(midAccent, null, new Rect(sticky.Left + 7, sticky.Top + 26, 20, 8), 1.5, 1.5);
                                dc.DrawRectangle(midAccent, null, new Rect(sticky.Right - 27, sticky.Top + 26, 20, 8), 1.5, 1.5);
                                dc.DrawLine(new Pen(Brush.Parse("#7A6E3A"), .9), new Point(sticky.Left + 20, sticky.Top + 20), new Point(sticky.Left + 17, sticky.Top + 26));
                                dc.DrawLine(new Pen(Brush.Parse("#7A6E3A"), .9), new Point(sticky.Left + 24, sticky.Top + 20), new Point(sticky.Right - 17, sticky.Top + 26));
                            }
                            break;
                        }

                        case "Artículo":
                        {
                            dc.DrawLine(new Pen(accent, 1.8), new Point(9, 10), new Point(w - 9, 10));
                            dc.DrawLine(new Pen(accent, .7), new Point(9, 13), new Point(w - 9, 13));
                            DrawTextVector(dc, "ARTÍCULO · SEPARATA", new Rect(9, 16, w - 18, 9), 5.8, accent, FontWeight.Bold, TextAlignment.Center, 1);
                            DrawTextVector(dc, entry.Title, new Rect(10, 26, w - 20, 29), 11, darkInk, FontWeight.Bold, TextAlignment.Center, 2, true);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(10, 55, w - 20, 10), 7.5, mutedInk, FontWeight.Normal, TextAlignment.Center, 1, true);

                            var abstractBox = new Rect(11, 67, w - 22, 20);
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#F2EFE6", .85), null, abstractBox);
                            dc.DrawLine(new Pen(accent, 1.2), abstractBox.TopLeft, abstractBox.BottomLeft);
                            dc.DrawLine(new Pen(accent, 1.2), abstractBox.TopRight, abstractBox.BottomRight);
                            dc.DrawRectangle(accent, null, new Rect(16, 71, 20, 2), 1, 1);
                            dc.DrawRectangle(Brush.Parse("#7A7488"), null, new Rect(16, 76, w - 32, 1.8), 1, 1);
                            dc.DrawRectangle(Brush.Parse("#7A7488"), null, new Rect(16, 81, w - 40, 1.8), 1, 1);

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
                            for (int r = 0; r < 9; r++)
                            {
                                double ry = colTop + r * 6.8;
                                if (r >= 5)
                                    dc.DrawRectangle(Brush.Parse("#8E897D"), null, new Rect(10, ry, colMid - 15 - (r % 2) * 6, 1.8), 1, 1);
                                dc.DrawRectangle(Brush.Parse("#8E897D"), null, new Rect(colMid + 4, ry, colMid - 14 - ((r + 1) % 3) * 5, 1.8), 1, 1);
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

                            DrawTextVector(dc, "CARTA", new Rect(13, 13, 52, 9), 6.2, accent, FontWeight.Bold, maxLines: 1);
                            dc.DrawRectangle(Brush.Parse("#B8A58C"), null, new Rect(13, 24, 34, 1.8), 1, 1);
                            dc.DrawRectangle(Brush.Parse("#C8B79E"), null, new Rect(13, 29, 26, 1.8), 1, 1);

                            DrawTextVector(dc, entry.Title, new Rect(13, 41, w - 26, 32), 12, Brush.Parse("#2B211B"), FontWeight.Bold, maxLines: 2, serif: true);
                            for (int l = 0; l < 5; l++)
                                dc.DrawRectangle(Brush.Parse("#B8A68E"), null, new Rect(l == 0 ? 22 : 13, 76 + l * 7.5, w - (l == 0 ? 35 : 26) - (l == 4 ? 18 : 0), 1.6), 1, 1);

                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(13, h - 25, w - 46, 12), 8.2, accent, FontWeight.SemiBold, maxLines: 1, serif: true);
                            else
                                dc.DrawLine(new Pen(accent, 1.2), new Point(13, h - 18), new Point(54, h - 18));

                            var sealCenter = new Point(w - 23, h - 22);
                            dc.DrawEllipse(Brush.Parse("#30000000"), null, sealCenter + new Vector(1, 1.5), 11, 11);
                            dc.DrawEllipse(accent, new Pen(deepAccent, 1), sealCenter, 10.5, 10.5);
                            dc.DrawEllipse(null, new Pen(Brush.Parse("#F2D29B"), .8), sealCenter, 7.2, 7.2);
                            dc.DrawEllipse(Brush.Parse("#F2D29B"), null, sealCenter, 2.2, 2.2);
                            break;
                        }

                        case "Propuesta":
                        {
                            dc.DrawRectangle(accent, null, new Rect(0, 0, 10, h));
                            foreach (var ry in new[] { 22d, h / 2, h - 22d })
                                dc.DrawEllipse(Brushes.Ivory, new Pen(deepAccent, .7), new Point(5, ry), 2, 2);

                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E4E7F4", .78), null, new Rect(10, 0, w - 10, 22));
                            DrawTextVector(dc, "PROPUESTA", new Rect(16, 7, w - 24, 9), 6.2, accent, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(16, 27, w - 24, 30), 11.5, darkInk, FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(16, 56, w - 24, 11), 7.8, accent, FontWeight.SemiBold, maxLines: 1);

                            var gantt = new Rect(16, 72, w - 24, 62);
                            dc.DrawRectangle(Brushes.White, new Pen(Brush.Parse("#C8CEE0"), .8), gantt, 3, 3);
                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, gantt.Deflate(2), 2);
                            }
                            else
                            {
                                for (int c = 1; c <= 3; c++)
                                    dc.DrawLine(new Pen(Brush.Parse("#E8EBF4"), .8), new Point(gantt.Left + c * (gantt.Width / 4), gantt.Top + 4), new Point(gantt.Left + c * (gantt.Width / 4), gantt.Bottom - 4));
                                (double left, double widthBar, string col)[] milestones = [(6, 34, accentHex), (20, 40, "#E06D53"), (38, 36, "#E5A93C"), (54, 32, "#38988E")];
                                for (int m = 0; m < milestones.Length; m++)
                                {
                                    double my = gantt.Top + 8 + m * 13;
                                    dc.DrawRectangle(Brush.Parse(milestones[m].col), null, new Rect(gantt.Left + milestones[m].left, my, milestones[m].widthBar, 7.5), 3.5, 3.5);
                                }
                            }

                            var budgetPill = new Rect(16, h - 24, w - 24, 14);
                            dc.DrawRectangle(accent, null, budgetPill, 3, 3);
                            dc.DrawRectangle(Brushes.Ivory, null, new Rect(budgetPill.Left + 6, budgetPill.Top + 5.5, 36, 3), 1.5, 1.5);
                            dc.DrawRectangle(Brush.Parse("#F5C862"), null, new Rect(budgetPill.Right - 24, budgetPill.Top + 4, 18, 6), 3, 3);
                            break;
                        }

                        case "Acta":
                        {
                            dc.DrawRectangle(null, new Pen(accent, 1.1), new Rect(7, 10, w - 14, h - 17));
                            dc.DrawRectangle(Brush.Parse("#747E78"), new Pen(Brush.Parse("#48504B"), .8), new Rect(w / 2 - 18, 3, 36, 9), 2, 2);
                            dc.DrawRectangle(Brush.Parse("#D8E0DA"), null, new Rect(w / 2 - 10, 5.5, 20, 3.5), 1.5, 1.5);

                            var headBox = new Rect(12, 16, w - 24, 15);
                            dc.DrawRectangle(EditionPalette.Mix(accentHex, "#E8ECE4", .82), new Pen(accent, .8), headBox, 1.5, 1.5);
                            DrawTextVector(dc, "ACTA DE SESIÓN", new Rect(14, 19.5, w - 28, 9), 5.8, accent, FontWeight.Bold, TextAlignment.Center, 1);

                            DrawTextVector(dc, entry.Title, new Rect(13, 36, w - 26, 29), 11.2, darkInk, FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(13, 64, w - 26, 11), 7.6, accent, FontWeight.SemiBold, maxLines: 1);

                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, new Rect(13, 78, w - 26, 48), 2);
                            }
                            else
                            {
                                for (int a = 0; a < 3; a++)
                                {
                                    double ay = 80 + a * 16;
                                    var chk = new Rect(13, ay, 10, 10);
                                    dc.DrawRectangle(a < 2 ? softAccent : Brushes.White, new Pen(accent, 1.1), chk, 1.5, 1.5);
                                    if (a < 2) DrawCheckmark(dc, chk.TopLeft, accent);
                                    dc.DrawRectangle(darkInk, null, new Rect(28, ay + 2, w - 44 - (a % 2) * 12, 2.2), 1, 1);
                                    dc.DrawRectangle(Brush.Parse("#9AA296"), null, new Rect(28, ay + 6.5, w - 54, 1.8), 1, 1);
                                }
                            }

                            dc.DrawLine(new Pen(Brush.Parse("#7C857A"), 1), new Point(13, h - 18), new Point(44, h - 18));
                            dc.DrawLine(new Pen(Brush.Parse("#7C857A"), 1), new Point(w - 44, h - 18), new Point(w - 13, h - 18));
                            var stampCenter = new Point(w / 2, h - 22);
                            dc.DrawEllipse(null, new Pen(accent, 1.2), stampCenter, 11, 11);
                            dc.DrawEllipse(null, new Pen(accent, .6), stampCenter, 8, 8);
                            dc.DrawRectangle(accent, null, new Rect(stampCenter.X - 6, stampCenter.Y - 1.2, 12, 2.4), 1, 1);
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

                            DrawTextVector(dc, "DOCUMENTO", new Rect(11, 7, w - 32, 9), 6.2, Brushes.Ivory, FontWeight.Bold, maxLines: 1);
                            DrawTextVector(dc, entry.Title, new Rect(11, 28, w - 22, 31), 11.8, darkInk, FontWeight.Bold, maxLines: 2);
                            if (entry.Author != "")
                                DrawTextVector(dc, entry.Author, new Rect(11, 58, w - 22, 11), 7.8, accent, FontWeight.SemiBold, maxLines: 1);
                            dc.DrawLine(new Pen(accent, 1), new Point(11, 71), new Point(w - 11, 71));

                            if (art != null)
                            {
                                DrawCoverSlice(dc, art, new Rect(11, 77, w - 22, 48), 2);
                            }
                            else
                            {
                                var quoteBox = new Rect(11, 77, w - 22, 26);
                                dc.DrawRectangle(EditionPalette.Mix(accentHex, "#EAE6DC", .84), null, quoteBox, 2, 2);
                                dc.DrawRectangle(accent, null, new Rect(11, 77, 3, 26), 1, 1);
                                dc.DrawRectangle(darkInk, null, new Rect(19, 83, w - 38, 2.2), 1, 1);
                                dc.DrawRectangle(mutedInk, null, new Rect(19, 89, w - 46, 2), 1, 1);
                                dc.DrawRectangle(mutedInk, null, new Rect(19, 95, w - 54, 2), 1, 1);
                            }

                            for (int n = 0; n < 5; n++)
                                dc.DrawRectangle(Brush.Parse("#A8A398"), null, new Rect(11, 112 + n * 7.5, w - 22 - (n == 4 ? 26 : (n % 2) * 10), 1.8), 1, 1);
                            DrawTextVector(dc, "— 1 —", new Rect(11, h - 13, w - 22, 9), 6.2, mutedInk, FontWeight.SemiBold, TextAlignment.Center, 1);
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



