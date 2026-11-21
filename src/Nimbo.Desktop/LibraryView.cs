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
        Text=text,FontSize=size,FontFamily=new FontFamily("Palatino Linotype, Liberation Serif, serif"),TextWrapping=TextWrapping.Wrap
    };
    async void InspectBook(Entry entry)
    {
        var body=Column(12);body.Margin=new Thickness(26);body.Children.Add(Kicker(entry.Type));body.Children.Add(Display(entry.Title,30));
        if(entry.Author!="")body.Children.Add(Label(entry.Author));
        body.Children.Add(new SelectableTextBlock{Text=entry.Description==""?"Sin sinopsis":entry.Description,FontFamily=WritingFamily,FontSize=17,TextWrapping=TextWrapping.Wrap});
        var dialog=new Window{Title="Contraportada",Width=465,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Sheet,Content=new ScrollViewer{Content=body}};
        await dialog.ShowDialog(this);
    }
    void Library()
    {
        if(!TrySave())return;
        session.Close();chapter=null;Navigate("Biblioteca");
        var entries=shelf.List();
        var layout=new DockPanel();
        var content=Column(16);content.MaxWidth=1460;content.Margin=new Thickness(10,6);
        var titleBar=new DockPanel();
        var import=Action("Importar biblioteca…",async()=>await Guard(async()=>{
            var folder=(await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta de LumenWriter"})).FirstOrDefault()?.TryGetLocalPath();
            if(folder==null)return;
            var count=await Task.Run(()=>Legacy.Import(shelf,folder));Library();status.Text=$"{count} obras importadas.";
        }));
        DockPanel.SetDock(import,Dock.Right);titleBar.Children.Add(import);
        titleBar.Children.Add(Display("Mi biblioteca",42));content.Children.Add(titleBar);
        var create=Column(10);
        var title=new TextBox{Watermark="Título de tu obra"};
        var format=new ComboBox{ItemsSource=Format.All,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        var templates=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};
        void SetTemplates(){templates.ItemsSource=EditorialTemplate.For((Format)format.SelectedItem!);templates.SelectedIndex=0;}
        format.SelectionChanged+=(_,_)=>SetTemplates();SetTemplates();
        var createButton=Action("Crear obra",()=>Run(()=>{
            if(string.IsNullOrWhiteSpace(title.Text)){title.Focus();return;}
            var work=((EditorialTemplate)templates.SelectedItem!).Create((Format)format.SelectedItem!,title.Text);
            if(File.Exists(Config("autor")))work.Author=File.ReadAllText(Config("autor"));
            Open(work,shelf.Save(work,0));
        }));
        createButton.Background=Accent;createButton.Foreground=ActualThemeVariant==Avalonia.Styling.ThemeVariant.Light?Brushes.White:Brushes.Black;
        var createFields=new Grid{ColumnDefinitions=new ColumnDefinitions("1.2*,2*,Auto"),ColumnSpacing=8};
        createFields.Children.Add(format);Grid.SetColumn(title,1);createFields.Children.Add(title);Grid.SetColumn(createButton,2);createFields.Children.Add(createButton);
        create.Children.Add(createFields);create.Children.Add(Label("Plantilla"));create.Children.Add(templates);
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
        content.Children.Add(new Expander{Header="Crear una obra",Content=create,IsExpanded=entries.Count==0,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        if(entries.Count>0){
            var latest=entries[0];void Resume()=>Run(()=>{var item=shelf.Open(latest.Id);Open(item.Work,item.Revision);});
            var hero=new Grid{ColumnDefinitions=new ColumnDefinitions("230,*,Auto"),ColumnSpacing=24};
            var jacket=new Border{Background=EditionPalette.Mix(palette.Wood,palette.Ink,.55),Child=Miniature(latest,Resume),CornerRadius=new CornerRadius(4)};
            hero.Children.Add(jacket);
            var info=Column(8);info.VerticalAlignment=VerticalAlignment.Center;info.Children.Add(Kicker("Última obra guardada"));
            info.Children.Add(Display(latest.Title,34));info.Children.Add(Label(string.Join("  ·  ",new[]{latest.Type,latest.Author,latest.Collection}.Where(t=>t!="")),14));
            if(latest.Description!="")info.Children.Add(new TextBlock{Text=latest.Description,MaxLines=2,MaxWidth=560,HorizontalAlignment=HorizontalAlignment.Left,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis});
            Grid.SetColumn(info,1);hero.Children.Add(info);
            var resume=Action("Abrir manuscrito →",Resume);Emphasize(resume);Grid.SetColumn(resume,2);resume.VerticalAlignment=VerticalAlignment.Center;hero.Children.Add(resume);
            content.Children.Insert(1,SheetOf(hero,double.PositiveInfinity));
        }
        var search=new TextBox{Watermark="Buscar por título o autor",Width=225,HorizontalAlignment=HorizontalAlignment.Right};
        var section=new DockPanel();DockPanel.SetDock(search,Dock.Right);section.Children.Add(search);
        var groupTitle=Display("Tu estantería",26);section.Children.Add(groupTitle);content.Children.Add(section);
        var listings=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch};content.Children.Add(listings);
        var browse=new WrapPanel();
        var types=new ComboBox{ItemsSource=new[]{"Todos los formatos"}.Concat(entries.Select(e=>e.Type).Distinct().Order()).ToArray(),SelectedIndex=0};
        var view=new ComboBox{ItemsSource=new[]{"Portadas","Catálogo"},SelectedIndex=ReadPref("vista","Portadas")=="Catálogo"?1:0};
        browse.Children.Add(types);browse.Children.Add(view);content.Children.Insert(content.Children.IndexOf(listings),browse);
        string selected="";
        var savedCollections=File.Exists(Config("estantes"))?File.ReadAllLines(Config("estantes")):[];
        var names=savedCollections.Concat(entries.Select(e=>e.Collection)).Where(s=>s!="").Distinct(StringComparer.CurrentCultureIgnoreCase).Order().ToArray();
        async Task Organize(Entry? target=null){
            var panel=Column(10);panel.Margin=new Thickness(20);
            var name=new TextBox{Watermark="Nombre de la colección",Text=target?.Collection??selected};
            panel.Children.Add(name);
            var known=new ComboBox{ItemsSource=names,PlaceholderText="Elegir colección existente",HorizontalAlignment=HorizontalAlignment.Stretch};
            known.SelectionChanged+=(_,_)=>name.Text=known.SelectedItem as string;panel.Children.Add(known);
            var works=new ListBox{ItemsSource=entries,SelectionMode=SelectionMode.Multiple,Height=230};panel.Children.Add(works);
            if(target!=null)works.SelectedItem=target;
            var window=new Window{Title="Organizar estanterías",Width=450,Height=450,Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            panel.Children.Add(Label("Selecciona las obras. Un nombre vacío las deja sin colección."));
            panel.Children.Add(Action("Guardar colección",()=>Run(()=>{
                var value=(name.Text??"").Trim();
                value=names.FirstOrDefault(s=>s.Equals(value,StringComparison.CurrentCultureIgnoreCase))??value;
                if(value!="" && !names.Contains(value))File.WriteAllLines(Config("estantes"),names.Append(value));
                foreach(var e in works.SelectedItems!.OfType<Entry>()){var item=shelf.Open(e.Id);item.Work.CollectionId=value;shelf.Save(item.Work,item.Revision);}
                window.Close(true);
            })));
            if(await window.ShowDialog<bool>(this))Library();
        }
        browse.Children.Add(Action("Organizar colecciones…",async()=>await Guard(()=>Organize())));
        void Ficha(Entry e){var item=shelf.Open(e.Id);Open(item.Work,item.Revision);Details();}
        void Filter(){
            var groups=Column(10);listings.Content=groups;
            var filtered=entries.Where(e=>(selected==""||e.Collection==selected) && (types.SelectedIndex==0||e.Type==types.SelectedItem as string) && (e.Title+" "+e.Author).Contains(search.Text??"",StringComparison.CurrentCultureIgnoreCase));
            foreach(var group in filtered.GroupBy(e=>e.Collection).OrderBy(g=>g.Key)){
            groups.Children.Add(Label(group.Key==""?"Sin colección":group.Key,18));
            var books=new WrapPanel();
            if(view.SelectedIndex==0){
                books.ItemWidth=Bookcase.Slot;books.ItemHeight=Bookcase.Pitch;
                var rack=new Grid{ClipToBounds=true};rack.Children.Add(new Bookcase(palette));rack.Children.Add(books);groups.Children.Add(rack);
            }else groups.Children.Add(books);
            groupTitle.Text=selected==""?"Tu estantería":selected;
            foreach(var entry in group){
                if(view.SelectedIndex==0){
                    var book=Miniature(entry,()=>Run(()=>{var item=shelf.Open(entry.Id);Open(item.Work,item.Revision);}));
                    var slot=Column(0);var miniature=new Viewbox{Child=book,Height=163,Width=Bookcase.Slot,Stretch=Stretch.Uniform};
                    slot.Children.Add(miniature);
                    var caption=Label(entry.Title,11);caption.TextWrapping=TextWrapping.NoWrap;caption.TextTrimming=TextTrimming.CharacterEllipsis;
                    var tag=Action(entry.Title,()=>Run(()=>Ficha(entry)));
                    tag.Content=caption;tag.HorizontalAlignment=HorizontalAlignment.Center;tag.MaxWidth=Bookcase.Slot-8;tag.Padding=new Thickness(5,1);tag.MinHeight=18;
                    tag.Background=Brush.Parse("#E6D8BC");tag.Foreground=Brush.Parse("#352A24");slot.Children.Add(tag);
                    if(book is BookCover volume)volume.ContextMenu=new ContextMenu{ItemsSource=new[]{MenuAction("Ver portada",()=>volume.ShowJacket(false)),MenuAction("Ver contraportada",()=>volume.ShowJacket(true)),MenuAction("Leer sinopsis completa",()=>InspectBook(entry))}};
                    var menu=book.ContextMenu??new ContextMenu();
                    var commands=new List<MenuItem>{MenuAction("Ficha de la obra",()=>Run(()=>Ficha(entry))),MenuAction("Cambiar colección…",async()=>await Guard(()=>Organize(entry)))};
                    if(menu.ItemsSource is IEnumerable<MenuItem> previous)commands.AddRange(previous);
                    menu.ItemsSource=commands;book.ContextMenu=menu;
                    tag.Content= new TextBlock{Text="Ficha · "+entry.Title,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Brush.Parse("#352A24")};
                    books.Children.Add(slot);
                }else{var record=Column(4);record.Width=215;record.Children.Add(Label(entry.Type));record.Children.Add(Display(entry.Title,20));if(entry.Author!="")record.Children.Add(Label(entry.Author));var open=Action("",()=>Run(()=>{var item=shelf.Open(entry.Id);Open(item.Work,item.Revision);}));open.Content=record;open.Margin=new Thickness(0,0,8,8);record.Children.Add(QuietAction("Ficha de la obra",()=>Run(()=>Ficha(entry))));books.Children.Add(open);}
            }
            }
            if(groups.Children.Count==0)listings.Content=Label(entries.Count==0?"Aquí aparecerá tu primera obra.":"Ninguna obra coincide con la búsqueda.");
        }
        var collections=new ComboBox{ItemsSource=new[]{"Todas las colecciones"}.Concat(names).ToArray(),SelectedIndex=0,MaxWidth=200};
        collections.SelectionChanged+=(_,_)=>{selected=collections.SelectedIndex<1?"":(string)collections.SelectedItem!;Filter();};browse.Children.Insert(0,collections);
        types.SelectionChanged+=(_,_)=>Filter();view.SelectionChanged+=(_,_)=>Run(()=>{File.WriteAllText(Config("vista"),view.SelectedItem!.ToString());Filter();});search.TextChanged+=(_,_)=>Filter();Filter();
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
        surface.Background=Background;surface.Children.Add(layout);page.Content=surface;
    }
}


