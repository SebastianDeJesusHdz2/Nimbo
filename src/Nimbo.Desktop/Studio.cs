using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Nimbo.Core;
using Nimbo.Storage;
using Nimbo.Publishing;
using Velopack;
using Velopack.Sources;

namespace Nimbo.Desktop;

public sealed class Studio : Window
{
    readonly Shelf shelf=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nimbo"));
    readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
    readonly ContentControl page=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1.2)};
    readonly Button updateButton=new(){Content="Actualizar",IsVisible=false};
    readonly UpdateManager updater=new(new GithubSource("https://github.com/SebastianDeJesusHdz2/Nimbo",null,false));
    UpdateInfo? update;
    Work? current;
    Chapter? chapter;
    long revision;
    bool dirty;
    static IBrush Accent=Brush.Parse("#A4DACC");
    static IBrush PanelColor=Brush.Parse("#202A32");

    public Studio()
    {
        Title="Nimbo"; Width=1230; Height=820; MinWidth=760; MinHeight=540;
        Background=Brush.Parse("#141C23");
        var frame=new DockPanel {Margin=new Thickness(24)};
        var footer=Row(); footer.Children.Add(status); footer.Children.Add(updateButton);
        DockPanel.SetDock(footer,Dock.Bottom); frame.Children.Add(footer);
        frame.Children.Add(page); Content=frame;
        timer.Tick+=(_,_)=>{timer.Stop();TrySave();};
        Closing+=(_,e)=>{if(!TrySave()) e.Cancel=true;};
        KeyDown+=(_,e)=>{if(e.Key==Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control)){TrySave();e.Handled=true;}};
        Opened+=async (_,_)=>{
            if(!updater.IsInstalled) return;
            try {update=await updater.CheckForUpdatesAsync();updateButton.IsVisible=update!=null;} catch(HttpRequestException){} catch(Exception ex){status.Text="Actualización: "+ex.Message;}
        };
        updateButton.Click+=async (_,_)=>await Guard(async ()=>{
            if(update==null)return;
            updateButton.IsEnabled=false;
            try {await updater.DownloadUpdatesAsync(update);if(TrySave()) updater.ApplyUpdatesAndRestart(update.TargetFullRelease);}
            finally {updateButton.IsEnabled=true;}
        });
        ApplyTheme();Library();
    }
    static StackPanel Column(double spacing=12)=>new(){Spacing=spacing};
    static StackPanel Row()=>new(){Orientation=Orientation.Horizontal,Spacing=8,Margin=new Thickness(0,6)};
    static TextBlock Label(string text,double size=14)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap};
    static Button Action(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();return b;}
    static Border Card(Control child)=>new(){Background=PanelColor,CornerRadius=new CornerRadius(10),Padding=new Thickness(18),Child=child};
    async Task Guard(Func<Task> action){try{await action();}catch(Exception ex){status.Text=ex.Message;}}
    void Run(Action action){try{action();}catch(Exception ex){status.Text=ex.Message;}}
    void Changed(){dirty=true;status.Text="Cambios pendientes";timer.Stop();timer.Start();}
    bool TrySave()
    {
        if(current==null || !dirty)return true;
        try {current.ModifiedAt=DateTimeOffset.Now.ToString("O");revision=shelf.Save(current,revision);dirty=false;status.Text="Guardado";return true;}
        catch(Exception ex){status.Text=ex.Message;return false;}
    }
    void Library()
    {
        if(!TrySave())return;current=null;chapter=null;
        var root=new DockPanel();var top=Column();var heading=Label("Nimbo",38);heading.Foreground=Accent;top.Children.Add(heading);
        top.Children.Add(Label("Biblioteca",23));
        var create=Row();var format=new ComboBox{ItemsSource=Format.All,SelectedIndex=0,MinWidth=140};
        var name=new TextBox{Watermark="Título de la nueva obra",MinWidth=230};
        create.Children.Add(format);create.Children.Add(name);
        create.Children.Add(Action("Crear obra",()=>Run(()=>{
            if(string.IsNullOrWhiteSpace(name.Text)){name.Focus();return;}
            var work=((Format)format.SelectedItem!).Create(name.Text);
            var rev=shelf.Save(work,0);Open(work,rev);
        })));
        create.Children.Add(Action("Importar biblioteca…",async ()=>await Guard(async ()=>{
            var folder=(await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta de LumenWriter"})).FirstOrDefault()?.TryGetLocalPath();
            if(folder==null)return;
            var count=await Task.Run(()=>Legacy.Import(shelf,folder));Library();status.Text=$"{count} obras importadas.";
        })));
        top.Children.Add(create);top.Children.Add(Action("Preferencias",Preferences));var search=new TextBox{Watermark="Buscar en la biblioteca",MaxWidth=520,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=300};top.Children.Add(search);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var books=new WrapPanel{Margin=new Thickness(0,16)};
        var entries=shelf.List();
        void Filter(){
            books.Children.Clear();
            foreach(var entry in entries.Where(e=>e.Title.Contains(search.Text??"",StringComparison.CurrentCultureIgnoreCase))){
                var tile=Column();tile.Children.Add(new TextBlock{Text=entry.Type,Foreground=Accent});
                tile.Children.Add(Label(entry.Title,20));
                var b=new Button{Content=tile,Width=210,MinHeight=100,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,12)};
                b.Click+=(_,_)=>Run(()=>{var item=shelf.Open(entry.Id);Open(item.Work,item.Revision);});books.Children.Add(b);
            }
        }
        search.TextChanged+=(_,_)=>Filter();Filter();root.Children.Add(new ScrollViewer{Content=books});page.Content=root;
    }
    void Open(Work work,long rev){current=work;revision=rev;dirty=false;chapter=WorldModel.Ordered(work).FirstOrDefault();status.Text="Guardado";Editor();}
    TextBox Field(StackPanel host,string caption,string value,Action<string> assign,bool multi=false)
    {
        host.Children.Add(Label(caption));
        var box=new TextBox{Text=value,AcceptsReturn=multi,TextWrapping=TextWrapping.Wrap,MinHeight=multi?130:32};
        box.TextChanged+=(_,_)=>{if((box.Text??"")==value)return;value=box.Text??"";assign(value);Changed();};
        host.Children.Add(box);return box;
    }
    void Editor()
    {
        var work=current!;var root=new DockPanel();var top=Row();top.Children.Add(Action("‹ Biblioteca",Library));
        top.Children.Add(Action("Obra",Details));top.Children.Add(Action("Glosario",()=>Glossary(null)));
        top.Children.Add(Action("PDF",async ()=>await Export(true)));top.Children.Add(Action("LaTeX",async ()=>await Export(false)));
        top.Children.Add(Action("Guardar",()=>TrySave()));top.Children.Add(Action("Guardar copia",()=>Run(()=>{
            var clone=System.Text.Json.JsonSerializer.Deserialize<Work>(System.Text.Json.JsonSerializer.Serialize(work))!;
            clone.Id=Guid.NewGuid().ToString();clone.Title+=" (copia)";Open(clone,shelf.Save(clone,0));
        })));DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var left=Column();left.Width=196;left.Children.Add(Label(work.Title,22));left.Children.Add(Label(work.Type));
        var outline=new ListBox{ItemsSource=WorldModel.Ordered(work).ToArray(),SelectedItem=chapter,MaxHeight=360};
        outline.SelectionChanged+=(_,_)=>{if(outline.SelectedItem is Chapter c && c!=chapter){chapter=c;Editor();}};left.Children.Add(outline);
        void Add(string level,string title,string parent) {
            chapter=new(){Title=title,Level=level,ParentId=parent,Order=work.Chapters.Count};work.Chapters.Add(chapter);Changed();Editor();
        }
        var section=Format.All.FirstOrDefault(f=>f.Name==work.Type)?.Section??"Capítulo";
        left.Children.Add(Action("+ "+section,()=>Add("Chapter",section+" "+(work.Chapters.Count+1),chapter?.Level=="Arc"?chapter.Id:"")));
        if(work.IsNovel){
            left.Children.Add(Action("+ Arco",()=>Add("Arc","Nuevo arco","")));
            left.Children.Add(Action("+ Subcapítulo",()=>{if(chapter!=null)Add("Subchapter","Nuevo subcapítulo",chapter.Id);}));
        }
        var side=Card(left);side.Margin=new Thickness(0,0,16,10);DockPanel.SetDock(side,Dock.Left);root.Children.Add(side);
        var body=Column();
        if(chapter is {} c){
            Field(body,work.Type=="Presentación"?"Título de diapositiva":"Título de sección",c.Title,v=>c.Title=v);
            if(work.Type is "Apuntes" or "Acta" or "CV")Field(body,work.Type switch{"Apuntes"=>"Preguntas y conceptos","CV"=>"Periodo y ubicación",_=>"Responsable y fecha"},c.Detail,v=>c.Detail=v);
            var input=Field(body,"Texto",c.PlainText,v=>c.PlainText=v,true);input.FontSize=18;input.MinHeight=235;
            var bar=Row();
            void Wrap(string marker){
                int start=Math.Min(input.SelectionStart,input.SelectionEnd),end=Math.Max(input.SelectionStart,input.SelectionEnd);
                var text=input.Text??"";input.Text=text[..start]+marker+text[start..end]+marker+text[end..];input.Focus();input.SelectionStart=start+marker.Length;input.SelectionEnd=end+marker.Length;
            }
            bar.Children.Add(Action("Negrita",()=>Wrap("**")));bar.Children.Add(Action("Cursiva",()=>Wrap("*")));
            void Insert(string text){
                int start=Math.Min(input.SelectionStart,input.SelectionEnd),end=Math.Max(input.SelectionStart,input.SelectionEnd);
                var old=input.Text??"";input.Text=old[..start]+text+old[end..];input.Focus();input.CaretIndex=start+text.Length;
            }
            var formats=Row();formats.Children.Add(Action("Subtítulo",()=>Insert("\n## ")));formats.Children.Add(Action("Lista",()=>Insert("\n- ")));
            formats.Children.Add(Action("Tabla",()=>Insert("\n| Columna 1 | Columna 2 |\n| Dato | Dato |\n")));body.Children.Add(formats);
            bar.Children.Add(Action("Añadir imagen…",async ()=>await Guard(async ()=>{
                var path=(await StorageProvider.OpenFilePickerAsync(new(){Title="Imagen PNG o JPEG"})).FirstOrDefault()?.TryGetLocalPath();
                if(path==null)return;c.Images.Add(shelf.AddImage(path));Changed();Editor();
            })));body.Children.Add(bar);
            if(c.Images.Count>0){body.Children.Add(Label("Disposición de las ilustraciones"));var layout=new ComboBox{ItemsSource=ImageLayouts,SelectedItem=ImageLayouts.Contains(c.Layout)?c.Layout:ImageLayouts[0]};layout.SelectionChanged+=(_,_)=>{if(layout.SelectedItem is string choice){c.Layout=choice;Changed();}};body.Children.Add(layout);}
            foreach(var key in c.Images.ToArray()){
                var row=Row();var imagePath=Path.Combine(shelf.Media,Path.GetFileName(key));
                if(File.Exists(imagePath)){using var stream=File.OpenRead(imagePath);var bmp=Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream,160);var preview=new Image{Source=bmp,Height=78};preview.DetachedFromVisualTree+=(_,_)=>bmp.Dispose();row.Children.Add(preview);}
                row.Children.Add(Action("Quitar imagen",()=>{c.Images.Remove(key);Changed();Editor();}));body.Children.Add(row);
            }
            if(work.Type=="Apuntes")Field(body,"Síntesis",c.Summary,v=>c.Summary=v,true);
        }
        root.Children.Add(new ScrollViewer{Content=Card(body)});page.Content=root;
    }
    void Details(){
        var work=current!;var body=Column();body.Children.Add(Action("‹ Manuscrito",Editor));body.Children.Add(Label("Ficha de la obra",28));
        body.Children.Add(Action("Elegir portada…",async ()=>await Guard(async ()=>{
            var path=(await StorageProvider.OpenFilePickerAsync(new(){Title="Portada PNG o JPEG"})).FirstOrDefault()?.TryGetLocalPath();
            if(path==null)return;work.CoverImage=shelf.AddImage(path);Changed();Details();
        })));
        if(work.CoverImage!=""){
            using var source=File.OpenRead(Path.Combine(shelf.Media,Path.GetFileName(work.CoverImage)));
            var bmp=Avalonia.Media.Imaging.Bitmap.DecodeToWidth(source,220);var img=new Image{Source=bmp,Height=150,HorizontalAlignment=HorizontalAlignment.Left};
            img.DetachedFromVisualTree+=(_,_)=>bmp.Dispose();body.Children.Add(img);
        }
        Field(body,"Título",work.Title,v=>work.Title=v);Field(body,"Autor",work.Author,v=>work.Author=v);Field(body,"Descripción",work.Description,v=>work.Description=v,true);
        page.Content=new ScrollViewer{Content=Card(body)};
    }
    void Glossary(Node? node){
        var work=current!;var body=Column();var bar=Row();bar.Children.Add(Action("‹ Manuscrito",Editor));
        bar.Children.Add(Action("Nueva ficha",()=>{var n=new Node{ParentId=node?.Id??""};work.Glossary.Add(n);Changed();Glossary(n);}));
        bar.Children.Add(Action("Cuadernillo PDF",async ()=>await Export(true,true)));body.Children.Add(bar);body.Children.Add(Label("Glosario",28));
        var list=new ComboBox{ItemsSource=work.Glossary.ToArray(),SelectedItem=node,PlaceholderText="Seleccionar ficha",MinWidth=260};
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is Node n && n!=node)Glossary(n);};body.Children.Add(list);
        if(node!=null){
            Field(body,"Nombre",node.Name,v=>node.Name=v);Field(body,"Clase",node.NodeKind,v=>node.NodeKind=v);
            Field(body,"Trasfondo",node.Background,v=>node.Background=v,true);Field(body,"Relaciones",node.Relations,v=>node.Relations=v);
            var baseOptions=work.Glossary.Where(n=>!WorldModel.WouldCycle(work,node.Id,node.ParentId,n.Id)).ToArray();
            body.Children.Add(Label("Modelo del que recibe campos"));
            var bases=new ComboBox{ItemsSource=baseOptions,SelectedItem=baseOptions.FirstOrDefault(n=>n.Id==node.BaseId),PlaceholderText="Elegir ficha base"};
            bases.SelectionChanged+=(_,_)=>{if(bases.SelectedItem is Node parent){node.BaseId=parent.Id;Changed();Glossary(node);}};body.Children.Add(bases);
            if(node.BaseId!="")body.Children.Add(Action("Quitar ficha base",()=>{node.BaseId="";Changed();Glossary(node);}));
            var fields=Row();var key=new TextBox{Watermark="Campo heredable",MinWidth=165};var initial=new TextBox{Watermark="Valor inicial",MinWidth=140};fields.Children.Add(key);fields.Children.Add(initial);
            fields.Children.Add(Action("Añadir campo",()=>{if(string.IsNullOrWhiteSpace(key.Text))return;node.AttributeDefinitions.Add(new(){Name=key.Text.Trim(),DefaultValue=initial.Text??""});Changed();Glossary(node);}));body.Children.Add(fields);
            Field(body,"Notas",node.Notes,v=>node.Notes=v,true);
            foreach(var field in WorldModel.Effective(work,node)){
                Field(body,field.Definition.Name,field.Value,v=>{
                    var val=node.Attributes.FirstOrDefault(a=>a.DefinitionId==field.Definition.Id);
                    if(val==null){val=new(){DefinitionId=field.Definition.Id};node.Attributes.Add(val);}val.Value=v;
                });
            }
        }
        page.Content=new ScrollViewer{Content=Card(body)};
    }
        static readonly string[] ImageLayouts=["Centrada","Página completa","Ajustar a la izquierda","Ajustar a la derecha"];
    string Config(string name)=>Path.Combine(shelf.Root,name+".txt");
    string? Engine(){
        if(File.Exists(Config("xelatex")))return File.ReadAllText(Config("xelatex"));
        return Environment.GetEnvironmentVariable("NIMBO_XELATEX");
    }
    void Preferences(){
        var body=Column();body.Children.Add(Action("‹ Biblioteca",Library));body.Children.Add(Label("Preferencias",28));
        var theme=new ComboBox{ItemsSource=new[]{"Oscuro","Claro"},SelectedIndex=ActualThemeVariant==Avalonia.Styling.ThemeVariant.Light?1:0};
        theme.SelectionChanged+=(_,_)=>Run(()=>{File.WriteAllText(Config("tema"),theme.SelectedIndex==1?"Claro":"Oscuro");ApplyTheme();});body.Children.Add(Label("Apariencia"));body.Children.Add(theme);
        body.Children.Add(Label("Motor de publicación"));body.Children.Add(Label(Engine()??"XeLaTeX del sistema"));
        body.Children.Add(Action("Seleccionar XeLaTeX…",async ()=>await Guard(async ()=>{
            var path=(await StorageProvider.OpenFilePickerAsync(new(){Title="Seleccionar xelatex o xelatex.exe"})).FirstOrDefault()?.TryGetLocalPath();
            if(path!=null){if(!Path.GetFileNameWithoutExtension(path).Equals("xelatex",StringComparison.OrdinalIgnoreCase))throw new IOException("Selecciona el ejecutable xelatex.");File.WriteAllText(Config("xelatex"),path);Preferences();}
        })));page.Content=Card(body);
    }
    void ApplyTheme(){
        var light=File.Exists(Config("tema")) && File.ReadAllText(Config("tema"))=="Claro";
        RequestedThemeVariant=light?Avalonia.Styling.ThemeVariant.Light:Avalonia.Styling.ThemeVariant.Dark;
        Background=Brush.Parse(light?"#F5F3ED":"#141C23");PanelColor=Brush.Parse(light?"#E7ECE9":"#202A32");Accent=Brush.Parse(light?"#23584E":"#A4DACC");
    }
    async Task Export(bool pdf,bool glossary=false)=>await Guard(async ()=>{
        if(!TrySave())return;
        var snapshot=System.Text.Json.JsonSerializer.Deserialize<Work>(System.Text.Json.JsonSerializer.Serialize(current!))!;
        var ext=pdf?"pdf":"zip";var file=await StorageProvider.SaveFilePickerAsync(new(){Title=pdf?"Exportar PDF":"Proyecto LaTeX",DefaultExtension=ext,SuggestedFileName=(glossary?"Glosario - ":"")+snapshot.Title+"."+ext});
        if(file?.TryGetLocalPath() is not string path)return;
        status.Text="Componiendo…";await Task.Run(()=>Publisher.Export(snapshot,shelf.Media,path,pdf,glossary,Engine()));status.Text="Exportado · "+Path.GetFileName(path);
    });
}



