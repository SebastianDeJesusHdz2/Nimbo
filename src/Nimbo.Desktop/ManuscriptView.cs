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
        var left=Column(10);left.Width=214;
        if(work.IsNovel || work.CoverImage!="")left.Children.Add(WorkArt(work,360,110));
        else left.Children.Add(new Viewbox{Height=126,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,Child=Miniature(Entry.FromWork(work),()=>Details())});
        left.Children.Add(Kicker(work.IsNovel ? work.Type : $"{work.Type}  ·  {(string.IsNullOrWhiteSpace(work.Template) ? "Clásica" : work.Template)}"));
        left.Children.Add(Display(work.Title,24));left.Children.Add(Rule(Edge));
        left.Children.Add(Label("Índice del manuscrito",14));
        var outline=Column(3);var titles=new Dictionary<Chapter,TextBlock>();
        int outlineIdx=0;
        foreach(var entry in WorldModel.Ordered(work)){
            outlineIdx++;
            var label=Label(entry.Title,13.5);titles[entry]=label;
            Control itemContent=label;
            if(work.Type=="Presentación"){
                var col=Column(1);
                var sub=new TextBlock{Text=$"{outlineIdx:D2} · {(string.IsNullOrWhiteSpace(entry.Layout)?"Texto e imagen":entry.Layout)}",FontSize=10.5,Foreground=Quiet};
                col.Children.Add(label);col.Children.Add(sub);
                itemContent=col;
            }else if(work.Type=="CV" && entry.Entries.Count>0){
                var row=new DockPanel();
                var badge=new TextBlock{Text=entry.Entries.Count.ToString(),FontSize=11,Foreground=Quiet,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center};
                DockPanel.SetDock(badge,Dock.Right);
                row.Children.Add(badge);row.Children.Add(label);
                itemContent=row;
            }
            var item=QuietAction("",()=>{if(entry==chapter)return;chapter=entry;RememberSection(work,entry);Editor();});
            var indent=entry.ParentId==""?0:entry.Level=="Subchapter"?20:10;
            item.Padding=new Thickness(10+indent,7,8,7);item.HorizontalAlignment=HorizontalAlignment.Stretch;
            item.HorizontalContentAlignment=HorizontalAlignment.Left;item.Content=itemContent;
            if(chapter==entry){item.Background=Sheet;item.BorderThickness=new Thickness(2,0,0,0);item.BorderBrush=Accent;item.Foreground=Accent;}
            outline.Children.Add(item);
        }
        left.Children.Add(outline);
        void Add(string level, string title, string parent)
        {
            chapter = new() { Title = title, Level = level, ParentId = parent, Order = work.Chapters.Count };
            if (work.Type == "Presentación") chapter.Layout = "Texto e imagen";
            work.Chapters.Add(chapter); RememberSection(work,chapter); Changed(); Editor();
        }
        var section = work.IsNovel ? "Capítulo" : Format.All.FirstOrDefault(f => f.Name == work.Type)?.Section ?? "Sección";
        var structure=new MenuFlyout();
        Chapter? FindChapter(string? id)=>work.Chapters.FirstOrDefault(n=>n.Id==id);
        var parentChapter=chapter?.Level=="Subchapter"?FindChapter(chapter.ParentId):chapter;
        var arc=parentChapter?.Level=="Arc"?parentChapter:FindChapter(parentChapter?.ParentId);
        var quickAddBtn=Action("+ Añadir "+section.ToLowerInvariant(),()=>Add("Chapter",section+" "+(work.Chapters.Count+1),arc?.Level=="Arc"?arc.Id:""));
        quickAddBtn.HorizontalAlignment=HorizontalAlignment.Stretch;
        left.Children.Add(quickAddBtn);
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
            var activeTpl=fmt!=null?EditorialTemplate.Resolve(fmt,work.Template):null;
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
            Action? renderPreviewNow=null;
            var previewTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
            previewTimer.Tick+=(_,_)=>{previewTimer.Stop();renderPreviewNow?.Invoke();};
            Action refreshSlidePreview=()=>{if(isAutomatedVerification)renderPreviewNow?.Invoke();else{previewTimer.Stop();previewTimer.Start();}};
            heading.TextChanged+=(_,_)=>{if(heading.Text==c.Title)return;c.Title=heading.Text??"";if(titles.TryGetValue(c,out var tb))tb.Text=c.Title;Changed();refreshSlidePreview();};body.Children.Add(heading);
            if (!work.IsNovel && work.Type!="Presentación") Field(body,EditorialTemplate.DetailLabel(work.Type),c.Detail,v=>{c.Detail=v;refreshSlidePreview();});
            if (work.Type == "Presentación")
            {
                var previewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                renderPreviewNow = () =>
                {
                    previewHost.Content = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        MaxHeight = 250,
                        Child = BuildSlideCanvas(work, c, 480)
                    };
                };
                renderPreviewNow();
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
                var detailBox = Field(rightMeta, detailLabel, c.Detail, v => { c.Detail = v; refreshSlidePreview(); });
                Grid.SetColumn(rightMeta, 1);
                metaGrid.Children.Add(rightMeta);
                body.Children.Add(metaGrid);

                var summaryLabel = c.Layout == "Ficha de concepto" ? "Concepto principal (esquina superior derecha)" : c.Layout == "Portada" ? "Actividad o subtítulo de portada (ej. ACT 2)" : "Texto secundario, resumen o leyenda lateral";
                Field(body, summaryLabel, c.Summary, v => { c.Summary = v; refreshSlidePreview(); }, c.Layout != "Portada");
            }
            else if (work.IsNovel)
            {
                var novelPreviewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                int novelPreviewMode = 0; // 0 = Hoja actual, 1 = Portada, 2 = Contraportada
                var btnPage = QuietAction("Hoja actual", () => { novelPreviewMode = 0; renderPreviewNow?.Invoke(); });
                var btnFront = QuietAction("Portada", () => { novelPreviewMode = 1; renderPreviewNow?.Invoke(); });
                var btnBack = QuietAction("Contraportada", () => { novelPreviewMode = 2; renderPreviewNow?.Invoke(); });
                renderPreviewNow = () =>
                {
                    btnPage.Foreground = novelPreviewMode == 0 ? Accent : Quiet;
                    btnFront.Foreground = novelPreviewMode == 1 ? Accent : Quiet;
                    btnBack.Foreground = novelPreviewMode == 2 ? Accent : Quiet;
                    Control previewSheet = novelPreviewMode switch
                    {
                        1 => BuildNovelCoverCanvas(work, false, 420),
                        2 => BuildNovelCoverCanvas(work, true, 420),
                        _ => BuildDocumentSectionCanvas(work, c, 420)
                    };
                    novelPreviewHost.Content = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        MaxHeight = 240,
                        Child = previewSheet
                    };
                };
                renderPreviewNow();
                var novelHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var openReading = QuietAction("Abrir lectura del libro →", () => ReadSection(novelPreviewMode == 1 ? null : c));
                DockPanel.SetDock(openReading, Dock.Right);
                novelHeader.Children.Add(openReading);
                var modeTabs = Row();
                modeTabs.Margin = new Thickness(0);
                modeTabs.Children.Add(btnPage);
                modeTabs.Children.Add(btnFront);
                modeTabs.Children.Add(btnBack);
                DockPanel.SetDock(modeTabs, Dock.Right);
                novelHeader.Children.Add(modeTabs);
                novelHeader.Children.Add(Kicker("Vista previa de novela"));

                var coverBar = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                var coverCombo = new ComboBox
                {
                    ItemsSource = EditorialTemplate.NovelCoverStyles,
                    SelectedItem = EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0],
                    MinWidth = 190,
                    Margin = new Thickness(0, 0, 8, 4)
                };
                coverCombo.SelectionChanged += (_, _) =>
                {
                    if (coverCombo.SelectedItem is string cs && cs != work.CoverStyle)
                    {
                        work.CoverStyle = cs;
                        novelPreviewMode = 1;
                        Changed();
                        renderPreviewNow?.Invoke();
                    }
                };
                coverBar.Children.Add(coverCombo);
                coverBar.Children.Add(QuietAction("Imagen de portada…", async () => await Guard(async () =>
                {
                    var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
                    if (path == null) return;
                    work.CoverImage = shelf.AddImage(path);
                    novelPreviewMode = 1;
                    Changed();
                    renderPreviewNow?.Invoke();
                })));
                coverBar.Children.Add(QuietAction("Imagen de contraportada…", async () => await Guard(async () =>
                {
                    var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Contraportada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
                    if (path == null) return;
                    work.BackImage = shelf.AddImage(path);
                    novelPreviewMode = 2;
                    Changed();
                    renderPreviewNow?.Invoke();
                })));
                var novelCard = Column(8);
                novelCard.Children.Add(novelHeader);
                novelCard.Children.Add(novelPreviewHost);
                var coverExp = new Expander
                {
                    Header = $"Portada y contraportada a hoja completa ({(EditorialTemplate.NovelCoverStyles.Contains(work.CoverStyle) ? work.CoverStyle : EditorialTemplate.NovelCoverStyles[0])})",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = coverBar
                };
                novelCard.Children.Add(coverExp);
                body.Children.Add(Card(novelCard));
            }
            else
            {
                var format = Format.All.FirstOrDefault(f => f.Name == work.Type);
                var activeTpl = EditorialTemplate.Resolve(format, work.Template);
                bool hasDedicatedCover = activeTpl.Style is "Libreta" or "Reporte";
                bool hasDedicatedToc = hasDedicatedCover && work.Publication.Contents;
                int docPreviewMode = 0; // 0 = Hoja de la sección, 1 = Portada / Encabezado, 2 = Índice / Contenido

                var btnSecView = QuietAction("Hoja de la sección", () => { docPreviewMode = 0; renderPreviewNow?.Invoke(); });
                var btnCoverView = QuietAction(hasDedicatedCover ? "Portada" : "Primera hoja", () => { docPreviewMode = 1; renderPreviewNow?.Invoke(); });
                var btnTocView = QuietAction(activeTpl.Style == "Libreta" ? "Índice" : "Contenido", () => { docPreviewMode = 2; renderPreviewNow?.Invoke(); });

                var docPreviewHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Stretch };
                renderPreviewNow = () =>
                {
                    btnSecView.Foreground = docPreviewMode == 0 ? Accent : Quiet;
                    btnCoverView.Foreground = docPreviewMode == 1 ? Accent : Quiet;
                    btnTocView.Foreground = docPreviewMode == 2 ? Accent : Quiet;

                    var pages = BuildDocumentPages(work, 400);
                    if (pages.Count == 0) return;

                    if (docPreviewMode == 1)
                    {
                        docPreviewHost.Content = new Viewbox
                        {
                            Stretch = Stretch.Uniform,
                            MaxHeight = 238,
                            Child = pages[0].BuildSheet()
                        };
                        return;
                    }
                    if (docPreviewMode == 2 && pages.Count > 1)
                    {
                        var tocPage = pages.FirstOrDefault(p => p.Label is "Índice" or "Contenido") ?? pages[1];
                        docPreviewHost.Content = new Viewbox
                        {
                            Stretch = Stretch.Uniform,
                            MaxHeight = 238,
                            Child = tocPage.BuildSheet()
                        };
                        return;
                    }

                    var sectionPages = pages.Where(p => p.Section == c).ToList();
                    if (sectionPages.Count == 0) sectionPages.Add(pages[0]);
                    if (sectionPages.Count >= 2)
                    {
                        var twoGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
                        twoGrid.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 235, Child = sectionPages[0].BuildSheet() });
                        var secondVb = new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 235, Child = sectionPages[1].BuildSheet() };
                        Grid.SetColumn(secondVb, 1);
                        twoGrid.Children.Add(secondVb);
                        docPreviewHost.Content = twoGrid;
                    }
                    else
                    {
                        docPreviewHost.Content = new Viewbox
                        {
                            Stretch = Stretch.Uniform,
                            MaxHeight = 235,
                            Child = sectionPages[0].BuildSheet()
                        };
                    }
                };
                renderPreviewNow();
                var docHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var openDocReading = QuietAction("Abrir modo Lectura →", () => ReadSection(docPreviewMode == 1 && hasDedicatedCover ? null : c));
                DockPanel.SetDock(openDocReading, Dock.Right);
                docHeader.Children.Add(openDocReading);

                var previewTabs = Row();
                previewTabs.Margin = new Thickness(0);
                previewTabs.Children.Add(btnSecView);
                previewTabs.Children.Add(btnCoverView);
                if (hasDedicatedToc) previewTabs.Children.Add(btnTocView);
                DockPanel.SetDock(previewTabs, Dock.Right);
                docHeader.Children.Add(previewTabs);

                docHeader.Children.Add(Kicker($"Vista en vivo  ·  {work.Type}  ·  {activeTpl.Name}"));

                var templateNames = format != null ? EditorialTemplate.For(format).Select(t => t.Name).ToArray() : [work.Template];
                var docCard = Column(8);
                docCard.Children.Add(docHeader);

                if (templateNames.Length > 1)
                {
                    var fmtRow = Column(4);
                    fmtRow.Children.Add(Label($"Plantilla y formato de {work.Type.ToLowerInvariant()}"));
                    var tplCombo = new ComboBox
                    {
                        ItemsSource = templateNames,
                        SelectedItem = templateNames.Contains(work.Template) ? work.Template : activeTpl.Name,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    tplCombo.SelectionChanged += (_, _) =>
                    {
                        if (tplCombo.SelectedItem is string chosen && chosen != work.Template)
                        {
                            work.Template = chosen;
                            if (format != null)
                            {
                                var tplObj = EditorialTemplate.Resolve(format, chosen);
                                bool allBlank = work.Chapters.All(ch => string.IsNullOrWhiteSpace(ch.PlainText) && ch.Entries.Count == 0 && ch.Images.Count == 0 && string.IsNullOrWhiteSpace(ch.Detail) && string.IsNullOrWhiteSpace(ch.Summary));
                                if (allBlank)
                                {
                                    work.Chapters = tplObj.Sections.Select((s, idx) => new Chapter { Title = s, Order = idx }).ToList();
                                    chapter = work.Chapters.FirstOrDefault();
                                }
                                else
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
                    fmtRow.Children.Add(Label(EditorialTemplate.FormatGuidance(work.Type, activeTpl.Name), 12.5));
                    docCard.Children.Add(fmtRow);
                }

                docCard.Children.Add(docPreviewHost);

                if (activeTpl.Style == "Reporte")
                {
                    var repToolbar = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
                    var roleNote = c.Title.Equals("Resumen", StringComparison.OrdinalIgnoreCase)
                        ? "Sección preliminar sin numerar · Seguida automáticamente por el índice de Contenido."
                        : (c.Title.StartsWith("Conclusi", StringComparison.OrdinalIgnoreCase) || c.Title.StartsWith("Bibliograf", StringComparison.OrdinalIgnoreCase))
                            ? "Sección final sin numerar · Incluida en el índice de Contenido."
                            : "Sección numerada del cuerpo del reporte.";
                    repToolbar.Children.Add(new TextBlock { Text = roleNote, FontSize = 12, Foreground = Quiet, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
                    repToolbar.Children.Add(QuietAction("+ Sección de desarrollo", () =>
                    {
                        var orderedCh = WorldModel.Ordered(work).ToList();
                        int insertIdx = orderedCh.FindIndex(ch => ch.Title.StartsWith("Conclusi", StringComparison.OrdinalIgnoreCase) || ch.Title.StartsWith("Bibliograf", StringComparison.OrdinalIgnoreCase));
                        int bodyNum = orderedCh.Count(ch => !ch.Title.Equals("Resumen", StringComparison.OrdinalIgnoreCase) && !ch.Title.StartsWith("Conclusi", StringComparison.OrdinalIgnoreCase) && !ch.Title.StartsWith("Bibliograf", StringComparison.OrdinalIgnoreCase)) + 1;
                        var newSec = new Chapter { Title = "Desarrollo " + bodyNum, Level = "Chapter" };
                        if (insertIdx >= 0) orderedCh.Insert(insertIdx, newSec);
                        else orderedCh.Add(newSec);
                        for (int n = 0; n < orderedCh.Count; n++) orderedCh[n].Order = n;
                        work.Chapters = orderedCh;
                        chapter = newSec;
                        RememberSection(work, newSec);
                        Changed();
                        Editor();
                    }));
                    docCard.Children.Add(repToolbar);
                }

                var frontKeys = work.Type == "CV"
                    ? ["Nombre profesional", "Profesión", "Correo", "Teléfono", "Ciudad", "Portafolio", "Perfil profesional en línea"]
                    : EditorialTemplate.AllFactKeys(work);
                if (frontKeys.Length > 0)
                {
                    bool expandByDefault = work.Type is "Carta" or "Acta" && work.Chapters.IndexOf(c) == 0 && frontKeys.All(k => string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k)));
                    var metaExpander = new Expander
                    {
                        Header = work.Type == "CV"
                            ? "Datos profesionales del encabezado"
                            : hasDedicatedCover
                                ? $"Portada institucional de {activeTpl.Name} ({frontKeys.Length} campos)"
                                : $"Datos de encabezado de {work.Type.ToLowerInvariant()} ({frontKeys.Length} campos)",
                        IsExpanded = expandByDefault,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var expanderStack = Column(8);
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
                            if (hasDedicatedCover) docPreviewMode = 1;
                            refreshSlidePreview();
                        });
                        Grid.SetRow(cellCol, idx / 2);
                        Grid.SetColumn(cellCol, idx % 2);
                        fieldsGrid.Children.Add(cellCol);
                    }
                    expanderStack.Children.Add(fieldsGrid);
                    if (hasDedicatedCover)
                    {
                        var coverImgBar = Row();
                        coverImgBar.Children.Add(QuietAction("Imagen de portada…", async () => await Guard(async () =>
                        {
                            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Imagen de portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
                            if (path == null) return;
                            work.CoverImage = shelf.AddImage(path);
                            docPreviewMode = 1;
                            Changed();
                            renderPreviewNow?.Invoke();
                        })));
                        if (work.CoverImage != "")
                        {
                            coverImgBar.Children.Add(QuietAction("Quitar imagen de portada", () =>
                            {
                                work.CoverImage = "";
                                docPreviewMode = 1;
                                Changed();
                                renderPreviewNow?.Invoke();
                            }));
                        }
                        expanderStack.Children.Add(coverImgBar);
                    }
                    metaExpander.Content = expanderStack;
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
            insert.Items.Add(MenuAction("Paleta cromática",()=>Insert("\n[[Paleta: #E4D1A5 Luz cálida | #D99442 Ámbar | #7B8581 Piedra | #55705C Musgo | #182A27 Sombra]]\n")));
            if(!work.IsNovel && work.Type!="Presentación")insert.Items.Add(MenuAction("Bloque de firmas",()=>Insert("\n[[Firmas: Responsable 1 | Responsable 2]]\n")));
            foreach(var (snipLabel,snipContent) in EditorialTemplate.QuickSnippets(work.Type)){
                var textToInsert=snipContent;
                insert.Items.Add(MenuAction(snipLabel,()=>Insert(textToInsert)));
            }
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
            if(work.Type=="CV" && draft.Blocks.Count==1)draft.Blocks[0].MinHeight=115;
            if(work.Type=="CV")ResumeEntries(body,c,refreshSlidePreview);
            if(work.Type=="Presentación")
                body.Children.Add(Label(c.Layout=="Ficha de concepto"?"Listas de Referencias y Key Words (tabla de 2 columnas o bloques ### Referencias / ### Key Words)":c.Layout=="Tabla visual"?"Tabla de elementos (puedes usar [[Imagen N]] en la primera columna para incrustar cada icono)":"Contenido principal de la diapositiva (admite tablas, listas y [[Paleta: ...]])"));
            else if(work.Type=="CV")
                body.Children.Add(Label("Texto libre o introducción de la sección (opcional si usas entradas estructuradas arriba)",13));
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
            if (work.Type == "Apuntes") Field(body, "Síntesis", c.Summary, v => { c.Summary = v; refreshSlidePreview?.Invoke(); }, true);
            else if (work.Type is "Informe" or "Propuesta" or "Artículo" or "Acta" or "Documento")
                Field(body, "Recuadro de síntesis o nota destacada al cierre de la sección (opcional)", c.Summary, v => { c.Summary = v; refreshSlidePreview?.Invoke(); }, true);
        }
        var sheet=new Border{Child=body,Padding=new Thickness(36,30,36,24),MaxWidth=body.MaxWidth+72,HorizontalAlignment=HorizontalAlignment.Stretch};
        root.Children.Add(new ScrollViewer{Content=sheet}); page.Content = root;
    }
    void Details()=>Details(false);
    void Details(bool printing)
    {
        Navigate("Ficha");var work = current!; var body = Column();body.MaxWidth=ReadPref("ancho","Medio") switch {"Amplio"=>1120,"Estrecho"=>680,_=>860};body.HorizontalAlignment=HorizontalAlignment.Stretch;
        body.Children.Add(Kicker(work.IsNovel ? work.Type : $"{work.Type}  ·  {(string.IsNullOrWhiteSpace(work.Template) ? "Clásica" : work.Template)}"));
        body.Children.Add(Display("Ficha y personalización",36));
        var jacket=new DockPanel();var coverOptions=Column(8);
        var leftStage=Column(6);leftStage.Margin=new Thickness(0,0,24,0);
        var proof=new ContentControl{Width=164,Height=224};
        bool showingBackFace=false;
        void RefreshJacket(){
            var entry=Entry.FromWork(work);
            var mini=Miniature(entry,()=>{if(work.IsNovel)InspectBook(entry);else ReadSection(chapter);});
            if(mini is BookCover bc)bc.ShowJacket(showingBackFace);
            else if(mini is TurningSheet ts)ts.ShowFace(showingBackFace);
            proof.Content=mini;
        }
        RefreshJacket();
        leftStage.Children.Add(proof);
        var faceRow=Row();faceRow.HorizontalAlignment=HorizontalAlignment.Center;faceRow.Margin=new Thickness(0);
        faceRow.Children.Add(QuietAction("Anverso",()=>{showingBackFace=false;RefreshJacket();}));
        faceRow.Children.Add(QuietAction("Reverso",()=>{showingBackFace=true;RefreshJacket();}));
        leftStage.Children.Add(faceRow);
        DockPanel.SetDock(leftStage,Dock.Left);jacket.Children.Add(leftStage);

        coverOptions.VerticalAlignment=VerticalAlignment.Center;
        coverOptions.Children.Add(Kicker(work.IsNovel?"Encuadernación 3D y portada":"Personalización de minidocumento 3D y color"));
        var inks=new WrapPanel();
        var presetColors=palette.Swatches.Concat(new[]{"#004785","#1B5272","#364A8A","#7A3E65","#376B48","#9B342E","#4A3C6E","#475E4B","#1E5B88","#3C425E","#886240","#8C6273","#1E6667","#E4DABC","#392B39","#88403F","#FAF8F0"}).Prepend(work.CoverColor).Where(s=>Color.TryParse(s,out _)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach(var hex in presetColors){
            var swatch=new Button{Width=28,Height=28,MinWidth=0,Padding=new Thickness(0),Margin=new Thickness(0,0,5,4),Background=Brush.Parse(hex),CornerRadius=new CornerRadius(14),
                BorderBrush=Foreground,BorderThickness=new Thickness(string.Equals(hex,work.CoverColor,StringComparison.OrdinalIgnoreCase)?2:0)};
            ToolTip.SetTip(swatch,"Color "+hex);Avalonia.Automation.AutomationProperties.SetName(swatch,"Pasta "+hex);
            swatch.Click+=(_,_)=>{work.CoverColor=hex;Changed();RefreshJacket();foreach(var b in inks.Children.OfType<Button>())b.BorderThickness=new Thickness(b==swatch?2:0);};inks.Children.Add(swatch);
        }
        coverOptions.Children.Add(inks);

        var customColorRow=Row();
        var hexInput=new TextBox{Text=work.CoverColor,Watermark="#HEX personalizado",Width=130};
        customColorRow.Children.Add(hexInput);
        customColorRow.Children.Add(QuietAction("Aplicar color HEX",()=>{
            var candidate=(hexInput.Text??"").Trim();
            if(!candidate.StartsWith('#'))candidate="#"+candidate;
            if(Color.TryParse(candidate,out _)){work.CoverColor=candidate.ToUpperInvariant();hexInput.Text=work.CoverColor;Changed();RefreshJacket();}
        }));
        coverOptions.Children.Add(customColorRow);

        if(work.IsNovel){
            Choice(coverOptions,"Letras de la encuadernación",BookCover.LetteringStyles,work.CoverLettering,v=>{work.CoverLettering=v;Changed();RefreshJacket();});
        }else{
            var miniStyles=EditorialTemplate.DocumentMiniatureStyles;
            Choice(coverOptions,"Estilo y acabado del minidocumento",miniStyles,miniStyles.Contains(work.CoverLettering)?work.CoverLettering:miniStyles[0],v=>{work.CoverLettering=v;Changed();RefreshJacket();});
        }
        var imgActions=Row();
        imgActions.Children.Add(Action(work.IsNovel?"Elegir portada…":"Imagen o logotipo de portada…", async () => await Guard(async () =>
        {
            var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Portada PNG o JPEG" })).FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return; work.CoverImage = shelf.AddImage(path); Changed(); Details(printing);
        })));
        if(work.CoverImage!="")imgActions.Children.Add(QuietAction("Quitar imagen de portada",()=>{work.CoverImage="";Changed();Details(printing);}));
        coverOptions.Children.Add(imgActions);
        jacket.Children.Add(coverOptions);body.Children.Add(Card(jacket));
        var coverPage=body;var form=Column(8);var print=Column(8);body=form;
        var titleBox=Field(body,"Título",work.Title,v=>{work.Title=v;RefreshJacket();});
        var authorBox=Field(body,"Autor",work.Author,v=>{work.Author=v;RefreshJacket();});
        var subBox=Field(body, "Subtítulo", work.Subtitle, v => { work.Subtitle = v; RefreshJacket(); });
        Field(body, "Colección", work.CollectionId, v => work.CollectionId = v);
        var blurb=Field(body,work.IsNovel?"Sinopsis de contraportada":"Resumen o síntesis para el reverso del minidocumento",work.Description,v=>{work.Description=v;RefreshJacket();},true);
        foreach(var field in new[]{titleBox,authorBox,subBox,blurb})field.LostFocus+=(_,_)=>RefreshJacket();
        if(work.Type=="CV")ResumeProfile(body,work);
        var facts=EditorialTemplate.AllFactKeys(work);
        if(facts.Length>0 || !work.IsNovel){
            var data=Column(6);data.Children.Add(Display("Datos de "+work.Type.ToLowerInvariant(),22));
            foreach(var key in facts)Field(data,key,work.DocumentFacts.GetValueOrDefault(key,""),v=>{work.DocumentFacts[key]=v;RefreshJacket();});
            if(!work.IsNovel && work.Type!="CV"){
                var extraFactRow=new WrapPanel{Margin=new Thickness(0,6,0,0)};
                var customKey=new TextBox{Watermark="Nuevo campo (ej. Expediente, Semestre)",MinWidth=210,Margin=new Thickness(0,0,8,4)};
                var customVal=new TextBox{Watermark="Valor del campo",MinWidth=210,Margin=new Thickness(0,0,8,4)};
                extraFactRow.Children.Add(customKey);extraFactRow.Children.Add(customVal);
                extraFactRow.Children.Add(QuietAction("+ Añadir dato",()=>{
                    var k=(customKey.Text??"").Trim();if(k=="")return;
                    work.DocumentFacts[k]=(customVal.Text??"").Trim();
                    Changed();Details(printing);
                }));
                data.Children.Add(extraFactRow);
            }
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










