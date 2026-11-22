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
    readonly EditionPalette palette;
    const double Rest = .21;
    double angle = Rest, initialAngle;
    Point? down;
    bool dragged;
    public BookCover(Entry entry, string media, EditionPalette palette, Action open)
    {
        this.entry = entry; this.media = media; this.open = open;this.palette=palette;
        Width = 151; Height = 222; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
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
        ToolTip.SetTip(this,entry.Title+" · Clic para abrir. Arrastra o usa las flechas para girar. Fin muestra la contraportada.");
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); down = e.GetPosition(this); initialAngle = angle; dragged = false; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (down is not Point p) return; var dx = e.GetPosition(this).X - p.X;
        if (Math.Abs(dx)>5 || Math.Abs(e.GetPosition(this).Y-p.Y)>5) dragged = true;
        if (dragged) { angle = (initialAngle + dx * .018) % (Math.PI * 2); InvalidateVisual(); }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (down == null) return; var click = !dragged && new Rect(Bounds.Size).Contains(e.GetPosition(this)); down = null; e.Pointer.Capture(null);
        e.Handled = true; if (click) open();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { down = null; base.OnPointerCaptureLost(e); }
    internal void ShowJacket(bool back){angle=Rest+(back?Math.PI:0);InvalidateVisual();}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch(e.Key){
            case Key.Left: angle-=Math.PI/8;break;
            case Key.Right: angle+=Math.PI/8;break;
            case Key.Home: ShowJacket(false);e.Handled=true;return;
            case Key.End: ShowJacket(true);e.Handled=true;return;
            case Key.Enter: case Key.Space:open();e.Handled=true;return;
            default:base.OnKeyDown(e);return;
        }
        InvalidateVisual();e.Handled=true;
    }
    static LinearGradientBrush Wash(bool vertical,params (double Position,string Ink)[] stops)
    {
        var brush=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(vertical?0:1,vertical?1:0,RelativeUnit.Relative)};
        foreach(var stop in stops)brush.GradientStops.Add(new GradientStop(Color.Parse(stop.Ink),stop.Position));
        return brush;
    }
    static readonly IBrush jacketShade=Wash(true,(0,"#00162026"),(.4,"#00162026"),(.68,"#B01B2325"),(1,"#F5151B20"));
    static readonly IBrush clothLight=Wash(false,(0,"#55111618"),(.035,"#44FFEBBB"),(.073,"#780C1618"),(.095,"#33FFF2D3"),(.22,"#08FFFFFF"),(1,"#420B1518"));
    static readonly IBrush leaves=Wash(false,(0,"#B7A685"),(.35,"#E9DEC4"),(.8,"#F7EAD6"),(1,"#BCAA8E"));
    FormattedText? blurb;

    void DrawSynopsis(DrawingContext dc,Rect box,IBrush ink,IBrush? label)
    {
        if(blurb==null){
            var text=string.Join(" ",entry.Description.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
            if(text.Length==0)text=entry.Title;
            FormattedText Set(string value,double size)=>new(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Georgia"),size,ink){MaxTextWidth=box.Width,TextAlignment=TextAlignment.Left};
            // Measure the untrimmed paragraph; reduce within book proportions, then end on a word.
            int limit=Math.Min(560,text.Length);string sample=text[..limit];
            const double minimum=8;
            for(double size=10;size>=minimum;size-=.5){
                var paragraph=Set(sample,size);
                if(limit==text.Length && paragraph.Height<=box.Height){blurb=paragraph;break;}
            }
            if(blurb==null){
                var words=sample.Split(' ');var excerpt="";
                foreach(var word in words){
                    var next=excerpt.Length==0?word:excerpt+" "+word;
                    if(Set(next+"…",minimum).Height>box.Height)break;
                    excerpt=next;
                }
                blurb=Set(excerpt+"…",minimum);
            }
        }
        using(dc.PushClip(box)){
            if(label!=null)dc.DrawRectangle(label,null,new Rect(box.X,box.Y,box.Width,Math.Min(box.Height,blurb.Height)).Inflate(2));
            dc.DrawText(blurb,box.TopLeft);
        }
    }
    public override void Render(DrawingContext dc)
    {
        var cos = Math.Cos(angle); var sin = Math.Sin(angle);
        var color = Color.TryParse(entry.Color, out var parsed) ? parsed : Color.Parse("#43676B");
        var cloth = new SolidColorBrush(color); IBrush ink = entry.Lettering switch {"Carbón"=>Brush.Parse("#15191D"),"Oro"=>Brush.Parse("#FFE4A0"),"Marfil"=>Brushes.Ivory,_=>Brushes.WhiteSmoke};
        bool charcoal=entry.Lettering=="Carbón";
        // A compact label in the binding hue supports solid dark ink on dark cloth or a photograph.
        static double Linear(byte channel){var s=channel/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
        var luminance=.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
        var label=EditionPalette.Mix(color.ToString(),"#ECCB9F",.62);
        bool needsLabel=charcoal && luminance<.31;
        void Words(string text, Rect bounds, double size, IBrush brush,bool photograph=false)
        {
            if (bounds.Width < 2 || text.Length == 0) return;
            var fmt = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Georgia"), size, brush)
            {
                MaxTextWidth = bounds.Width,
                MaxTextHeight = bounds.Height,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            using (dc.PushClip(bounds)){
                if(brush==ink && (needsLabel || (charcoal && photograph))){
                    double width=Math.Min(bounds.Width,fmt.Width+6);
                    dc.DrawRectangle(label,null,new Rect(bounds.Center.X-width/2,bounds.Y,width,Math.Min(bounds.Height,fmt.Height+1)),1,1);
                }
                dc.DrawText(fmt,bounds.TopLeft);
            }
        }
        const double w=131,h=176,d=21;
        double depth(double x,double z)=>z*cos-x*sin;
        Point Project(double x,double y,double z)=>new(Bounds.Width/2+x*cos+z*sin,21+y+depth(x,z)*.17);
        void Face(double x1,double z1,double x2,double z2,double length,Action draw)
        {
            var a=Project(x1,0,z1);var b=Project(x2,0,z2);
            if(b.X-a.X<.015)return;
            using(dc.PushTransform(new Matrix((b.X-a.X)/length,(b.Y-a.Y)/length,0,1,a.X,a.Y)))draw();
        }
        var gold=EditionPalette.Mix(palette.Foil,"#F4D99F",.48);var paper=EditionPalette.Mix(palette.Paper,"#F1DDC4",.62);
        for(int i=0;i<5;i++)dc.DrawEllipse(Brush.Parse("#14000000"),null,new Point(Bounds.Width/2+4,201),Math.Abs(cos)*44+16-i*2,7-i);
        // Top outline follows the rounded binding and the recessed fore edge.
        var outline=new List<(double X,double Z)>{(-w/2,d/2),(w/2,d/2),(w/2,d/2-2),(w/2-3,d/2-2),(w/2-3,2-d/2),(w/2,2-d/2),(w/2,-d/2),(-w/2,-d/2)};
        for(int i=1;i<=16;i++){var t=Math.PI*i/16;outline.Add((-w/2-4*Math.Sin(t),-d/2*Math.Cos(t)));}
        var cap=new StreamGeometry();
        using(var ctx=cap.Open()){
            ctx.BeginFigure(Project(outline[0].X,0,outline[0].Z),true);
            foreach(var v in outline.Skip(1))ctx.LineTo(Project(v.X,0,v.Z));ctx.EndFigure(true);
        }
        dc.DrawGeometry(cloth,null,cap);
        for(int i=2;i<d-2;i++){
            double z=i-d/2;dc.DrawLine(new Pen(i%3==0?Brush.Parse("#BFAC91"):paper,1.1),Project(-w/2+6,0,z),Project(w/2-3,0,z));
        }
        var faces=new List<(double Z,Action Draw)>();
        void Edge(double x1,double z1,double x2,double z2,double width,Action paint)
            =>faces.Add(((depth(x1,z1)+depth(x2,z2))/2,()=>Face(x1,z1,x2,z2,width,paint)));
        void Jacket(bool front){
            dc.DrawRectangle(cloth,null,new Rect(0,0,w,h));
            if(front && art!=null){
                double scale=Math.Max(w/art.Size.Width,h/art.Size.Height);var size=new Size(w/scale,h/scale);
                dc.DrawImage(art,new Rect((art.Size.Width-size.Width)/2,(art.Size.Height-size.Height)/2,size.Width,size.Height),new Rect(0,0,w,h));
                if(entry.Lettering!="Carbón")dc.DrawRectangle(jacketShade,null,new Rect(0,0,w,h));
            }else{
                if(entry.Lettering!="Carbón")dc.DrawRectangle(jacketShade,null,new Rect(0,0,w,h));
                if(front){
                    var binding=new Pen(gold,.7);
                    var stamp=Studio.JacketStamp(entry.Id);
                    var frame=new Rect(38,14,64,64);
                    dc.DrawImage(stamp,new Rect(stamp.Size),frame);
                    dc.DrawRectangle(null,binding,frame.Inflate(2));
                    dc.DrawRectangle(null,binding,new Rect(11,5,w-16,h-10));
                    dc.DrawLine(binding,new Point(16,10),new Point(29,10));
                    dc.DrawLine(binding,new Point(w-9,10),new Point(w-22,10));
                }
            }
            dc.DrawRectangle(clothLight,null,new Rect(0,0,w,h));
            var foil=new Pen(gold,.65);
            if(front){
                dc.DrawLine(foil,new Point(15,88),new Point(w-10,88));
                Words(entry.Type.ToUpperInvariant(),new Rect(15,93,w-26,13),8,gold);
                Words(entry.Title,new Rect(13,110,w-22,41),16,ink,art!=null);
                Words(entry.Author,new Rect(13,154,w-22,15),10,ink,front && art!=null);
            }else{
                if(entry.Lettering!="Carbón")dc.DrawRectangle(Brush.Parse("#7F12181B"),null,new Rect(0,0,w,h));
                dc.DrawRectangle(null,foil,new Rect(7,6,w-14,h-12));
                dc.DrawLine(foil,new Point(46,21),new Point(w-46,21));
                DrawSynopsis(dc,new Rect(16,30,w-32,111),ink,needsLabel?label:null);
                Words(entry.Author,new Rect(11,152,w-22,15),10,ink,front && art!=null);
            }
        }
        Edge(-w/2,d/2,w/2,d/2,w,()=>Jacket(true));
        Edge(w/2,-d/2,-w/2,-d/2,w,()=>Jacket(false));
        for(int i=1;i<6;i++){
            var a=outline[i];var b=outline[i+1];
            bool pageEdge=i==3;
            Edge(a.X,a.Z,b.X,b.Z,d,()=>{
                dc.DrawRectangle(cloth,null,new Rect(0,0,d,h));
                if(pageEdge){
                    dc.DrawRectangle(leaves,null,new Rect(0,2,d,h-4));
                    for(int n=2;n<d;n+=2)dc.DrawLine(new Pen(Brush.Parse("#9B8B71"),.45),new Point(n,2),new Point(n,h-2));
                }
            });
        }
        var spine=new List<Point>();
        for(int i=7;i<outline.Count-1;i++){
            var a=Project(outline[i].X,0,outline[i].Z);var b=Project(outline[i+1].X,0,outline[i+1].Z);
            if(b.X<=a.X)continue;
            if(spine.Count==0)spine.Add(a);
            spine.Add(b);
        }
        if(spine.Count>1){
            var binding=new StreamGeometry();
            using(var path=binding.Open()){
                path.BeginFigure(spine[0],true);
                foreach(var p in spine.Skip(1))path.LineTo(p);
                foreach(var p in spine.AsEnumerable().Reverse())path.LineTo(new Point(p.X,p.Y+h));
                path.EndFigure(true);
            }
            var shade=Wash(false,(0,"#55141A21"),(.45,"#12FFFFFF"),(1,"#44131C24"));
            faces.Add((depth(-w/2,0),()=>{dc.DrawGeometry(cloth,null,binding);dc.DrawGeometry(shade,null,binding);}));
        }
        foreach(var face in faces.OrderBy(f=>f.Z))face.Draw();
        if(sin>.42)Face(-w/2-4,-d*.37,-w/2-4,d*.37,d*.74,()=>{
            if(entry.Lettering!="Carbón")dc.DrawRectangle(EditionPalette.Mix(color.ToString(),"#10141A",.74),null,new Rect(0,15,d*.74,h-30));
            using(dc.PushTransform(Matrix.CreateRotation(-Math.PI/2)*Matrix.CreateTranslation(1,h-18)))
                Words(entry.Title,new Rect(0,0,h-36,d*.72),11,ink);
        });
    }
}
