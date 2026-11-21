using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Nimbo.Storage;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    sealed class TurningSheet(Control paper,Action open):Button
    {
        Point? origin;
        double angle,start;
        bool moved;
        void Turn(double value){
            angle=value%(Math.PI*2);
            var scale=Math.Max(.018,Math.Abs(Math.Cos(angle)));
            paper.RenderTransform=new MatrixTransform(new Matrix(scale,.08*Math.Sin(angle),0,1,0,0));
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
        var sheets=new Grid{Width=width+5,Height=height+5};
        for(int offset=4;offset>=0;offset-=2){
            var face=new Border{Width=width,Height=height,HorizontalAlignment=HorizontalAlignment.Left,
                VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(offset,offset,0,0),
                Background=offset==0?paper:Brush.Parse(slide?"#516472":"#CCC9BF"),BorderThickness=new Thickness(.8),BorderBrush=Brush.Parse("#9B998D")};
            sheets.Children.Add(face);
            if(offset!=0)continue;
            var design=new Grid{ClipToBounds=true};
            if(entry.Cover!="" && Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(entry.Cover)),360,height-2) is {} image){
                image.HorizontalAlignment=HorizontalAlignment.Stretch;image.Stretch=Stretch.Uniform;design.Children.Add(image);
            }else{
                var line=new Border{Height=slide?5:2,VerticalAlignment=slide?VerticalAlignment.Bottom:VerticalAlignment.Top,
                    Background=edge,Margin=slide?new Thickness(0):new Thickness(12,15,12,0)};
                design.Children.Add(line);
                design.Children.Add(new TextBlock{Text=entry.Title,FontSize=slide?16:15,FontWeight=FontWeight.SemiBold,
                    Foreground=slide?Brushes.Ivory:Brush.Parse("#26343C"),TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
                    MaxLines=slide?3:5,Margin=new Thickness(12,slide?8:26,12,9),VerticalAlignment=slide?VerticalAlignment.Center:VerticalAlignment.Top});
            }
            face.Child=design;
        }
        var canvas=new Grid{Width=155,Height=210};sheets.VerticalAlignment=VerticalAlignment.Bottom;sheets.Margin=new Thickness(0,0,0,15);canvas.Children.Add(sheets);
        var button=new TurningSheet(sheets,open){Content=canvas,Name=slide?"MiniDiapositiva":"MiniDocumento",Padding=new Thickness(0),Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center};
        Avalonia.Automation.AutomationProperties.SetName(button,entry.Title);ToolTip.SetTip(button,entry.Title);
        return button;
    }
}
