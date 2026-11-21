using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Nimbo.Desktop;

// Each wrapping row rests on one continuous shelf; the shelves follow the window width.
internal sealed class Bookcase : Control
{
    public const int Slot=114,Pitch=190;
    readonly EditionPalette palette;
    public Bookcase(EditionPalette palette){this.palette=palette;IsHitTestVisible=false;}
    public override void Render(DrawingContext dc){
        base.Render(dc);
        var width=Bounds.Width;
        for(int top=0;top<Bounds.Height-1;top+=Pitch){
            var back=EditionPalette.Mix(palette.Wood,palette.Ink,.72);
            dc.DrawRectangle(back,null,new Rect(0,top,width,Pitch));
            for(int i=0;i<6;i++)dc.DrawRectangle(Brush.Parse("#0C000000"),null,new Rect(0,top,width,3+i*3));
            var grain=new Pen(EditionPalette.Mix(palette.Wood,palette.Foil,.28),.8);
            dc.DrawRectangle(Brush.Parse(palette.Wood),null,new Rect(0,top+150,width,37));
            dc.DrawLine(new Pen(Brush.Parse(palette.Foil),1.2),new Point(0,top+151),new Point(width,top+151));
            foreach(int y in new[]{155,181,185})dc.DrawLine(grain,new Point(0,top+y),new Point(width,top+y));
            for(int x=0;x<width;x+=210){
                var grainLine=new StreamGeometry();
                using(var line=grainLine.Open()){
                    line.BeginFigure(new Point(x,top+180),false);
                    line.CubicBezierTo(new Point(x+38,top+173),new Point(x+134,top+184),new Point(x+210,top+180));line.EndFigure(false);
                }
                dc.DrawGeometry(null,grain,grainLine);
            }
            dc.DrawRectangle(Brush.Parse("#52000000"),null,new Rect(0,top+187,width,3));
        }
    }
}
