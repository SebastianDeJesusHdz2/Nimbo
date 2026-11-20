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
    readonly Entry entry;
    readonly Action open;
    readonly string media;
    Bitmap? art;
    double angle = -.16, initialAngle;
    Point? down;
    bool dragged;
    public BookCover(Entry entry, string media, Action open)
    {
        this.entry = entry; this.media = media; this.open = open;
        Width = 162; Height = 198; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        AttachedToVisualTree += (_, _) =>
        {
            if (entry.Cover.Length == 0) return;
            try { using var f = File.OpenRead(Path.Combine(media, Path.GetFileName(entry.Cover))); art = Bitmap.DecodeToWidth(f, 260); }
            catch (IOException) { }
            catch (ArgumentException) { }
            InvalidateVisual();
        };
        DetachedFromVisualTree += (_, _) => { art?.Dispose(); art = null; };
        Avalonia.Automation.AutomationProperties.SetName(this, entry.Title);
        ToolTip.SetTip(this, entry.Title + " · Clic para abrir, arrastra para girar");
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); down = e.GetPosition(this); initialAngle = angle; dragged = false; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (down is not Point p) return; var dx = e.GetPosition(this).X - p.X;
        if (Math.Abs(dx) > 4) dragged = true;
        if (dragged) { angle = (initialAngle + dx * .025) % (Math.PI * 2); InvalidateVisual(); }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (down == null) return; var click = !dragged && new Rect(Bounds.Size).Contains(e.GetPosition(this)); down = null; e.Pointer.Capture(null);
        e.Handled = true; if (click) open();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { down = null; base.OnPointerCaptureLost(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.Key is Key.Enter or Key.Space) { open(); e.Handled = true; } else base.OnKeyDown(e); }
    public override void Render(DrawingContext dc)
    {
        var cos = Math.Cos(angle); var sin = Math.Sin(angle);
        var width = Math.Abs(cos) * 113; var depth = Math.Abs(sin) * 20;
        var x = (Bounds.Width - width - depth) / 2; var y = 11d; const double h = 160;
        var front = new Rect(x + (sin < 0 ? depth : 0), y, width, h);
        var side = new Rect(x + (sin < 0 ? 0 : width), y, depth, h);
        var color = Color.TryParse(entry.Color, out var parsed) ? parsed : Color.Parse("#43676B");
        var cloth = new SolidColorBrush(color); var ink = Brushes.WhiteSmoke;
        void Words(string text, Rect bounds, double size, IBrush brush)
        {
            if (bounds.Width < 2 || text.Length == 0) return;
            var fmt = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Georgia"), size, brush)
            {
                MaxTextWidth = bounds.Width,
                MaxTextHeight = bounds.Height,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            using (dc.PushClip(bounds)) dc.DrawText(fmt, bounds.TopLeft);
        }
        dc.DrawRectangle(Brush.Parse("#80674C"), null, new Rect(0, 172, Bounds.Width, 5));
        if (depth > 0)
        {
            dc.DrawRectangle(sin < 0 ? cloth : Brushes.Bisque, null, side);
            if (sin < 0 && depth > 6)
            {
                using (dc.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(side.X, side.Bottom)))
                    Words(entry.Title, new Rect(6, 1, h - 12, depth), Math.Min(12, depth * .75), ink);
            }
        }
        if (width < 1) return;
        using (dc.PushTransform(Matrix.CreateScale(width / 113, 1) * Matrix.CreateTranslation(front.X, y)))
        {
            dc.DrawRectangle(cloth, null, new Rect(0, 0, 113, h));
            if (cos > 0 && art != null) dc.DrawImage(art, new Rect(art.Size), new Rect(0, 0, 113, h));
            if (cos > 0) { dc.DrawRectangle(Brush.Parse("#D5232930"), null, new Rect(0, 92, 113, 68)); Words(entry.Title, new Rect(5, 99, 103, 39), 14, ink); Words(entry.Author, new Rect(5, 139, 103, 16), 10, ink); }
            else { Words(entry.Description, new Rect(9, 26, 95, 106), 11, ink); Words(entry.Type, new Rect(6, 139, 101, 18), 10, ink); }
        }
    }
}
