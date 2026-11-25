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
        menu.Items.Add(MenuAction("Ajustes de impresión y fuente LaTeX",()=>Details(true)));
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
        if(work.UseTexSource){
            top.Children.Add(Display(work.Title,23));
            top.Children.Add(QuietAction("Fuente y recursos",()=>Details(true)));
            top.Children.Add(QuietAction("Guardar",()=>TrySave()));
            var code=new TextBox{Text=work.TexSource,AcceptsReturn=true,AcceptsTab=true,FontFamily=new FontFamily("Consolas, Liberation Mono, monospace"),FontSize=15,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,Padding=new Thickness(16)};
            code.TextChanged+=(_,_)=>{work.TexSource=code.Text??"";Changed();};
            root.Children.Add(code);page.Content=root;return;
        }
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
        if(work.Type=="Presentación"){
            foreach(var presetLayout in SlideLayouts.Where(l=>l!="Portada")){
                var preset=presetLayout;
                structure.Items.Add(MenuAction("Añadir diapositiva: "+preset,()=>{
                    chapter=new(){Title=preset,Level="Chapter",Order=work.Chapters.Count,Layout=preset};
                    work.Chapters.Add(chapter);RememberSection(work,chapter);Changed();Editor();
                }));
            }
        }
        else if(!work.IsNovel){
            var fmt=Format.All.FirstOrDefault(f=>f.Name==work.Type);
            var activeTpl=fmt!=null?EditorialTemplate.For(fmt).FirstOrDefault(t=>t.Name==work.Template)??EditorialTemplate.For(fmt)[0]:null;
            if(activeTpl!=null){
                foreach(var secName in activeTpl.Sections){
                    var targetTitle=secName;
                    structure.Items.Add(MenuAction("Añadir sección de plantilla: "+targetTitle,()=>Add("Chapter",targetTitle,"")));
                }
            }
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
            Action? refreshSlidePreview=null;
            heading.TextChanged+=(_,_)=>{if(heading.Text==c.Title)return;c.Title=heading.Text??"";titles[c].Text=c.Title;Changed();refreshSlidePreview?.Invoke();};body.Children.Add(heading);
            if (!work.IsNovel && work.Type!="Presentación") Field(body,EditorialTemplate.DetailLabel(work.Type),c.Detail,v=>{c.Detail=v;refreshSlidePreview?.Invoke();});
            if (work.Type == "Presentación")
            {
                var previewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                refreshSlidePreview = () =>
                {
                    previewHost.Content = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        MaxHeight = 250,
                        Child = BuildSlideCanvas(work, c, 480)
                    };
                };
                refreshSlidePreview();
                var previewHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var openStage = QuietAction("Abrir vista de presentación 16:9 →", () => ReadSection(c));
                DockPanel.SetDock(openStage, Dock.Right);
                previewHeader.Children.Add(openStage);
                previewHeader.Children.Add(Kicker("Vista previa en vivo de la diapositiva (16:9)"));
                var previewCard = Column(6);
                previewCard.Children.Add(previewHeader);
                previewCard.Children.Add(previewHost);
                body.Children.Add(Card(previewCard));

                var options = SlideLayouts;
                var metaGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
                var leftMeta = Column(6);
                leftMeta.Children.Add(Label("Composición de diapositiva"));
                var layouts = new ComboBox { ItemsSource = options, SelectedItem = options.Contains(c.Layout) ? c.Layout : "Texto e imagen", HorizontalAlignment = HorizontalAlignment.Stretch };
                layouts.SelectionChanged += (_, _) => { if (layouts.SelectedItem is string choice && choice != c.Layout) { c.Layout = choice; Changed(); Editor(); } };
                leftMeta.Children.Add(layouts);
                metaGrid.Children.Add(leftMeta);

                var rightMeta = Column(6);
                var detailLabel = c.Layout == "Ficha de concepto" ? "Género o clasificación (ej. Terror psicológico)" : c.Layout == "Portada" ? "Materia o encabezado superior" : "Subtítulo o encabezado secundario";
                var detailBox = Field(rightMeta, detailLabel, c.Detail, v => { c.Detail = v; refreshSlidePreview?.Invoke(); });
                Grid.SetColumn(rightMeta, 1);
                metaGrid.Children.Add(rightMeta);
                body.Children.Add(metaGrid);

                var summaryLabel = c.Layout == "Ficha de concepto" ? "Concepto principal (esquina superior derecha)" : c.Layout == "Portada" ? "Actividad o subtítulo de portada (ej. ACT 2)" : "Texto secundario, resumen o leyenda lateral";
                Field(body, summaryLabel, c.Summary, v => { c.Summary = v; refreshSlidePreview?.Invoke(); }, c.Layout != "Portada");
            }
            else if (work.IsNovel)
            {
                var novelPreviewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                bool showNovelCoverInEditor = false;
                var toggleNovelViewBtn = QuietAction("Ver portada", () => { });
                refreshSlidePreview = () =>
                {
                    toggleNovelViewBtn.Content = showNovelCoverInEditor ? "Ver hoja del capítulo actual" : "Ver portada de novela";
                    novelPreviewHost.Content = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        MaxHeight = 235,
                        Child = showNovelCoverInEditor
                            ? BuildNovelCoverCanvas(work, false, 420)
                            : BuildDocumentSectionCanvas(work, c, 420)
                    };
                };
                toggleNovelViewBtn.Click += (_, _) => { showNovelCoverInEditor = !showNovelCoverInEditor; refreshSlidePreview(); };
                refreshSlidePreview();
                var novelHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var openReading = QuietAction("Abrir lectura por páginas →", () => ReadSection(c));
                DockPanel.SetDock(openReading, Dock.Right);
                novelHeader.Children.Add(openReading);
                DockPanel.SetDock(toggleNovelViewBtn, Dock.Right);
                novelHeader.Children.Add(toggleNovelViewBtn);
                novelHeader.Children.Add(Kicker("Vista en vivo de la página actual"));
                var coverBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
                var coverCombo = new ComboBox
                {
                    ItemsSource = EditorialTemplate.NovelCoverStyles,
                    SelectedItem = EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0],
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                coverCombo.SelectionChanged += (_, _) =>
                {
                    if (coverCombo.SelectedItem is string cs && cs != work.CoverStyle)
                    {
                        work.CoverStyle = cs;
                        showNovelCoverInEditor = true;
                        Changed();
                        refreshSlidePreview?.Invoke();
                    }
                };
                coverBar.Children.Add(coverCombo);
                var pickCoverBtn = QuietAction("Imagen de portada…", async () => await Guard(async () =>
                {
                    var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
                    if (path == null) return;
                    work.CoverImage = shelf.AddImage(path);
                    showNovelCoverInEditor = true;
                    Changed();
                    refreshSlidePreview?.Invoke();
                }));
                Grid.SetColumn(pickCoverBtn, 1);
                coverBar.Children.Add(pickCoverBtn);
                var novelCard = Column(8);
                novelCard.Children.Add(novelHeader);
                novelCard.Children.Add(novelPreviewHost);
                var coverExp = new Expander
                {
                    Header = $"Formato de portada de novela ({(EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0])})",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = coverBar
                };
                novelCard.Children.Add(coverExp);
                body.Children.Add(Card(novelCard));
            }
            else
            {
                var docPreviewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                refreshSlidePreview = () =>
                {
                    var pages = BuildDocumentPages(work, 400);
                    var sectionPages = pages.Where(p => p.Section == c).ToList();
                    if (sectionPages.Count == 0 && pages.Count > 0) sectionPages.Add(pages[0]);
                    if (sectionPages.Count >= 2)
                    {
                        var twoGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
                        twoGrid.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 235, Child = sectionPages[0].Sheet });
                        var secondVb = new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 235, Child = sectionPages[1].Sheet };
                        Grid.SetColumn(secondVb, 1);
                        twoGrid.Children.Add(secondVb);
                        docPreviewHost.Content = twoGrid;
                    }
                    else if (sectionPages.Count == 1)
                    {
                        docPreviewHost.Content = new Viewbox
                        {
                            Stretch = Stretch.Uniform,
                            MaxHeight = 235,
                            Child = sectionPages[0].Sheet
                        };
                    }
                };
                refreshSlidePreview();
                var docHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var openDocReading = QuietAction($"Abrir lectura paginada de {work.Type.ToLowerInvariant()} →", () => ReadSection(c));
                DockPanel.SetDock(openDocReading, Dock.Right);
                docHeader.Children.Add(openDocReading);
                docHeader.Children.Add(Kicker($"Vista en vivo de la hoja actual  ·  {work.Type}"));

                var format = Format.All.FirstOrDefault(f => f.Name == work.Type);
                var templateNames = format != null ? EditorialTemplate.For(format).Select(t => t.Name).ToArray() : [work.Template];
                var docCard = Column(8);
                docCard.Children.Add(docHeader);

                if (templateNames.Length > 1)
                {
                    var fmtRow = Column(4);
                    fmtRow.Children.Add(Label($"Plantilla de {work.Type.ToLowerInvariant()}"));
                    var tplCombo = new ComboBox
                    {
                        ItemsSource = templateNames,
                        SelectedItem = templateNames.Contains(work.Template) ? work.Template : templateNames[0],
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    tplCombo.SelectionChanged += (_, _) =>
                    {
                        if (tplCombo.SelectedItem is string chosen && chosen != work.Template)
                        {
                            work.Template = chosen;
                            if (format != null)
                            {
                                var tplObj = EditorialTemplate.For(format).FirstOrDefault(t => t.Name == chosen);
                                if (tplObj != null)
                                {
                                    foreach (var secName in tplObj.Sections)
                                    {
                                        if (!work.Chapters.Any(ch => ch.Title.Equals(secName, StringComparison.OrdinalIgnoreCase)))
                                            work.Chapters.Add(new Chapter { Title = secName, Order = work.Chapters.Count });
                                    }
                                }
                            }
                            Changed();
                            Editor();
                        }
                    };
                    fmtRow.Children.Add(tplCombo);
                    docCard.Children.Add(fmtRow);
                }

                docCard.Children.Add(docPreviewHost);

                var frontKeys = work.Type == "CV"
                    ? ["Nombre profesional", "Profesión", "Correo", "Teléfono", "Ciudad", "Portafolio", "Perfil profesional en línea"]
                    : EditorialTemplate.FrontMatter(work.Type);
                if (frontKeys.Length > 0)
                {
                    var metaExpander = new Expander
                    {
                        Header = work.Type == "CV"
                            ? "Datos profesionales del encabezado"
                            : $"Datos del encabezado de {work.Type.ToLowerInvariant()} ({frontKeys.Length} campos)",
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var fieldsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12, RowSpacing = 6 };
                    for (int idx = 0; idx < frontKeys.Length; idx++)
                    {
                        if (idx % 2 == 0) fieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                        var key = frontKeys[idx];
                        var cellCol = Column(2);
                        var currentVal = work.Type == "CV" ? work.Contact.GetValueOrDefault(key, "") : work.DocumentFacts.GetValueOrDefault(key, "");
                        Field(cellCol, key, currentVal, val =>
                        {
                            if (work.Type == "CV") work.Contact[key] = val;
                            else work.DocumentFacts[key] = val;
                            refreshSlidePreview?.Invoke();
                        });
                        Grid.SetRow(cellCol, idx / 2);
                        Grid.SetColumn(cellCol, idx % 2);
                        fieldsGrid.Children.Add(cellCol);
                    }
                    metaExpander.Content = fieldsGrid;
                    docCard.Children.Add(metaExpander);
                }

                body.Children.Add(Card(docCard));
            }
            bool illustrated=!continuousDraft && work.Type!="Presentación";
            var draft=new DraftSurface(this,c,illustrated);
            if(refreshSlidePreview!=null)draft.Edited+=refreshSlidePreview;
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
                    if(line.StartsWith("### "))line=line[4..];else if(line.StartsWith("## "))line=line[3..];else if(line.StartsWith("- ") || line.StartsWith("> "))line=line[2..];else if(line.StartsWith("1. "))line=line[3..];
                    return remove?line:prefix+line;
                }
                string formatted=string.Join("\n",lines.Select(FormatLine));
                box.SelectionStart=first;box.SelectionEnd=last;box.SelectedText=formatted;box.Focus();
                box.SelectionStart=first;box.SelectionEnd=first+formatted.Length;
            }
            var paragraph=new MenuFlyout();
            paragraph.Items.Add(MenuAction("Párrafo normal",()=>ParagraphStyle("")));
            paragraph.Items.Add(MenuAction("Subtítulo",()=>ParagraphStyle("## ")));
            paragraph.Items.Add(MenuAction("Subapartado",()=>ParagraphStyle("### ")));
            paragraph.Items.Add(MenuAction("Lista con viñetas",()=>ParagraphStyle("- ")));
            paragraph.Items.Add(MenuAction("Lista numerada",()=>ParagraphStyle("1. ")));
            paragraph.Items.Add(MenuAction("Cita en bloque",()=>ParagraphStyle("> ")));
            bar.Children.Add(new Button{Content="Párrafo  ▾",Flyout=paragraph,Background=Brushes.Transparent,BorderThickness=new Thickness(0)});
            var insert=new MenuFlyout();
            if(work.Type!="Presentación")insert.Items.Add(MenuAction("Salto de página",()=>Insert("\n[[Página]]\n")));
            insert.Items.Add(MenuAction("Tabla",()=>Insert("\n| Columna 1 | Columna 2 |\n| Dato | Dato |\n")));
            if(work.Type=="Presentación"){
                insert.Items.Add(MenuAction("Listas de Ficha de concepto (Referencias y Key Words)",()=>Insert("\n| Referencias | Key Words |\n| Referencia 1 | Explorar |\n| Referencia 2 | Combatir |\n| Referencia 3 | Mejorar |\n| Referencia 4 | Investigar |\n| Referencia 5 | Sobrevivir |\n| Referencia 6 | Escapar |\n")));
                insert.Items.Add(MenuAction("Tabla de HUD con imágenes",()=>Insert("\n| Imagen | Elemento | Uso y función | Estado o respuesta visual |\n| [[Imagen 1]] | Retrato | Identifica al personaje y su condición | Cambia según salud o estado |\n| [[Imagen 2]] | Indicador | Muestra el recurso principal en pantalla | Se consume gradualmente al usarse |\n")));
                insert.Items.Add(MenuAction("Tarjetas de 3 columnas",()=>Insert("\n### Pilar 1: Claridad visual | Lectura inmediata\n- Jerarquía periférica limpia en proporción 16:9.\n- Contraste alto sobre fondos complejos.\n\n### Pilar 2: Respuesta diegética | Inmersión\n- Los indicadores pertenecen al universo de la obra.\n- Transiciones visuales reactivas al estado.\n\n### Pilar 3: Escalabilidad | Sistema modular\n- Componentes reutilizables y consistentes.\n- Adaptación directa a impresión y pantalla.\n")));
                insert.Items.Add(MenuAction("Cuadrícula 2x2 (4 cuadrantes)",()=>Insert("\n### 1. Fortalezas de diseño | Núcleo visual\n- Identidad cromática definida y memorable.\n- Composición panorámica equilibrada.\n\n### 2. Oportunidades técnicas | Expansión\n- Plantillas modulares para múltiples entregas.\n- Integración con recursos de alta resolución.\n\n### 3. Riesgos controlados | Mitigación\n- Evitar saturación de texto en pantalla.\n- Mantener márgenes de seguridad 16:9.\n\n### 4. Criterios de validación | Entrega\n- Lectura en menos de 3 segundos por lámina.\n- Exportación vectorial nítida en PDF.\n")));
                insert.Items.Add(MenuAction("Tarjetas de métricas (KPIs)",()=>Insert("\n| Indicador | Valor | Detalle |\n|---|---|---|\n| Resolución base | 1080p | Lienzo panorámico 16:9 (960x540 pt) |\n| Tiempo de lectura | < 1.5 s | Identificación inmediata de estado en HUD |\n| Escenarios validados | 100 % | Estratos L01 a L03 y Cámara del Sol |\n| Variantes visuales | 17 | Plantillas profesionales de diapositiva |\n")));
                insert.Items.Add(MenuAction("Línea de tiempo / Roadmap",()=>Insert("\n### Fase 1: Concepto | Semana 1-2\n- Definición de pilares visuales y referencias.\n- Bocetos estructurales de zonas.\n\n### Fase 2: Prototipo | Semana 3-4\n- Construcción de mapas y pruebas de lectura.\n- Ajustes de paleta cromática.\n\n### Fase 3: Vertical Slice | Semana 5-6\n- Integración de HUD reactivo y sprites.\n- Balance de encuentro principal.\n\n### Fase 4: Entrega final | Semana 7-8\n- Empaquetado de dossier y presentación PDF.\n")));
                insert.Items.Add(MenuAction("Comparativa (Antes / Después)",()=>Insert("\n### Antes: Boceto preliminar | Layout de zonas\n- Geometría base codificada por bloques de color.\n- Sin iluminación atmosférica ni texturas finales.\n- Jerarquía espacial en validación.\n\n### Después: Composición final | Render iluminado\n- Iluminación cálida de faroles frente a penumbra fría.\n- Texturas completas y profundidad ambiental.\n- Lectura inmediata de rutas y puntos focales.\n")));
                insert.Items.Add(MenuAction("Matriz de riesgos y seguridad",()=>Insert("\n| Riesgo o amenaza | Nivel | Mitigación y control |\n|---|---|---|\n| Desborde visual en láminas 16:9 | Crítico | Auto-ajuste con resizebox y Viewbox de seguridad |\n| Rutas de recursos externos no válidas | Alto | Validación SHA-256 y aislamiento en directorio temporal |\n| Pérdida de legibilidad en HUD | Medio | Contraste térmico validado sobre fondos oscuros |\n| Latencia en compilación de activos | Bajo | Caché local en SQLite y empaquetado atómico |\n")));
                insert.Items.Add(MenuAction("Arquitectura en capas (Stack)",()=>Insert("\n### Capa 1: Presentación e Interfaz 16:9 | Avalonia UI & Lienzo 960x540\n- Escenario interactivo en vivo, tira de miniaturas y 24 composiciones de diapositiva.\n\n### Capa 2: Motor de Publicación y Seguridad | Nimbo.Publishing & TikZ\n- Maquetación vectorial con auto-escala anti-desborde y ejecución aislada sin shell-escape.\n\n### Capa 3: Persistencia y Activos | SQLite & Almacén SHA-256\n- Control de revisiones concurrentes, respaldo automático y verificación de recursos.\n")));
                insert.Items.Add(MenuAction("Flujo de proceso (Pipeline)",()=>Insert("\n### 1. Ingesta | Validación\n- Registro de imágenes con hash SHA-256.\n- Limpieza de rutas.\n\n### 2. Maquetación | Lienzo 16:9\n- Selección entre 24 plantillas.\n- Vista previa en vivo.\n\n### 3. Ajuste | Anti-desborde\n- Escalado automático en celdas y tarjetas.\n- Cero cortes de palabra.\n\n### 4. Entrega | PDF y Release\n- Compilación XeLaTeX segura.\n- Empaquetado final.\n")));
                insert.Items.Add(MenuAction("Checklist de validación y seguridad",()=>Insert("\n### Criterios de Geometría 16:9 | Aprobado\n- Ningún texto, tabla o imagen rebasa el marco de 960x540 puntos.\n- Auto-ajuste tipográfico activo en tablas de hasta 12 filas.\n\n### Criterios de Seguridad de Datos | Aprobado\n- Compilación LaTeX con -no-shell-escape y rutas confinadas.\n- Reemplazo atómico de archivos PDF y ZIP al exportar.\n")));
                insert.Items.Add(MenuAction("Ficha de personaje / Equipo",()=>Insert("\n### Rol y Especialidad | Liderazgo Táctico\n- Capitana del sexto escuadrón de exploración en Ciudad Miramar.\n- Especialista en cartografía de la Necrópolis y lectura de rutas.\n\n### Equipamiento y Reliquias | Inventario Activo\n- Linterna de aceite consagrado con radio de luz extendido.\n- Escudo de bronce grabado con el mapa de los estratos 78 a 85.\n")));
                insert.Items.Add(MenuAction("Embudo o pirámide estratégica",()=>Insert("\n### Nivel 1: Visión Rectora | Dirección de Arte\n- Identidad gótica y táctica inconfundible en cada pantalla.\n\n### Nivel 2: Sistemas Nucleares | HUD y Cartografía\n- Retroalimentación diegética en menos de un segundo.\n\n### Nivel 3: Ejecución Técnica | Pipeline 1080p\n- Activos verificados, paletas Adobe Color y exportación vectorial.\n\n### Nivel 4: Validación y QA | Entrega Final\n- Pruebas de estrés visual y seguridad de compilación.\n")));
            }
            else if(work.Type=="Apuntes"){
                insert.Items.Add(MenuAction("Encabezado de clase fechada (Libreta UASLP)",()=>Insert("\n## Clase: Tema de la sesión ("+DateTime.Now.ToString("dd/MM/yyyy")+")\n### Conceptos principales\n- Definición y fundamentos vistos en clase.\n- Ejemplo práctico y observaciones.\n")));
                insert.Items.Add(MenuAction("Tabla de subredes IPv4 / VLSM",()=>Insert("\n| Subred | Red / Prefijo | Primer Host | Último Host | Broadcast |\n|---|---|---|---|---|\n| LAN A | 192.168.10.0/26 | 192.168.10.1 | 192.168.10.62 | 192.168.10.63 |\n| LAN B | 192.168.10.64/27 | 192.168.10.65 | 192.168.10.94 | 192.168.10.95 |\n")));
                insert.Items.Add(MenuAction("Tabla comparativa de conceptos",()=>Insert("\n| Concepto / Obra | Principio clave | Aplicación práctica |\n|---|---|---|\n| Concepto 1 | Fundamento teórico o visual | Caso de uso en el proyecto |\n| Concepto 2 | Contraste y estructura | Validación experimental |\n")));
            }
            else if(work.Type=="Informe"){
                insert.Items.Add(MenuAction("Tabla de direccionamiento de red (P3/P4/P5)",()=>Insert("\n| Dispositivo | Interfaz | Dirección IP | Máscara de subred | Gateway predeterminado |\n|---|---|---|---|---|\n| R1 | G0/0/0 | 10.10.1.1 | 255.255.255.252 | N/A |\n| R1 | G0/0/1 | 192.168.1.1 | 255.255.255.0 | N/A |\n| S1 | VLAN 1 | 192.168.1.2 | 255.255.255.0 | 192.168.1.1 |\n| PC-A | NIC | 192.168.1.10 | 255.255.255.0 | 192.168.1.1 |\n")));
                insert.Items.Add(MenuAction("Tabla de políticas y reglas ACL",()=>Insert("\n| ACL / Regla | Origen | Destino | Protocolo / Puerto | Acción |\n|---|---|---|---|---|\n| ACL 100 · Regla 10 | 192.168.10.0/24 | 10.0.0.10 | TCP 80 / 443 (HTTP/S) | Permitir |\n| ACL 100 · Regla 20 | Cualquier red externa | Sede corporativa HQ | ICMP (Ping) | Denegar |\n")));
                insert.Items.Add(MenuAction("Tabla de especificaciones de IP / Arte",()=>Insert("\n| Atributo del proyecto | Especificación técnica y visual |\n|---|---|\n| Género y perspectiva | RPG táctico por turnos en cuadrícula isométrica |\n| Dirección de arte | Arquitectura gótica, piedra húmeda e iluminación cálida |\n")));
            }
            else if(work.Type=="CV"){
                insert.Items.Add(MenuAction("Matriz de competencias y stack técnico",()=>Insert("\n- **Lenguajes y motores:** C#, .NET, Python, C++, SQL, LaTeX, Git.\n- **Diseño y herramientas:** Avalonia UI, Tiled, Krita, Blender, Cisco Packet Tracer.\n- **Idiomas:** Español (nativo) · Inglés técnico y conversacional (B2/C1).\n")));
            }
            else if(work.Type is "Propuesta" or "Documento" or "Acta" or "Artículo"){
                insert.Items.Add(MenuAction("Tabla de requerimientos o entregables",()=>Insert("\n| Clave | Descripción del requerimiento o entregable | Prioridad | Estado |\n|---|---|---|---|\n| RF-01 | Maquetación modular por tipo de documento con vista previa | Alta | Completado |\n| RF-02 | Exportación vectorial segura a PDF mediante XeLaTeX | Alta | Validado |\n")));
            }
            insert.Items.Add(MenuAction("Paleta cromática",()=>Insert("\n[[Paleta: #E4D1A5 Luz cálida | #D99442 Ámbar | #7B8581 Piedra | #55705C Musgo | #182A27 Sombra]]\n")));
            if(!work.IsNovel && work.Type!="Presentación")insert.Items.Add(MenuAction("Bloque de firmas",()=>Insert("\n[[Firmas: Responsable 1 | Responsable 2]]\n")));
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
            var fixedTools=Column(0);fixedTools.Name="HerramientasDeEscritura";fixedTools.MaxWidth=body.MaxWidth;fixedTools.HorizontalAlignment=HorizontalAlignment.Stretch;
            fixedTools.Children.Add(barFrame);DockPanel.SetDock(fixedTools,Dock.Top);root.Children.Add(fixedTools);
            AddSearch(root,fixedTools,bar,draft);
            if(work.Type=="Presentación")
                body.Children.Add(Label(c.Layout=="Ficha de concepto"?"Listas de Referencias y Key Words (tabla de 2 columnas o bloques ### Referencias / ### Key Words)":c.Layout=="Tabla visual"?"Tabla de elementos (puedes usar [[Imagen N]] en la primera columna para incrustar cada icono)":"Contenido principal de la diapositiva (admite tablas, listas y [[Paleta: ...]])"));
            body.Children.Add(draft.View);
            var count=Label("",12);void Count(){var clean=System.Text.RegularExpressions.Regex.Replace(Illustrations.Marker().Replace(c.PlainText," "),@"\[\[(?:Página|Índice|Paleta:[^\]]+|Firmas:[^\]]+)\]\]"," ");var words=System.Text.RegularExpressions.Regex.Count(clean, @"[\p{L}\p{N}]+");count.Text=$"{words:N0} palabras   ·   {Math.Max(1,(int)Math.Ceiling(words/210d))} min de lectura";}Count();draft.Edited+=Count;body.Children.Add(count);
            if(c.Images.Count!=0 && !illustrated){
                var assets=new Expander{Header=$"Ilustraciones de la sección  ·  {c.Images.Count}",HorizontalAlignment=HorizontalAlignment.Stretch};
                void LoadAssets(){
                    if(assets.Content!=null)return;
                    var shelfContent=Column(8);
                    if(work.Type!="Presentación")Choice(shelfContent,"Disposición",ImageLayouts,c.Layout,v=>{c.Layout=v;Changed();});
                    var cards=new WrapPanel();
                    foreach(var (key,index) in c.Images.Select((key,index)=>(key,index))){
                        var card=Column(5);card.Margin=new Thickness(0,0,12,8);
                        if(Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(key)),180,90) is {} thumb)card.Children.Add(thumb);
                        card.Children.Add(Label("Imagen "+(index+1),13));
                        if(work.Type=="Presentación" && c.Layout=="Ficha de concepto"){
                            var slotBox=new ComboBox{ItemsSource=ConceptSlotOptions,SelectedItem=ConceptSlotOptions.Contains(c.ImageLayouts.GetValueOrDefault(index,"Automática"))?c.ImageLayouts.GetValueOrDefault(index,"Automática"):"Automática",Width=180};
                            slotBox.SelectionChanged+=(_,_)=>{
                                if(slotBox.SelectedItem is not string role)return;
                                if(role=="Automática")c.ImageLayouts.Remove(index);else c.ImageLayouts[index]=role;
                                Changed();refreshSlidePreview?.Invoke();
                            };
                            card.Children.Add(slotBox);
                        }
                        var cap=new TextBox{Watermark=work.Type=="Presentación"?"Pie o etiqueta Gameplay":"Pie de imagen",Text=c.ImageCaptions.GetValueOrDefault(index,""),Width=180};
                        cap.TextChanged+=(_,_)=>{var val=cap.Text??"";if(val=="")c.ImageCaptions.Remove(index);else c.ImageCaptions[index]=val;Changed();refreshSlidePreview?.Invoke();};
                        card.Children.Add(cap);
                        var rowActions=Row();
                        if(work.Type=="Presentación")rowActions.Children.Add(QuietAction("En celda",()=>Insert(Illustrations.At(index))));
                        rowActions.Children.Add(QuietAction("Quitar",()=>{Illustrations.Remove(c,index);Changed();Editor();}));
                        card.Children.Add(rowActions);cards.Children.Add(card);
                    }
                    shelfContent.Children.Add(cards);assets.Content=shelfContent;
                }
                assets.Expanding+=(_,_)=>LoadAssets();
                if(work.Type=="Presentación"){LoadAssets();assets.IsExpanded=true;}
                body.Children.Add(assets);
            }
            if(work.Type=="CV")ResumeEntries(body,c,refreshSlidePreview);
            if (work.Type == "Apuntes") Field(body, "Síntesis", c.Summary, v => { c.Summary = v; refreshSlidePreview?.Invoke(); }, true);
        }
        var sheet=new Border{Child=body,Padding=new Thickness(36,30,36,24),MaxWidth=body.MaxWidth+72,HorizontalAlignment=HorizontalAlignment.Stretch};
        root.Children.Add(new ScrollViewer{Content=sheet}); page.Content = root;
    }
    void Details()=>Details(false);
    void Details(bool printing)
    {
        Navigate("Ficha");var work = current!; var body = Column();body.MaxWidth=ReadPref("ancho","Medio") switch {"Amplio"=>1120,"Estrecho"=>680,_=>860};body.HorizontalAlignment=HorizontalAlignment.Stretch; body.Children.Add(Kicker(work.Type));body.Children.Add(Display("Ficha de la obra",36));
        var jacket=new DockPanel();var coverOptions=Column(8);
        var proof=new ContentControl{Width=160,Height=222,Margin=new Thickness(0,0,22,0)};
        void RefreshJacket(){
            var entry=new Entry(work.Id,work.Title,work.Type,0,work.CoverImage,work.CoverColor,work.Author,work.Description,work.CollectionId,work.CoverLettering);
            proof.Content=Miniature(entry,()=>{if(work.IsNovel)InspectBook(entry);else ReadSection(chapter);});
        }
        RefreshJacket();DockPanel.SetDock(proof,Dock.Left);jacket.Children.Add(proof);
        coverOptions.VerticalAlignment=VerticalAlignment.Center;coverOptions.Children.Add(Kicker(work.IsNovel?"Encuadernación":"Miniatura"));
        var inks=new WrapPanel();
        foreach(var hex in palette.Swatches.Concat(new[]{"#3C425E","#886240","#8C6273","#1E6667","#E4DABC","#392B39","#88403F","#FAF8F0"}).Prepend(work.CoverColor).Where(s=>Color.TryParse(s,out _)).Distinct(StringComparer.OrdinalIgnoreCase)){
            var swatch=new Button{Width=30,Height=30,MinWidth=0,Padding=new Thickness(0),Margin=new Thickness(0,0,5,4),Background=Brush.Parse(hex),CornerRadius=new CornerRadius(15),
                BorderBrush=Foreground,BorderThickness=new Thickness(hex==work.CoverColor?2:0)};
            ToolTip.SetTip(swatch,"Pasta "+hex);Avalonia.Automation.AutomationProperties.SetName(swatch,"Pasta "+hex);
            swatch.Click+=(_,_)=>{work.CoverColor=hex;Changed();RefreshJacket();foreach(var b in inks.Children.OfType<Button>())b.BorderThickness=new Thickness(b==swatch?2:0);};inks.Children.Add(swatch);
        }
        coverOptions.Children.Add(inks);
        Choice(coverOptions,"Letras de la encuadernación",BookCover.LetteringStyles,work.CoverLettering,v=>{work.CoverLettering=v;Changed();RefreshJacket();});
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
        CoverSettings(print,work);
        SourceSettings(print,work);
        if (work.Editions.Count > 0)
        {
            body.Children.Add(Label("Edición anterior", 21));
            var query=new TextBox{Watermark="Buscar un PDF o proyecto por nombre"};
            var files=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};
            void FindFiles(){files.ItemsSource=work.Editions.Where(e=>e.Name.Contains(query.Text??"",StringComparison.CurrentCultureIgnoreCase)).Select(e=>e.Name).Distinct().ToArray();files.SelectedIndex=0;}
            query.TextChanged+=(_,_)=>FindFiles();FindFiles();body.Children.Add(query);body.Children.Add(files);
            body.Children.Add(Action("Guardar archivo seleccionado…", async () => await Guard(async () =>
                {
                    var edition=work.Editions.FirstOrDefault(e=>e.Name==files.SelectedItem as string);if(edition==null)return;
                    var destination = await StorageProvider.SaveFilePickerAsync(new() { Title = "Guardar edición anterior", SuggestedFileName = Path.GetFileName(edition.Name) });
                    if (destination?.TryGetLocalPath() is not string path) return;
                    var source = Path.Combine(shelf.Media, Path.GetFileName(edition.Key));
                    var temp = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.Copy(source, temp); File.Move(temp, path, true); } finally { File.Delete(temp); }
                    status.Text = "Edición anterior guardada";
                })));
        }
        var tabs=new TabControl{ItemsSource=new[]{new TabItem{Header="Datos editoriales",Content=form},new TabItem{Header="Impresión",Content=print}},SelectedIndex=printing?1:0};
        coverPage.Children.Add(tabs);page.Content=new ScrollViewer{Content=SheetOf(coverPage,940)};
    }
}










