using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
using Nimbo.Storage;

namespace Nimbo.Desktop;

internal sealed class BookCover : Control
{
    internal static readonly string[] LetteringStyles = ["Automática", "Marfil", "Oro", "Plata", "Cobre", "Carbón", "Relieve", "Tejuelo"];
    readonly Entry entry;
    readonly Action open;
    readonly string media;
    Bitmap? art;
    readonly EditionPalette palette;
    const double Rest = .21;
    double angle = Rest, initialAngle;
    Point? down;
    bool dragged;
    public BookCover(Entry entry, string media, EditionPalette palette, Action open)
    {
        this.entry = entry; this.media = media; this.open = open; this.palette = palette;
        Width = 151; Height = 222; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
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
        Avalonia.Automation.AutomationProperties.SetName(this, entry.Title);
        ToolTip.SetTip(this, entry.Title + " · Clic para abrir. Arrastra o usa las flechas para girar. Fin muestra la contraportada.");
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); down = e.GetPosition(this); initialAngle = angle; dragged = false; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (down is not Point p) return; var dx = e.GetPosition(this).X - p.X;
        if (Math.Abs(dx) > 5 || Math.Abs(e.GetPosition(this).Y - p.Y) > 5) dragged = true;
        if (dragged) { angle = (initialAngle + dx * .018) % (Math.PI * 2); InvalidateVisual(); }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (down == null) return; var click = !dragged && new Rect(Bounds.Size).Contains(e.GetPosition(this)); down = null; e.Pointer.Capture(null);
        e.Handled = true; if (click) open();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { down = null; base.OnPointerCaptureLost(e); }
    internal void ShowJacket(bool back) { angle = Rest + (back ? Math.PI : 0); InvalidateVisual(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: angle -= Math.PI / 8; break;
            case Key.Right: angle += Math.PI / 8; break;
            case Key.Home: ShowJacket(false); e.Handled = true; return;
            case Key.End: ShowJacket(true); e.Handled = true; return;
            case Key.Enter: case Key.Space: open(); e.Handled = true; return;
            default: base.OnKeyDown(e); return;
        }
        InvalidateVisual(); e.Handled = true;
    }
    static LinearGradientBrush Wash(bool vertical, params (double Position, string Ink)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(vertical ? 0 : 1, vertical ? 1 : 0, RelativeUnit.Relative) };
        foreach (var stop in stops) brush.GradientStops.Add(new GradientStop(Color.Parse(stop.Ink), stop.Position));
        return brush;
    }
    internal static readonly IBrush JacketShade = Wash(true, (0, "#10162026"), (.38, "#18162026"), (.66, "#9E171F22"), (1, "#E813191E"));
    internal static readonly IBrush ClothLight = Wash(false, (0, "#50111618"), (.035, "#3EFFEBBB"), (.073, "#6E0C1618"), (.095, "#2EFFF2D3"), (.22, "#08FFFFFF"), (1, "#3C0B1518"));
    static readonly IBrush leaves = Wash(false, (0, "#B7A685"), (.35, "#E9DEC4"), (.8, "#F7EAD6"), (1, "#BCAA8E"));
    static readonly IBrush goldLeaf = Wash(true, (0, "#FFF4C8"), (.34, "#F2CC76"), (.68, "#C79338"), (1, "#F7DD95"));
    static readonly IBrush antiqueGoldLeaf = Wash(true, (0, "#9E7322"), (.36, "#7A5614"), (.70, "#593C0B"), (1, "#8C641B"));
    static readonly IBrush silverLeaf = Wash(true, (0, "#FFFFFF"), (.36, "#DAE3ED"), (.70, "#96A5B5"), (1, "#EFF5FA"));
    static readonly IBrush pewterLeaf = Wash(true, (0, "#556677"), (.36, "#3B4956"), (.70, "#27323C"), (1, "#4A5A6A"));
    static readonly IBrush copperLeaf = Wash(true, (0, "#FFE7D4"), (.36, "#E79E77"), (.70, "#A95937"), (1, "#F4BA99"));
    static readonly IBrush bronzeCopperLeaf = Wash(true, (0, "#9A4C2A"), (.36, "#763519"), (.70, "#54220D"), (1, "#884122"));
    static readonly IBrush bronzeLeaf = Wash(true, (0, "#F7DFA8"), (.35, "#CA984A"), (.70, "#8C601D"), (1, "#E2B872"));
    static readonly IBrush darkBronzeLeaf = Wash(true, (0, "#7E5618"), (.36, "#5E3E0D"), (.70, "#402906"), (1, "#6E4A12"));
    static readonly IBrush charcoalObsidian = Wash(true, (0, "#2D3640"), (.45, "#151A20"), (1, "#0C0F13"));
    static readonly IBrush anthraciteSteel = Wash(true, (0, "#E6ECF2"), (.32, "#A8B4C0"), (.68, "#586470"), (1, "#C8D2DC"));
    FormattedText? blurb;
    Geometry? blurbGeometry;

    internal readonly record struct StampStyle(IBrush Face, IBrush Ornament, IBrush? Recess, IBrush? Glint, IPen? Rim = null, IBrush? Extrude = null, bool Tejuelo = false);

    internal static bool IsLight(Color color)
    {
        static double Linear(byte channel) { var s = channel / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return (.2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B)) > .44;
    }

    internal static void DrawStampedGeometry(DrawingContext dc, Geometry? geo, StampStyle style, bool emboss = true)
    {
        if (geo == null) return;
        if (emboss && style.Extrude != null)
        {
            using (dc.PushTransform(Matrix.CreateTranslation(.95, 1.15)))
                dc.DrawGeometry(style.Extrude, null, geo);
        }
        if (emboss && style.Glint != null)
        {
            using (dc.PushTransform(Matrix.CreateTranslation(-.48, -.52)))
                dc.DrawGeometry(style.Glint, null, geo);
        }
        if (style.Recess != null)
        {
            double d = emboss ? .55 : .4;
            using (dc.PushTransform(Matrix.CreateTranslation(d, d + .1)))
                dc.DrawGeometry(style.Recess, null, geo);
        }
        if (style.Rim != null)
            dc.DrawGeometry(null, style.Rim, geo);
        dc.DrawGeometry(style.Face, null, geo);
    }

    internal static void DrawTejueloReserve(DrawingContext dc, Rect zone, Color clothColor, IBrush ornament, bool translucent)
    {
        if (translucent)
        {
            for (int b = 3; b >= 1; b--)
                dc.DrawRectangle(Brush.Parse("#240E1318"), null, zone.Inflate(b * 1.4), 2.5, 2.5);
        }
        var fill = translucent
            ? Brush.Parse("#8210161D")
            : EditionPalette.Mix(clothColor.ToString(), "#0F1318", .72);
        var outer = new Pen(ornament, .85);
        var inner = new Pen(ornament, .45);
        dc.DrawRectangle(fill, outer, zone, 1.5, 1.5);
        if (zone.Width > 14 && zone.Height > 14)
        {
            var inset = zone.Deflate(2.2);
            dc.DrawRectangle(null, inner, inset, 1, 1);
            foreach (var pt in new[] { inset.TopLeft, inset.TopRight, inset.BottomLeft, inset.BottomRight })
                dc.DrawEllipse(ornament, null, pt, 1.1, 1.1);
        }
    }

    internal static void DrawFrostedPhotoPlate(DrawingContext dc, Rect zone, IBrush ornament)
    {
        for (int b = 4; b >= 1; b--)
            dc.DrawRectangle(Brush.Parse("#1F0E141B"), null, zone.Inflate(b * 1.35), 4 + b * .5, 4 + b * .5);
        dc.DrawRectangle(Brush.Parse("#7410171F"), new Pen(ornament, .55), zone, 3.5, 3.5);
    }

    internal static StampStyle ResolveStamp(string mode, Color color, EditionPalette palette, bool overPhoto)
    {
        var hex = color.ToString();
        bool lightCloth = IsLight(color);
        bool brightSurface = !overPhoto && lightCloth;
        var warmFoil = EditionPalette.Mix(palette.Foil, "#F4D99F", .48);
        if (mode == "Automática")
        {
            var face = brightSurface ? Brush.Parse("#192026") : Brush.Parse("#FAF5EA");
            var orn = brightSurface ? Brush.Parse("#785A28") : warmFoil;
            var recess = brightSurface ? Brush.Parse("#78FFF9EC") : Brush.Parse("#B00D1318");
            return new(face, orn, recess, null);
        }
        return mode switch
        {
            "Oro" => brightSurface
                ? new(antiqueGoldLeaf, Brush.Parse("#86601C"), Brush.Parse("#88FFF9E6"), null)
                : new(goldLeaf, Brush.Parse("#ECCB78"), Brush.Parse("#C8201505"), Brush.Parse("#55FFFBE8")),
            "Plata" => brightSurface
                ? new(pewterLeaf, Brush.Parse("#465666"), Brush.Parse("#90FFFFFF"), null)
                : new(silverLeaf, Brush.Parse("#D0DBE6"), Brush.Parse("#C0121A22"), Brush.Parse("#60FFFFFF")),
            "Cobre" => brightSurface
                ? new(bronzeCopperLeaf, Brush.Parse("#823E20"), Brush.Parse("#85FFF2E6"), null)
                : new(copperLeaf, Brush.Parse("#E29B75"), Brush.Parse("#C8240F07"), Brush.Parse("#52FFF0E4")),
            "Bronce" => brightSurface
                ? new(darkBronzeLeaf, Brush.Parse("#6E4A12"), Brush.Parse("#85FFF6E0"), null)
                : new(bronzeLeaf, Brush.Parse("#D6A75C"), Brush.Parse("#C81E1205"), Brush.Parse("#50FFF5D8")),
            "Carbón" => brightSurface
                ? new(charcoalObsidian, Brush.Parse("#5E4822"), Brush.Parse("#90FFF9EC"), null)
                : new(
                    anthraciteSteel,
                    Brush.Parse("#C4D0DC"),
                    Brush.Parse("#E0070A0E"),
                    Brush.Parse("#70FFFFFF")),
            "Relieve" => brightSurface
                ? new(
                    EditionPalette.Mix(hex, "#181D22", .74),
                    EditionPalette.Mix(hex, "#3E3325", .58),
                    EditionPalette.Mix(hex, "#0B0E12", .85),
                    Brush.Parse("#B0FFFDF6"),
                    null,
                    Brush.Parse("#90FFFFFF"))
                : new(
                    EditionPalette.Mix(hex, "#FAF2E2", overPhoto ? .84 : .76),
                    EditionPalette.Mix(hex, "#EEDBB8", .64),
                    EditionPalette.Mix(hex, "#0A0E12", .82),
                    Brush.Parse("#90FFFFFF"),
                    null,
                    Brush.Parse("#D806090D")),
            "Tejuelo" => new(
                goldLeaf,
                Brush.Parse("#E8C472"),
                Brush.Parse("#D0140D04"),
                Brush.Parse("#60FFFBE8"),
                null,
                null,
                true),
            _ => new(
                Brush.Parse("#FBF6EB"),
                warmFoil,
                Brush.Parse("#B80E1419"),
                null)
        };
    }

    internal static void DrawWords(DrawingContext dc, string text, Rect bounds, double size, StampStyle style, FontWeight weight = FontWeight.Normal, bool singleLine = false)
    {
        if (bounds.Width < 2 || text.Length == 0) return;
        var typeface = new Typeface(new FontFamily("Georgia, Liberation Serif, serif"), FontStyle.Normal, weight);
        FormattedText Build(double s) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, s, Brushes.White)
        {
            MaxTextWidth = bounds.Width,
            MaxTextHeight = bounds.Height,
            MaxLineCount = singleLine ? 1 : 2,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center
        };
        var fmt = Build(size);
        if (singleLine)
        {
            for (double s = size; s >= 8.0 && fmt.WidthIncludingTrailingWhitespace > bounds.Width - 2; s -= .5)
                fmt = Build(s);
        }
        var top = bounds.Y + Math.Max(0, (bounds.Height - fmt.Height) / 2);
        var geo = fmt.BuildGeometry(new Point(bounds.X, top));
        using (dc.PushClip(bounds.Inflate(2)))
        {
            if (geo != null) DrawStampedGeometry(dc, geo, style);
            else dc.DrawText(fmt, new Point(bounds.X, top));
        }
    }

    internal static (FormattedText Text, Geometry? Geometry) FitSynopsis(string description, string title, Rect box)
    {
        var text = string.Join(" ", description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0) text = title;
        FormattedText Set(string value, double size) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Georgia"), size, Brushes.White) { MaxTextWidth = box.Width, TextAlignment = TextAlignment.Left };
        int limit = Math.Min(560, text.Length); string sample = text[..limit];
        const double minimum = 7.5;
        FormattedText? fitted = null;
        for (double size = 10; size >= minimum; size -= .5)
        {
            var paragraph = Set(sample, size);
            if (limit == text.Length && paragraph.Height <= box.Height) { fitted = paragraph; break; }
        }
        if (fitted == null)
        {
            var words = sample.Split(' '); var excerpt = "";
            foreach (var word in words)
            {
                var next = excerpt.Length == 0 ? word : excerpt + " " + word;
                if (Set(next + "…", minimum).Height > box.Height) break;
                excerpt = next;
            }
            fitted = Set(excerpt + "…", minimum);
        }
        return (fitted, fitted.BuildGeometry(box.TopLeft));
    }

    void DrawSynopsis(DrawingContext dc, Rect box, StampStyle style)
    {
        if (blurb == null) (blurb, blurbGeometry) = FitSynopsis(entry.Description, entry.Title, box);
        using (dc.PushClip(box))
        {
            if (blurbGeometry != null) DrawStampedGeometry(dc, blurbGeometry, style, false);
            else dc.DrawText(blurb, box.TopLeft);
        }
    }

    public override void Render(DrawingContext dc)
    {
        var cos = Math.Cos(angle); var sin = Math.Sin(angle);
        var color = Color.TryParse(entry.Color, out var parsed) ? parsed : Color.Parse("#43676B");
        var cloth = new SolidColorBrush(color);
        bool lightCloth = IsLight(color);
        var paper = EditionPalette.Mix(palette.Paper, "#F1DDC4", .62);

        const double w = 131, h = 176, d = 21;
        double depth(double x, double z) => z * cos - x * sin;
        Point Project(double x, double y, double z) => new(Bounds.Width / 2 + x * cos + z * sin, 21 + y + depth(x, z) * .17);
        void Face(double x1, double z1, double x2, double z2, double length, Action draw)
        {
            var a = Project(x1, 0, z1); var b = Project(x2, 0, z2);
            if (b.X - a.X < .015) return;
            using (dc.PushTransform(new Matrix((b.X - a.X) / length, (b.Y - a.Y) / length, 0, 1, a.X, a.Y))) draw();
        }
        for (int i = 0; i < 5; i++) dc.DrawEllipse(Brush.Parse("#14000000"), null, new Point(Bounds.Width / 2 + 4, 201), Math.Abs(cos) * 44 + 16 - i * 2, 7 - i);
        var outline = new List<(double X, double Z)> { (-w / 2, d / 2), (w / 2, d / 2), (w / 2, d / 2 - 2), (w / 2 - 3, d / 2 - 2), (w / 2 - 3, 2 - d / 2), (w / 2, 2 - d / 2), (w / 2, -d / 2), (-w / 2, -d / 2) };
        for (int i = 1; i <= 16; i++) { var t = Math.PI * i / 16; outline.Add((-w / 2 - 4 * Math.Sin(t), -d / 2 * Math.Cos(t))); }
        var cap = new StreamGeometry();
        using (var ctx = cap.Open())
        {
            ctx.BeginFigure(Project(outline[0].X, 0, outline[0].Z), true);
            foreach (var v in outline.Skip(1)) ctx.LineTo(Project(v.X, 0, v.Z)); ctx.EndFigure(true);
        }
        dc.DrawGeometry(cloth, null, cap);
        for (int i = 2; i < d - 2; i++)
        {
            double z = i - d / 2; dc.DrawLine(new Pen(i % 3 == 0 ? Brush.Parse("#BFAC91") : paper, 1.1), Project(-w / 2 + 6, 0, z), Project(w / 2 - 3, 0, z));
        }
        var faces = new List<(double Z, Action Draw)>();
        void Edge(double x1, double z1, double x2, double z2, double width, Action paint)
            => faces.Add(((depth(x1, z1) + depth(x2, z2)) / 2, () => Face(x1, z1, x2, z2, width, paint)));
        void Jacket(bool front)
        {
            bool hasPhoto = front && art != null;
            var style = ResolveStamp(entry.Lettering, color, palette, hasPhoto);
            var ornamentStyle = style with { Face = style.Ornament };
            dc.DrawRectangle(cloth, null, new Rect(0, 0, w, h));
            if (hasPhoto)
            {
                double scale = Math.Max(w / art!.Size.Width, h / art.Size.Height); var size = new Size(w / scale, h / scale);
                dc.DrawImage(art, new Rect((art.Size.Width - size.Width) / 2, (art.Size.Height - size.Height) / 2, size.Width, size.Height), new Rect(0, 0, w, h));
                dc.DrawRectangle(JacketShade, null, new Rect(0, 0, w, h));
            }
            else
            {
                if ((!lightCloth && entry.Lettering != "Relieve") || (lightCloth && entry.Lettering == "Marfil"))
                    dc.DrawRectangle(Wash(true, (0, "#0810161A"), (.48, "#2C10161A"), (1, "#6E0E1418")), null, new Rect(0, 0, w, h));
                if (front)
                {
                    var binding = new Pen(style.Ornament, .75);
                    var stamp = Studio.JacketStamp(entry.Id);
                    var frame = new Rect(38, 14, 64, 64);
                    dc.DrawImage(stamp, new Rect(stamp.Size), frame);
                    dc.DrawRectangle(null, binding, frame.Inflate(2));
                    dc.DrawRectangle(null, binding, new Rect(11, 5, w - 16, h - 10));
                    dc.DrawLine(binding, new Point(16, 10), new Point(29, 10));
                    dc.DrawLine(binding, new Point(w - 9, 10), new Point(w - 22, 10));
                }
            }
            dc.DrawRectangle(ClothLight, null, new Rect(0, 0, w, h));
            var foil = new Pen(style.Ornament, .7);
            if (front)
            {
                if (style.Tejuelo)
                    DrawTejueloReserve(dc, new Rect(13, 86, w - 21, 83), color, style.Ornament, hasPhoto);
                else if (hasPhoto)
                    DrawFrostedPhotoPlate(dc, new Rect(12, 85, w - 19, 84), style.Ornament);
                else
                    dc.DrawLine(foil, new Point(15, 88), new Point(w - 10, 88));
                DrawWords(dc, entry.Type.ToUpperInvariant(), new Rect(15, 91, w - 26, 13), 8, ornamentStyle, FontWeight.SemiBold, true);
                DrawWords(dc, entry.Title, new Rect(15, 106, w - 26, 44), 15.5, style, FontWeight.SemiBold);
                DrawWords(dc, entry.Author, new Rect(15, 152, w - 26, 15), 10, style, FontWeight.Normal, true);
            }
            else
            {
                if (style.Tejuelo)
                {
                    DrawTejueloReserve(dc, new Rect(7, 6, w - 14, h - 12), color, style.Ornament, false);
                }
                else
                {
                    if (!lightCloth || entry.Lettering == "Marfil") dc.DrawRectangle(Brush.Parse("#4410161A"), null, new Rect(0, 0, w, h));
                    dc.DrawRectangle(null, foil, new Rect(7, 6, w - 14, h - 12));
                }
                dc.DrawLine(foil, new Point(44, 21), new Point(w - 44, 21));
                DrawSynopsis(dc, new Rect(16, 29, w - 32, 113), style);
                DrawWords(dc, entry.Author, new Rect(14, 150, w - 28, 15), 10, style, FontWeight.Normal, true);
            }
        }
        Edge(-w / 2, d / 2, w / 2, d / 2, w, () => Jacket(true));
        Edge(w / 2, -d / 2, -w / 2, -d / 2, w, () => Jacket(false));
        for (int i = 1; i < 6; i++)
        {
            var a = outline[i]; var b = outline[i + 1];
            bool pageEdge = i == 3;
            Edge(a.X, a.Z, b.X, b.Z, d, () =>
            {
                dc.DrawRectangle(cloth, null, new Rect(0, 0, d, h));
                if (pageEdge)
                {
                    dc.DrawRectangle(leaves, null, new Rect(0, 2, d, h - 4));
                    for (int n = 2; n < d; n += 2) dc.DrawLine(new Pen(Brush.Parse("#9B8B71"), .45), new Point(n, 2), new Point(n, h - 2));
                }
            });
        }
        var spine = new List<Point>();
        for (int i = 7; i < outline.Count - 1; i++)
        {
            var a = Project(outline[i].X, 0, outline[i].Z); var b = Project(outline[i + 1].X, 0, outline[i + 1].Z);
            if (b.X <= a.X) continue;
            if (spine.Count == 0) spine.Add(a);
            spine.Add(b);
        }
        if (spine.Count > 1)
        {
            var binding = new StreamGeometry();
            using (var path = binding.Open())
            {
                path.BeginFigure(spine[0], true);
                foreach (var p in spine.Skip(1)) path.LineTo(p);
                foreach (var p in spine.AsEnumerable().Reverse()) path.LineTo(new Point(p.X, p.Y + h));
                path.EndFigure(true);
            }
            var shade = Wash(false, (0, "#4E141A21"), (.45, "#16FFFFFF"), (1, "#3E131C24"));
            faces.Add((depth(-w / 2, 0), () => { dc.DrawGeometry(cloth, null, binding); dc.DrawGeometry(shade, null, binding); }));
        }
        foreach (var face in faces.OrderBy(f => f.Z)) face.Draw();
        if (sin > .42) Face(-w / 2 - 4, -d * .37, -w / 2 - 4, d * .37, d * .74, () =>
        {
            var spineWidth = d * .74;
            var style = ResolveStamp(entry.Lettering, color, palette, false);
            var bandPen = new Pen(style.Ornament, .65);
            if (style.Tejuelo)
                DrawTejueloReserve(dc, new Rect(1.1, 16, spineWidth - 2.2, h - 32), color, style.Ornament, false);
            else if (lightCloth && entry.Lettering == "Marfil")
                dc.DrawRectangle(Brush.Parse("#4210161A"), null, new Rect(1.2, 17, spineWidth - 2.4, h - 34));
            foreach (var y in new[] { 14d, 17d, h - 17d, h - 14d })
                dc.DrawLine(bandPen, new Point(1.4, y), new Point(spineWidth - 1.4, y));
            using (dc.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(0, h - 22)))
                DrawWords(dc, entry.Title, new Rect(0, 0, h - 44, spineWidth), 10.5, style, FontWeight.SemiBold, true);
        });
    }
}

