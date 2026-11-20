using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Nimbo.Core;
using Nimbo.Storage;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    static TextBlock Display(string text,double size) => new(){
        Text=text,FontSize=size,FontFamily=new FontFamily("Georgia, Liberation Serif, serif"),TextWrapping=TextWrapping.Wrap
    };
    void Library()
    {
        if(!TrySave())return;
        session.Close();chapter=null;
        var entries=shelf.List();
        var layout=new DockPanel();
        var rail=Column(16);rail.Width=166;
        rail.Children.Add(Display("Nimbo",34));
        var controls=Column(7);
        rail.Children.Add(controls);
        var footer=Column(8);
        footer.Children.Add(Action("Importar biblioteca…",async()=>await Guard(async()=>{
            var folder=(await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta de LumenWriter"})).FirstOrDefault()?.TryGetLocalPath();
            if(folder==null)return;
            var count=await Task.Run(()=>Legacy.Import(shelf,folder));Library();status.Text=$"{count} obras importadas.";
        })));
        footer.Children.Add(Action("Preferencias",Preferences));
        var sidebar=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);sidebar.Children.Add(footer);
        sidebar.Children.Add(new ScrollViewer{Content=rail});
        var railCard=Card(sidebar);railCard.Margin=new Thickness(0,0,24,0);DockPanel.SetDock(railCard,Dock.Left);layout.Children.Add(railCard);

        var content=Column(18);
        var heading=Display("Biblioteca",44);
        content.Children.Add(heading);
        var create=Column(10);
        var title=new TextBox{Watermark="Título de tu obra"};
        var format=new ComboBox{ItemsSource=Format.All,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        var createButton=Action("Crear obra",()=>Run(()=>{
            if(string.IsNullOrWhiteSpace(title.Text)){title.Focus();return;}
            var work=((Format)format.SelectedItem!).Create(title.Text);
            if(File.Exists(Config("autor")))work.Author=File.ReadAllText(Config("autor"));
            Open(work,shelf.Save(work,0));
        }));
        createButton.Background=Accent;createButton.Foreground=Brushes.Black;
        var createFields=new Grid{ColumnDefinitions=new ColumnDefinitions("1.2*,2*,Auto"),ColumnSpacing=8};
        createFields.Children.Add(format);Grid.SetColumn(title,1);createFields.Children.Add(title);Grid.SetColumn(createButton,2);createFields.Children.Add(createButton);
        create.Children.Add(Display("Nueva obra",24));create.Children.Add(createFields);
        if(entries.Count==0){
            var gallery=new WrapPanel();
            foreach(var (kind,color) in new[]{("Novela","#435D61"),("Apuntes","#686342"),("CV","#664D61"),("Presentación","#435877")}){
                var tile=new StackPanel{Spacing=8};var miniature=new Canvas{Width=102,Height=64};
                void Shape(double x,double y,double w,double h,string ink){
                    var block=new Border{Width=w,Height=h,Background=Brush.Parse(ink),CornerRadius=new CornerRadius(1)};
                    Canvas.SetLeft(block,x);Canvas.SetTop(block,y);miniature.Children.Add(block);
                }
                Shape(0,0,102,64,color);
                if(kind=="Novela"){Shape(29,7,44,51,"#EFE6D5");Shape(32,7,2,51,"#C1A97E");Shape(40,26,24,2,color);Shape(44,31,16,1,color);}
                else if(kind=="Apuntes"){Shape(20,7,62,51,"#EEE8CE");Shape(35,12,1,38,color);for(int y=19;y<48;y+=7)Shape(25,y,50,1,"#AEA887");}
                else if(kind=="CV"){Shape(27,5,48,54,"#F0E6E8");Shape(27,5,14,54,"#BFAAB7");for(int y=16;y<49;y+=8)Shape(46,y,22,2,color);}
                else{Shape(9,11,84,42,"#E7EBF3");Shape(15,17,34,3,color);Shape(15,27,30,2,"#A9B7BF");Shape(53,19,32,25,"#92A8A3");}
                tile.Children.Add(miniature);tile.Children.Add(Label(kind));
                var preset=new Button{Content=tile,Margin=new Thickness(0,0,8,6),Padding=new Thickness(8),CornerRadius=new CornerRadius(6)};
                preset.Click+=(_,_)=>{format.SelectedItem=Format.All.Single(f=>f.Name==kind);title.Focus();};gallery.Children.Add(preset);
            }
            create.Children.Insert(1,gallery);
        }
        content.Children.Add(Card(create));
        if(entries.Count>0){
            var latest=entries[0];var resume=new DockPanel();
            var button=Action("Continuar →",()=>Run(()=>{var item=shelf.Open(latest.Id);Open(item.Work,item.Revision);}));
            DockPanel.SetDock(button,Dock.Right);resume.Children.Add(button);
            var info=Column(3);info.Children.Add(Label("Última obra guardada",13));info.Children.Add(Display(latest.Title,25));resume.Children.Add(info);content.Children.Add(Card(resume));
        }
        var search=new TextBox{Watermark="Buscar por título o autor",Width=225,HorizontalAlignment=HorizontalAlignment.Right};
        var section=new DockPanel();DockPanel.SetDock(search,Dock.Right);section.Children.Add(search);
        var groupTitle=Display("Tu estantería",26);section.Children.Add(groupTitle);content.Children.Add(section);
        var books=new WrapPanel();content.Children.Add(books);
        string selected="";
        void Filter(){
            books.Children.Clear();groupTitle.Text=selected==""?"Tu estantería":selected;
            foreach(var entry in entries.Where(e=>(selected==""||e.Collection==selected) && (e.Title+" "+e.Author).Contains(search.Text??"",StringComparison.CurrentCultureIgnoreCase))){
                var book=new BookCover(entry,shelf.Media,()=>Run(()=>{var item=shelf.Open(entry.Id);Open(item.Work,item.Revision);}));
                books.Children.Add(book);
            }
            if(books.Children.Count==0)books.Children.Add(Label(entries.Count==0?"Aquí aparecerá tu primera obra.":"Ninguna obra coincide con la búsqueda."));
        }
        controls.Children.Add(Label("Colecciones",15));
        foreach(var collection in new[]{""}.Concat(entries.Select(e=>e.Collection).Where(g=>g!="").Distinct().Order())){
            var item=Action(collection==""?"Todas las obras":collection,()=>{selected=collection;Filter();});
            item.HorizontalAlignment=HorizontalAlignment.Stretch;controls.Children.Add(item);
        }
        search.TextChanged+=(_,_)=>Filter();Filter();
                layout.Children.Add(new ScrollViewer{Content=content});
        var surface=new Grid();
        if(File.Exists(Config("fondo"))){
            try{
                var path=File.ReadAllText(Config("fondo"));using var file=File.OpenRead(Path.Combine(shelf.Media,Path.GetFileName(path)));
                var image=Avalonia.Media.Imaging.Bitmap.DecodeToWidth(file,1920);
                var background=new Image{Source=image,Stretch=Stretch.Uniform,Opacity=.23,Effect=new BlurEffect{Radius=6},IsHitTestVisible=false};
                background.DetachedFromVisualTree+=(_,_)=>image.Dispose();surface.Children.Add(background);
            }catch(IOException){}
        }
        surface.Children.Add(layout);page.Content=surface;
    }
}

