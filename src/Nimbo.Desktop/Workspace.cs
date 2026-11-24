using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    readonly Border masthead = new();
    static IBrush Edge = Brush.Parse("#44515B");
    static IBrush Sheet = Brush.Parse("#24292C");

    void Navigate(string active)
    {

        masthead.IsVisible=true;var dock = new DockPanel();
        var nav = Column(6);
        var mark = Display("N",34);mark.FontStyle=FontStyle.Italic;mark.Foreground=Accent;
        mark.HorizontalAlignment=HorizontalAlignment.Center;mark.Margin=new Thickness(0,0,0,20);
        nav.Children.Add(mark);
        Button Link(string title, Action go)
        {
            var face=Column(5);face.HorizontalAlignment=HorizontalAlignment.Center;
            face.Children.Add(Glyph(title,20));
            var label=Label(title,10);label.HorizontalAlignment=HorizontalAlignment.Center;face.Children.Add(label);
            var button=Action(title,go);button.Content=face;button.Name="Nav"+title;button.HorizontalContentAlignment=HorizontalAlignment.Center;
            button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(1,9);
            button.Foreground=active==title?Accent:Foreground;
            button.Background=active==title?Sheet:Brushes.Transparent;button.BorderThickness=new Thickness(active==title?2:0,0,0,0);
            button.BorderBrush=Accent;button.CornerRadius=new CornerRadius(2);
            Avalonia.Automation.AutomationProperties.SetName(button,title);return button;
        }
        nav.Children.Add(Link("Biblioteca",Library));
        if(current!=null){
            nav.Children.Add(Link("Manuscrito",Editor));nav.Children.Add(Link("Lectura",()=>ReadSection(chapter)));
            if(current.IsNovel)nav.Children.Add(Link("Glosario",()=>Glossary(null)));
            nav.Children.Add(Link("Ficha",Details));
        }
        var prefs=Link("Preferencias",Preferences);DockPanel.SetDock(prefs,Dock.Bottom);dock.Children.Add(prefs);
        dock.Children.Add(new ScrollViewer{Content=nav});
        masthead.Child=dock;masthead.Background=PanelColor;masthead.BorderBrush=Edge;
    }
    static Control Glyph(string name,double size)
    {
        var path=name switch{
            "Biblioteca"=>"M3 4 L6 4 L6 20 L3 20 Z M9 4 H12 V20 H9 Z M14 5 L17 4 L21 19 L18 20 Z",
            "Manuscrito"=>"M4 20 L5 15 L17 3 L21 7 L9 19 Z M6 15 L10 18 M15 5 L19 9",
            "Lectura"=>"M12 6 Q7 3 2 5 L2 19 Q7 17 12 20 Q17 17 22 19 L22 5 Q17 3 12 6 L12 20",
            "Glosario"=>"M3 4 H17 V19 H3 Z M7 4 V19 M17 8 H21 V22 H7 V19 M10 9 H14 M10 12 H14",
            "Ficha"=>"M4 5 H20 V19 H4 Z M8 10 H11 M8 14 H16",
            _=>"M3 6 H12 M16 6 H21 M3 12 H6 M10 12 H21 M3 18 H14 M18 18 H21 M12 3 V9 H16 V3 Z M6 9 V15 H10 V9 Z M14 15 V21 H18 V15 Z"
        };
        var drawing=new Avalonia.Controls.Shapes.Path{Data=StreamGeometry.Parse(path),Stroke=Accent,StrokeThickness=1.5,Width=24,Height=24};
        return new Viewbox{Child=drawing,Width=size,Height=size};
    }
    static readonly Lazy<Bitmap> Observatorio=new(()=>{
        using var art=Avalonia.Platform.AssetLoader.Open(new Uri("avares://Nimbo.Desktop/Art/observatorio.jpg"));
        return new Bitmap(art);
    });
    Image WorkArt(Nimbo.Core.Work work,int width,double height)
    {
        var art=work.CoverImage==""?null:Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(work.CoverImage)),width,height);
        if(art==null)art=new Image{Source=Observatorio.Value,Height=height,Stretch=Stretch.UniformToFill};
        art.HorizontalAlignment=HorizontalAlignment.Stretch;return art;
    }
    bool IsLight => RequestedThemeVariant==Avalonia.Styling.ThemeVariant.Light;
    IBrush Quiet => Brush.Parse(IsLight?"#586260":"#AFB7B0");
    Button QuietAction(string text, Action action)
    {
        var button=Action(text,action);button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Foreground=Quiet;
        button.Padding=new Thickness(9,6);return button;
    }
    static Border Rule(IBrush ink)=>new(){Height=1,Background=ink,Opacity=.5};
    static MenuItem MenuAction(string text,Action action){var item=new MenuItem{Header=text};item.Click+=(_,_)=>action();return item;}

    static Image? Thumbnail(string path,int width,double height)
    {
        try{
            using var source=File.OpenRead(path);
            var bitmap=Bitmap.DecodeToWidth(source,width);
            var image=new Image{Source=bitmap,Height=height,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};
            image.DetachedFromVisualTree+=(_,_)=>{(image.Source as Bitmap)?.Dispose();image.Source=null;};
            image.AttachedToVisualTree+=(_,_)=>{
                if(image.Source!=null)return;
                try{using var file=File.OpenRead(path);image.Source=Bitmap.DecodeToWidth(file,width);}
                catch(Exception ex) when(ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException){}
            };
            return image;
        }catch(Exception ex) when(ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException){return null;}
    }
    static readonly Lazy<CroppedBitmap[]> Emblems=new(()=>{
        using var file=Avalonia.Platform.AssetLoader.Open(new Uri("avares://Nimbo.Desktop/Art/coleccion.jpg"));
        var atlas=new Bitmap(file);int width=atlas.PixelSize.Width/3,height=atlas.PixelSize.Height/2;
        return Enumerable.Range(0,6).Select(i=>new CroppedBitmap(atlas,new PixelRect(i%3*width,i/3*height,width,height))).ToArray();
    });
    static Border ClassEmblem(string kind)
    {
        int index=kind switch{"Personaje"=>0,"Lugar"=>1,"Mundo"=>2,"Raza"=>3,"Organización"=>4,_=>5};
        return new Border{Height=168,ClipToBounds=true,CornerRadius=new CornerRadius(3),
            Child=new Image{Source=Emblems.Value[index],Stretch=Stretch.UniformToFill}};
    }
    internal static CroppedBitmap JacketStamp(string id){
        uint hash=0;
        foreach(char letter in id)hash=unchecked(hash*31+letter);
        return Emblems.Value[hash%(uint)Emblems.Value.Length];
    }
    void Emphasize(Button button)
    {
        button.Background=Accent;button.Foreground=ActualThemeVariant==Avalonia.Styling.ThemeVariant.Light?Brushes.White:Brush.Parse("#122321");
        button.FontWeight=FontWeight.SemiBold;
    }
    static Control SheetOf(Control content,double width)
        => new Border {Child=content, MaxWidth=width,Padding=new Thickness(30,22),Background=Sheet,BorderBrush=Edge,BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(3)};
    static TextBlock Kicker(string text)
        => new(){Text=text.ToUpperInvariant(),FontSize=11,LetterSpacing=1.5,Foreground=Accent,FontWeight=FontWeight.SemiBold};
}
