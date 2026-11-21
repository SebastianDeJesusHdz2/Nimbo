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

public sealed partial class Studio
{
    void Editor()
    {
        Navigate("Manuscrito");
        var work=current!;masthead.IsVisible=!work.FocusWriting;var root=new DockPanel();var top=new WrapPanel{Margin=new Thickness(0,2,0,5)};
        var header=new DockPanel{Margin=new Thickness(0,0,0,12)};
        var menu=new MenuFlyout();
        menu.Items.Add(MenuAction("Vista previa de impresión",async()=>await Preview()));
        menu.Items.Add(MenuAction("Exportar PDF…",async()=>await Export(true)));
        menu.Items.Add(MenuAction("Proyecto LaTeX…",async()=>await Export(false)));
        if(work.IsNovel)menu.Items.Add(MenuAction("Edición completa y fichas…",async()=>await Guard(async()=>{
            if(!TrySave())return;
            var snapshot=session.Snapshot();
            var parent=(await StorageProvider.OpenFolderPickerAsync(new(){Title="Dónde guardar la edición completa"})).FirstOrDefault()?.TryGetLocalPath();
            if(parent==null)return;
            var stem="Edición - "+string.Join("_",snapshot.Title.Split(Path.GetInvalidFileNameChars()));
            var path=Path.Combine(parent,stem);int copy=1;
            while(Directory.Exists(path)||File.Exists(path))path=Path.Combine(parent,stem+" ("+(++copy)+")");
            status.Text="Componiendo el manuscrito y las fichas…";var engine=Engine();
            await Task.Run(()=>ReadingEdition.Write(snapshot,shelf.Media,path,engine));status.Text="Edición guardada en "+path;
        })));
        menu.Items.Add(MenuAction("Guardar copia",()=>Run(()=>{var clone=session.SaveCopy();Open(clone,session.Revision);})));
        var publish=new Button{Content="Publicar  ▾",Flyout=menu};Emphasize(publish);
        DockPanel.SetDock(publish,Dock.Right);header.Children.Add(publish);header.Children.Add(top);
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var left=Column(12);left.Width=202;
        left.Children.Add(WorkArt(work,360,116));
        left.Children.Add(Kicker(work.Type));left.Children.Add(Display(work.Title,25));left.Children.Add(Rule(Edge));
        left.Children.Add(Label("Índice del manuscrito",14));
        var outline=Column(3);var titles=new Dictionary<Chapter,TextBlock>();
        foreach(var entry in WorldModel.Ordered(work)){
            var label=Label(entry.Title,14);titles[entry]=label;
            var item=QuietAction("",()=>{if(entry==chapter)return;chapter=entry;RememberSection(work,entry);Editor();});
            var indent=entry.ParentId==""?0:entry.Level=="Subchapter"?20:10;
            item.Padding=new Thickness(10+indent,8,8,8);item.HorizontalAlignment=HorizontalAlignment.Stretch;
            item.HorizontalContentAlignment=HorizontalAlignment.Left;item.Content=label;
            if(chapter==entry){item.Background=Sheet;item.BorderThickness=new Thickness(2,0,0,0);item.BorderBrush=Accent;item.Foreground=Accent;}
            outline.Children.Add(item);
        }
        left.Children.Add(outline);
        void Add(string level, string title, string parent)
        {
            chapter = new() { Title = title, Level = level, ParentId = parent, Order = work.Chapters.Count }; work.Chapters.Add(chapter); RememberSection(work,chapter); Changed(); Editor();
        }
        var section = work.IsNovel ? "Capítulo" : Format.All.FirstOrDefault(f => f.Name == work.Type)?.Section ?? "Sección";
        var structure=new MenuFlyout();
        Chapter? FindChapter(string? id)=>work.Chapters.FirstOrDefault(n=>n.Id==id);
        var parentChapter=chapter?.Level=="Subchapter"?FindChapter(chapter.ParentId):chapter;
        var arc=parentChapter?.Level=="Arc"?parentChapter:FindChapter(parentChapter?.ParentId);
        structure.Items.Add(MenuAction("Añadir "+section.ToLowerInvariant(),()=>Add("Chapter",section+" "+(work.Chapters.Count+1),arc?.Level=="Arc"?arc.Id:"")));
        if(work.IsNovel){
            structure.Items.Add(MenuAction("Añadir arco",()=>Add("Arc","Nuevo arco","")));
            var sub=MenuAction("Añadir subcapítulo",()=>Add("Subchapter","Nuevo subcapítulo",parentChapter!.Id));
            sub.IsEnabled=parentChapter is {Level:"Chapter"};structure.Items.Add(sub);
        }
        if(chapter!=null){
            var siblings=work.Chapters.Where(c=>c.ParentId==chapter.ParentId).OrderBy(c=>c.Order).ToList();
            void Move(int step){
                int i=siblings.IndexOf(chapter);(siblings[i],siblings[i+step])=(siblings[i+step],siblings[i]);
                for(int n=0;n<siblings.Count;n++)siblings[n].Order=n;Changed();Editor();
            }
            int pos=siblings.IndexOf(chapter);
            var up=MenuAction("Subir en el índice",()=>Move(-1));up.IsEnabled=pos>0;structure.Items.Add(up);
            var down=MenuAction("Bajar en el índice",()=>Move(1));down.IsEnabled=pos<siblings.Count-1;structure.Items.Add(down);
        }
        left.Children.Add(new Button{Content="Organizar índice  ▾",Flyout=structure,HorizontalAlignment=HorizontalAlignment.Stretch});
        var side=new Border{Background=PanelColor,Child=new ScrollViewer{Content=left},Padding=new Thickness(12,12,16,12),BorderBrush=Edge,BorderThickness=new Thickness(0,0,1,0)};
        DockPanel.SetDock(side,Dock.Left);root.Children.Add(side);
        bool narrowIndex=false;
        var indexToggle=QuietAction("Índice",()=>{narrowIndex=!side.IsVisible;side.IsVisible=narrowIndex;});
        top.Children.Add(indexToggle);
        top.Children.Add(QuietAction(work.FocusWriting?"Salir de concentración":"Concentración",()=>{work.FocusWriting=!work.FocusWriting;Changed();Editor();}));
        top.Children.Add(QuietAction("Guardar",()=>TrySave()));
        root.SizeChanged+=(_,e)=>{bool small=e.NewSize.Width<900;indexToggle.IsVisible=small&&!work.FocusWriting;side.IsVisible=!work.FocusWriting&&(!small||narrowIndex);};
        side.IsVisible=!work.FocusWriting;
        var body = Column();body.MaxWidth=ReadPref("ancho","Medio") switch {"Amplio"=>1120,"Estrecho"=>680,_=>860};body.HorizontalAlignment=HorizontalAlignment.Stretch;
        if (chapter is { } c)
        {
            var kind=c.Level=="Arc"?"Arco":c.Level=="Subchapter"?"Subcapítulo":section;
            var ordinal=work.Chapters.Where(n=>n.Level==c.Level && n.ParentId==c.ParentId).OrderBy(n=>n.Order).ToList().IndexOf(c)+1;
            body.Children.Add(Kicker(kind+"  /  "+ordinal.ToString("D2")));
            var marginContent=new ScrollViewer{Content=WritingMargin(work,c)};
            var margin=new Border{Background=EditionPalette.Mix(palette.Binding,IsLight?"#FFFFFF":"#212426",IsLight?.87:.80),Child=marginContent,Width=264,Padding=new Thickness(20,18,10,12),
                BorderBrush=Edge,BorderThickness=new Thickness(1,0,0,0)};
            DockPanel.SetDock(margin,Dock.Right);root.Children.Add(margin);
            var pocket=new Button{Content="Cuaderno",Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
            var pop=new Flyout();pocket.Flyout=pop;
            pop.Opening+=(_,_)=>{margin.Child=null;marginContent.Width=250;marginContent.MaxHeight=430;pop.Content=marginContent;};
            pop.Closed+=(_,_)=>{pop.Content=null;marginContent.Width=double.NaN;marginContent.MaxHeight=double.PositiveInfinity;margin.Child=marginContent;};
            top.Children.Add(pocket);
            void FitMargin(double width){margin.IsVisible=!work.FocusWriting && width>=1260;pocket.IsVisible=!margin.IsVisible;}
            FitMargin(Bounds.Width-130);root.SizeChanged+=(_,e)=>FitMargin(e.NewSize.Width);
            var heading=new TextBox{Text=c.Title,Watermark="Título",FontFamily=WritingFamily,FontSize=43,Padding=new Thickness(0,4),Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
            heading.TextChanged+=(_,_)=>{if(heading.Text==c.Title)return;c.Title=heading.Text??"";titles[c].Text=c.Title;Changed();};body.Children.Add(heading);
            if (!work.IsNovel && work.Type!="Presentación") Field(body,EditorialTemplate.DetailLabel(work.Type),c.Detail,v=>c.Detail=v);
            if (work.Type == "Presentación")
            {
                var options = new[] { "Portada", "Texto", "Texto e imagen", "Plano y leyenda", "Moodboard y paleta", "Moodboard y landmark", "Imagen protagonista", "Dos columnas" };
                body.Children.Add(Label("Composición de diapositiva"));
                var layouts = new ComboBox { ItemsSource = options, SelectedItem = options.Contains(c.Layout) ? c.Layout : "Texto" };
                layouts.SelectionChanged += (_, _) => { if (layouts.SelectedItem is string choice) { c.Layout = choice; Changed(); } };
                body.Children.Add(layouts); Field(body, "Texto secundario", c.Summary, v => c.Summary = v, true);
            }
            bool illustrated=!continuousDraft && work.Type!="Presentación";
            var draft=new DraftSurface(this,c,illustrated);
            var bar = new WrapPanel();
            void Wrap(string marker)
            {
                var input=draft.Active;int start = Math.Min(input.SelectionStart, input.SelectionEnd), end = Math.Max(input.SelectionStart, input.SelectionEnd);
                var text=input.Text??"";string selected=text[start..end];
                bool marked=start>=marker.Length && end+marker.Length<=text.Length && text[(start-marker.Length)..start]==marker && text[end..(end+marker.Length)]==marker;
                if(marked){
                    input.SelectionStart=start-marker.Length;input.SelectionEnd=end+marker.Length;input.SelectedText=selected;start-=marker.Length;
                }else{input.SelectedText=marker+selected+marker;start+=marker.Length;}
                input.Focus();input.SelectionStart=start;input.SelectionEnd=start+selected.Length;
            }
            draft.View.AddHandler(InputElement.KeyDownEvent,(_,e)=>{
                if(e.KeyModifiers!=KeyModifiers.Control || e.Key is not (Key.B or Key.I))return;
                Wrap(e.Key==Key.B?"**":"*");e.Handled=true;
            },Avalonia.Interactivity.RoutingStrategies.Tunnel);
            var bold=QuietAction("B",()=>Wrap("**"));bold.FontWeight=FontWeight.Bold;ToolTip.SetTip(bold,"Negrita · Ctrl+B");
            var italic=QuietAction("I",()=>Wrap("*"));italic.FontStyle=FontStyle.Italic;ToolTip.SetTip(italic,"Cursiva · Ctrl+I");
            bar.Children.Add(bold);bar.Children.Add(italic);
            void Insert(string text)
            {
                var input=draft.Active;int start = Math.Min(input.SelectionStart, input.SelectionEnd), end = Math.Max(input.SelectionStart, input.SelectionEnd);
                input.SelectedText=text; input.Focus(); input.CaretIndex = start + text.Length;
            }
            void ParagraphStyle(string prefix)
            {
                var box=draft.Active;var text=box.Text??"";
                int start=Math.Min(box.SelectionStart,box.SelectionEnd),end=Math.Max(box.SelectionStart,box.SelectionEnd);
                int first=start==0?0:text.LastIndexOf('\n',start-1)+1;
                int last=end>start && text[end-1]=='\n'?end-1:text.IndexOf('\n',end);
                if(last<0)last=text.Length;
                var lines=text[first..last].Split('\n');
                bool remove=prefix.Length>0 && lines.All(l=>l.StartsWith(prefix,StringComparison.Ordinal));
                string FormatLine(string line){
                    if(line.StartsWith("## "))line=line[3..];else if(line.StartsWith("- ") || line.StartsWith("> "))line=line[2..];
                    return remove?line:prefix+line;
                }
                string formatted=string.Join("\n",lines.Select(FormatLine));
                box.SelectionStart=first;box.SelectionEnd=last;box.SelectedText=formatted;box.Focus();
                box.SelectionStart=first;box.SelectionEnd=first+formatted.Length;
            }
            var paragraph=new MenuFlyout();
            paragraph.Items.Add(MenuAction("Párrafo normal",()=>ParagraphStyle("")));
            paragraph.Items.Add(MenuAction("Subtítulo",()=>ParagraphStyle("## ")));
            paragraph.Items.Add(MenuAction("Lista con viñetas",()=>ParagraphStyle("- ")));
            paragraph.Items.Add(MenuAction("Cita en bloque",()=>ParagraphStyle("> ")));
            bar.Children.Add(new Button{Content="Párrafo  ▾",Flyout=paragraph,Background=Brushes.Transparent,BorderThickness=new Thickness(0)});
            var insert=new MenuFlyout();
            if(work.IsNovel)insert.Items.Add(MenuAction("Salto de página",()=>Insert("\n[[Página]]\n")));
            insert.Items.Add(MenuAction("Tabla",()=>Insert("\n| Columna 1 | Columna 2 |\n| Dato | Dato |\n")));
            async Task InsertPictures(bool gallery){
                var paths=await PickIllustrations(true);
                if(paths.Length==0)return;
                var keys=paths.Select(shelf.AddImage).ToArray();
                int position=draft.Cursor;
                foreach(var key in keys){
                    int index=c.Images.Count;c.Images.Add(key);
                    if(gallery)c.ImageLayouts[index]=Illustrations.Gallery;
                    if(work.Type!="Presentación")position=Illustrations.Move(c,index,null,position);
                }
                c.CaretOffset=position;
                Changed();Editor();
            }
            bar.Children.Add(new Button{Content="Insertar  ▾",Flyout=insert,Background=Brushes.Transparent,BorderThickness=new Thickness(0)});
            var pictures=new MenuFlyout();
            pictures.Items.Add(MenuAction(work.Type=="Presentación"?"Añadir a la diapositiva…":"Insertar donde escribo…",async()=>await Guard(()=>InsertPictures(false))));
            if(work.Type!="Presentación")pictures.Items.Add(MenuAction("Insertar varias en una fila…",async()=>await Guard(()=>InsertPictures(true))));
            var imageTool=new Button{Content="Imágenes  ▾",Flyout=pictures};bar.Children.Add(imageTool);
            if(c.Images.Count!=0 && work.Type!="Presentación"){
                bar.Children.Add(QuietAction(illustrated?"Texto continuo":"Mostrar imágenes",()=>{continuousDraft=illustrated;Editor();}));
                bar.Children.Add(QuietAction("Lectura",()=>ReadSection(c)));
            }
            var barFrame=new Border{Background=EditionPalette.Mix(palette.Detail,IsLight?"#FFFFFF":"#252527",IsLight?.85:.78),IsVisible=!work.FocusWriting,Child=bar,BorderBrush=Edge,BorderThickness=new Thickness(0,1),Padding=new Thickness(0,4)};
            if(work.FocusWriting)top.Children.Add(QuietAction("Formato",()=>barFrame.IsVisible=!barFrame.IsVisible));
            body.Children.Add(barFrame);AddSearch(root,body,bar,draft);body.Children.Add(draft.View);
            var count=Label("",12);void Count(){var words=System.Text.RegularExpressions.Regex.Count(Illustrations.Marker().Replace(c.PlainText," ").Replace("[[Página]]"," "), @"[\p{L}\p{N}]+");count.Text=$"{words:N0} palabras   ·   {Math.Max(1,(int)Math.Ceiling(words/210d))} min de lectura";}Count();draft.Edited+=Count;body.Children.Add(count);
            if(c.Images.Count!=0 && !illustrated){
                var assets=new Expander{Header=$"Ilustraciones de la sección  ·  {c.Images.Count}",HorizontalAlignment=HorizontalAlignment.Stretch};
                assets.Expanding+=(_,_)=>{
                    if(assets.Content!=null)return;
                    var shelfContent=Column(8);
                    if(work.Type!="Presentación")Choice(shelfContent,"Disposición",ImageLayouts,c.Layout,v=>{c.Layout=v;Changed();});
                    var cards=new WrapPanel();
                    foreach(var (key,index) in c.Images.Select((key,index)=>(key,index))){
                        var card=Column(5);card.Margin=new Thickness(0,0,12,8);
                        if(Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(key)),180,90) is {} thumb)card.Children.Add(thumb);
                        card.Children.Add(Label("Imagen "+(index+1),13));
                        card.Children.Add(QuietAction("Quitar",()=>{Illustrations.Remove(c,index);Changed();Editor();}));cards.Children.Add(card);
                    }
                    shelfContent.Children.Add(cards);assets.Content=shelfContent;
                };
                body.Children.Add(assets);
            }
            if(work.Type=="CV")ResumeEntries(body,c);
            if (work.Type == "Apuntes") Field(body, "Síntesis", c.Summary, v => c.Summary = v, true);
        }
        var sheet=new Border{Child=body,Padding=new Thickness(36,30,36,24),MaxWidth=body.MaxWidth+72,HorizontalAlignment=HorizontalAlignment.Stretch};
        root.Children.Add(new ScrollViewer{Content=sheet}); page.Content = root;
    }
    void Details()
    {
        Navigate("Ficha");var work = current!; var body = Column();body.MaxWidth=ReadPref("ancho","Medio") switch {"Amplio"=>1120,"Estrecho"=>680,_=>860};body.HorizontalAlignment=HorizontalAlignment.Stretch; body.Children.Add(Kicker(work.Type));body.Children.Add(Display("Ficha de la obra",36));
        var jacket=new DockPanel();var coverOptions=Column(8);
        var proof=new ContentControl{Width=160,Height=222,Margin=new Thickness(0,0,22,0)};
        void RefreshJacket(){
            var entry=new Entry(work.Id,work.Title,work.Type,0,work.CoverImage,work.CoverColor,work.Author,work.Description,work.CollectionId);
            proof.Content=Miniature(entry,()=>{if(work.IsNovel)InspectBook(entry);else ReadSection(chapter);});
        }
        RefreshJacket();DockPanel.SetDock(proof,Dock.Left);jacket.Children.Add(proof);
        coverOptions.VerticalAlignment=VerticalAlignment.Center;coverOptions.Children.Add(Kicker(work.IsNovel?"Encuadernación":"Miniatura"));
        var inks=new WrapPanel();
        foreach(var hex in palette.Swatches.Prepend(work.CoverColor).Where(s=>Color.TryParse(s,out _)).Distinct(StringComparer.OrdinalIgnoreCase)){
            var swatch=new Button{Width=30,Height=30,MinWidth=0,Padding=new Thickness(0),Margin=new Thickness(0,0,5,4),Background=Brush.Parse(hex),CornerRadius=new CornerRadius(15),
                BorderBrush=Foreground,BorderThickness=new Thickness(hex==work.CoverColor?2:0)};
            ToolTip.SetTip(swatch,"Pasta "+hex);Avalonia.Automation.AutomationProperties.SetName(swatch,"Pasta "+hex);
            swatch.Click+=(_,_)=>{work.CoverColor=hex;Changed();RefreshJacket();foreach(var b in inks.Children.OfType<Button>())b.BorderThickness=new Thickness(b==swatch?2:0);};inks.Children.Add(swatch);
        }
        coverOptions.Children.Add(inks);
        coverOptions.Children.Add(Action("Elegir portada…", async () => await Guard(async () =>
        {
            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return; work.CoverImage = shelf.AddImage(path); Changed(); Details();
        })));
        if(work.CoverImage!="")coverOptions.Children.Add(QuietAction("Quitar imagen de portada",()=>{work.CoverImage="";Changed();Details();}));
        jacket.Children.Add(coverOptions);body.Children.Add(jacket);
        var coverPage=body;var form=Column(8);var print=Column(8);body=form;
        var titleBox=Field(body,"Título",work.Title,v=>work.Title=v);
        var authorBox=Field(body,"Autor",work.Author,v=>work.Author=v);
        Field(body, "Subtítulo", work.Subtitle, v => work.Subtitle = v); Field(body, "Colección", work.CollectionId, v => work.CollectionId = v);
        var blurb=Field(body,"Sinopsis de contraportada",work.Description,v=>work.Description=v,true);
        foreach(var field in new[]{titleBox,authorBox,blurb})field.LostFocus+=(_,_)=>RefreshJacket();
        if(work.Type=="CV")ResumeProfile(body,work);
        var facts=EditorialTemplate.FrontMatter(work.Type);
        if(facts.Length>0){
            var data=Column(6);data.Children.Add(Display("Datos de "+work.Type.ToLowerInvariant(),22));
            foreach(var key in facts)Field(data,key,work.DocumentFacts.GetValueOrDefault(key,""),v=>work.DocumentFacts[key]=v);
            body.Children.Add(Card(data));
        }
        PrintSettings(print,work);
        if (work.Editions.Count > 0)
        {
            body.Children.Add(Label("Edición anterior", 21));
            foreach (var edition in work.Editions)
            {
                body.Children.Add(Action("Guardar copia de " + edition.Name, async () => await Guard(async () =>
                {
                    var destination = await StorageProvider.SaveFilePickerAsync(new() { Title = "Guardar edición anterior", SuggestedFileName = Path.GetFileName(edition.Name) });
                    if (destination?.TryGetLocalPath() is not string path) return;
                    var source = Path.Combine(shelf.Media, Path.GetFileName(edition.Key));
                    var temp = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.Copy(source, temp); File.Move(temp, path, true); } finally { File.Delete(temp); }
                    status.Text = "Edición anterior guardada";
                })));
            }
        }
        var tabs=new TabControl{ItemsSource=new[]{new TabItem{Header="Datos editoriales",Content=form},new TabItem{Header="Impresión",Content=print}},SelectedIndex=0};
        coverPage.Children.Add(tabs);page.Content=new ScrollViewer{Content=SheetOf(coverPage,940)};
    }
}










