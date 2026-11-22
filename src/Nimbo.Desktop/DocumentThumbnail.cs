using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Nimbo.Storage;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    sealed class TurningSheet : Button
    {
        readonly Control front,back,edge;
        readonly Action open;
        readonly double width,thickness;
        Point? origin;
        double angle,start;
        bool moved;

        public TurningSheet(Control front,Control back,Control edge,double width,double thickness,Action open)
        {
            this.front=front;this.back=back;this.edge=edge;
            this.width=width;this.thickness=thickness;this.open=open;
            Turn(0);
        }

        internal void Turn(double value)
        {
            angle=value%Math.Tau;
            var c=Math.Cos(angle);var s=Math.Sin(angle);
            bool facingFront=c>=0;
            front.IsVisible=facingFront;back.IsVisible=!facingFront;
            var face=facingFront?front:back;
            double side=facingFront?1:-1;
            // Project the two sheets and their shared cut edge around a fixed vertical axis.
            face.RenderTransform=new MatrixTransform(new Matrix(Math.Abs(c),-.12*s*side,0,1,side*thickness*s/2,.12*side*thickness*c/2));
            var rimSide=s>=0?-1:1;
            edge.RenderTransform=new MatrixTransform(new Matrix(Math.Abs(s),.12*c*(-rimSide),0,1,rimSide*width*c/2,-.12*rimSide*width*s/2));
        }
        protected override void OnClick()=>open();
        protected override void OnPointerPressed(PointerPressedEventArgs e){
            if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){base.OnPointerPressed(e);return;}
            Focus();origin=e.GetPosition(this);start=angle;moved=false;e.Pointer.Capture(this);e.Handled=true;
        }
        protected override void OnPointerMoved(PointerEventArgs e){
            if(origin is not Point p)return;
            var delta=e.GetPosition(this)-p;if(Math.Abs(delta.X)>5||Math.Abs(delta.Y)>5)moved=true;
            if(moved)Turn(start+.018*delta.X);
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e){
            if(origin==null){base.OnPointerReleased(e);return;}
            bool click=!moved&&new Rect(Bounds.Size).Contains(e.GetPosition(this));origin=null;e.Pointer.Capture(null);e.Handled=true;if(click)OnClick();
        }
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e){origin=null;base.OnPointerCaptureLost(e);}
        protected override void OnKeyDown(KeyEventArgs e){
            if(e.Key is Key.Home or Key.End){Turn(e.Key==Key.Home?0:Math.PI);e.Handled=true;return;}
            if(e.Key is Key.Left or Key.Right){Turn(angle+(e.Key==Key.Left?-1:1)*Math.PI/8);e.Handled=true;}else base.OnKeyDown(e);
        }
    }
    Control Miniature(Entry entry,Action open)
    {
        if(entry.Type is "Novela" or "Historia")return new BookCover(entry,shelf.Media,palette,open);
        bool slide=entry.Type=="Presentación";
        var edge=Color.TryParse(entry.Color,out var color)?new SolidColorBrush(color):Brush.Parse(palette.Binding);
        double width=slide?145:113,height=slide?82:155;
        var paper=Brush.Parse(slide?"#192C39":"#F7F3EB");
        Border Face(bool reverse)
        {
            var design=new Grid{ClipToBounds=true};
            var face=new Border{Width=width,Height=height,Child=design,Background=paper,
                BorderThickness=new Thickness(.8),BorderBrush=Brush.Parse(slide?"#506776":"#B5B1A6")};
            if(!reverse && entry.Cover!="" && Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(entry.Cover)),360,height-2) is {} image){
                image.HorizontalAlignment=HorizontalAlignment.Stretch;image.Stretch=Stretch.Uniform;design.Children.Add(image);
            }else{
                design.Children.Add(new Border{Height=slide?5:2,VerticalAlignment=slide?VerticalAlignment.Bottom:VerticalAlignment.Top,
                    Background=edge,Margin=slide?new Thickness(0):new Thickness(12,15,12,0)});
                design.Children.Add(new TextBlock{Text=entry.Title,FontSize=slide?15:13,FontWeight=FontWeight.SemiBold,
                    Foreground=slide?Brushes.Ivory:Brush.Parse("#26343C"),TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
                    MaxLines=slide?3:4,Margin=new Thickness(12,slide?6:23,12,10),VerticalAlignment=slide?VerticalAlignment.Center:VerticalAlignment.Top});
                if(!slide)
                    for(int n=0;n<(reverse?6:4);n++)
                        design.Children.Add(new Border{Height=1,Background=Brush.Parse("#C9C6BD"),VerticalAlignment=VerticalAlignment.Bottom,
                            Margin=new Thickness(12,0,n%3==0?33:13,17+n*7)});
            }
            return face;
        }
        var front=Face(false);var back=Face(true);double thickness=slide?4:5;
        var rim=new Border{Width=thickness,Height=height,Background=Brush.Parse(slide?"#627987":"#C8C3B6")};
        var stack=new Grid{Width=155,Height=height+24,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,4)};
        stack.Children.Add(rim);stack.Children.Add(back);stack.Children.Add(front);
        var canvas=new Grid{Width=155,Height=210,Background=Brushes.Transparent};canvas.Children.Add(stack);
        var button=new TurningSheet(front,back,rim,width,thickness,open){Content=canvas,Name=slide?"MiniDiapositiva":"MiniDocumento",Padding=new Thickness(0),Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,Cursor=new Cursor(StandardCursorType.Hand)};
        Avalonia.Automation.AutomationProperties.SetName(button,entry.Title);ToolTip.SetTip(button,entry.Title+" · Arrastra para girar o usa las flechas. Inicio y Fin muestran cada cara.");
        return button;
    }
}
