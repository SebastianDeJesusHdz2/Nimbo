using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nimbo.Core;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    string archiveWork="",archiveKind="",archiveQuery="";
    bool archivePinned;
    int archivePage;
    int archiveViewMode; // 0 = Tarjetas, 1 = Por clase, 2 = Cuadernillo impreso
    const int PageLength=24;

    Button NewEntry(Work work,Node? parent)
    {
        var choices=new MenuFlyout();
        foreach(var kind in Dossiers.Kinds)
        {
            var k=kind;
            choices.Items.Add(MenuAction(k,()=>{
                var next=new Node{NodeKind=k,Name=k+" sin nombre",ParentId=parent?.Id??""};
                work.Glossary.Add(next);Changed();Glossary(next,0);
            }));
        }
        var button=new Button{Content=parent==null?"+ Nueva ficha":"+ Ficha hija",Flyout=choices};Emphasize(button);return button;
    }

    internal Border BuildGlossaryCoverSheet(Work work)
    {
        var (mmW, mmH) = Dossiers.Size(work.GlossaryPaper);
        double sheetW = 340;
        double sheetH = Math.Round(sheetW * mmH / (double)mmW);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var ink = Brush.Parse("#1D2227");
        var muted = Brush.Parse("#545E68");

        var col = Column(10);
        col.Margin = new Thickness(28, 32);
        col.Children.Add(new Border { Height = 2, Background = accentBrush });
        col.Children.Add(new Border { Height = 0.8, Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.45), Margin = new Thickness(0, -7, 0, 12) });
        col.Children.Add(new TextBlock
        {
            Text = "COMPENDIO Y GLOSARIO DEL MUNDO",
            FontSize = 10.5,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1.2,
            Foreground = accentBrush
        });
        col.Children.Add(new TextBlock
        {
            Text = "Glosario de " + work.Title,
            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
            FontSize = 23,
            FontWeight = FontWeight.Bold,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap
        });
        col.Children.Add(new Border { Width = 72, Height = 1.6, Background = accentBrush, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 10) });

        col.Children.Add(new TextBlock
        {
            Text = "Índice de fichas (" + work.Glossary.Count + ")",
            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = ink,
            Margin = new Thickness(0, 4, 0, 4)
        });
        int idx = 0;
        foreach (var n in work.Glossary.OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase).Take(8))
        {
            idx++;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(new TextBlock { Text = $"{idx}. {n.Name}", FontSize = 11.5, Foreground = ink, TextTrimming = TextTrimming.CharacterEllipsis });
            var kTb = new TextBlock { Text = n.NodeKind, FontSize = 10.5, Foreground = muted, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(kTb, 1);
            row.Children.Add(kTb);
            col.Children.Add(row);
        }
        if (work.Glossary.Count > 8)
            col.Children.Add(new TextBlock { Text = $"… y {work.Glossary.Count - 8} fichas más", FontSize = 11, FontStyle = FontStyle.Italic, Foreground = muted });

        col.Children.Add(new Border { Height = 1, Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.35), Margin = new Thickness(0, 14, 0, 2) });
        col.Children.Add(new TextBlock
        {
            Text = (work.Author != "" ? work.Author + "  ·  " : "") + $"Formato {work.GlossaryPaper} ({mmW} × {mmH} mm)",
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            Foreground = muted
        });

        return new Border
        {
            Width = sheetW,
            MinHeight = sheetH,
            Background = Brush.Parse("#FCFBF7"),
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            BoxShadow = BoxShadows.Parse("0 10 24 #24000000"),
            Child = col,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    internal Border BuildGlossaryCardSheet(Work work, Node node, int decodeWidth = 380)
    {
        var (mmW, mmH) = Dossiers.Size(work.GlossaryPaper);
        double sheetW = 340;
        double sheetH = Math.Round(sheetW * mmH / (double)mmW);
        var accentHex = ResolveFormatAccentHex(work);
        var accentBrush = Brush.Parse(accentHex);
        var ink = Brush.Parse("#1D2227");
        var muted = Brush.Parse("#545E68");

        var col = Column(8);
        col.Margin = new Thickness(24, 24);

        var headBanner = Column(2);
        headBanner.Children.Add(new TextBlock
        {
            Text = node.NodeKind.ToUpperInvariant() + (node.Pinned ? "  ·  ★ DESTACADA" : ""),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1.1,
            Foreground = accentBrush
        });
        headBanner.Children.Add(new TextBlock
        {
            Text = node.Name,
            FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
            FontSize = 21,
            FontWeight = FontWeight.Bold,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap
        });
        col.Children.Add(new Border
        {
            Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.14),
            Padding = new Thickness(10, 7),
            CornerRadius = new CornerRadius(3),
            Child = headBanner
        });
        col.Children.Add(new Border { Height = 1.4, Background = accentBrush, Margin = new Thickness(0, -2, 0, 4) });

        if (node.ImagePath != "" && Thumbnail(Path.Combine(shelf.Media, Path.GetFileName(node.ImagePath)), decodeWidth, 125) is { } pic)
        {
            pic.MaxHeight = 125;
            pic.Stretch = Stretch.Uniform;
            pic.HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = EditionPalette.Mix(accentHex, "#FFFFFF", 0.38),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = pic
            });
        }

        void AddCompactRow(string label, string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            var tb = new TextBlock { FontSize = 11.5, LineHeight = 15.5, Foreground = ink, TextWrapping = TextWrapping.Wrap };
            tb.Inlines!.Add(new Avalonia.Controls.Documents.Run("▪ ") { Foreground = accentBrush, FontWeight = FontWeight.Bold });
            tb.Inlines.Add(new Avalonia.Controls.Documents.Run(label + ": ") { Foreground = accentBrush, FontWeight = FontWeight.Bold });
            tb.Inlines.Add(new Avalonia.Controls.Documents.Run(val));
            col.Children.Add(tb);
        }

        void AddSectionBlock(string label, string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            var sec = Column(2);
            sec.Margin = new Thickness(0, 3, 0, 0);
            sec.Children.Add(new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = accentBrush });
            sec.Children.Add(new Border { Height = 0.7, Background = EditionPalette.Mix(accentHex, "#FFFFFF", 0.32) });
            sec.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = new FontFamily("Georgia, Liberation Serif, serif"),
                FontSize = 11.5,
                LineHeight = 16,
                Foreground = ink,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 6,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            col.Children.Add(sec);
        }

        if (!string.IsNullOrWhiteSpace(node.Role))
            AddSectionBlock("Función narrativa", node.Role);

        foreach (var (lbl, val) in Dossiers.Details(work, node))
        {
            if (val.Length <= 72 && !val.Contains('\n')) AddCompactRow(lbl, val);
            else AddSectionBlock(lbl, val);
        }

        AddSectionBlock("Trasfondo", node.Background);
        foreach (var (lbl, val) in node.Sections)
            AddSectionBlock(lbl, val);
        AddSectionBlock("Relaciones", node.Relations);
        foreach (var (target, role) in Dossiers.Links(work, node))
            AddCompactRow(role, target.Name);
        AddSectionBlock("Notas de autor", node.Notes);

        col.Children.Add(new Border { Height = 0.8, Background = Brush.Parse("#DCE2E8"), Margin = new Thickness(0, 8, 0, 0) });
        col.Children.Add(new TextBlock
        {
            Text = $"{work.Title}  ·  Ficha {work.GlossaryPaper} ({mmW} × {mmH} mm)",
            FontSize = 10,
            Foreground = muted,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Width = sheetW,
            MinHeight = sheetH,
            Background = Brush.Parse("#FCFBF7"),
            BorderBrush = Brush.Parse("#C9D1D8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            BoxShadow = BoxShadows.Parse("0 10 24 #24000000"),
            Child = col,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    void DuplicateNode(Work work, Node source)
    {
        var copy = new Node
        {
            Name = source.Name + " (copia)",
            NodeKind = source.NodeKind,
            ParentId = source.ParentId,
            BaseId = source.BaseId,
            ImagePath = source.ImagePath,
            Type = source.Type,
            Age = source.Age,
            Measures = source.Measures,
            Aliases = source.Aliases,
            Background = source.Background,
            Notes = source.Notes,
            Relations = source.Relations,
            Tags = source.Tags,
            Role = source.Role,
            Status = source.Status,
            Pinned = source.Pinned,
            Sections = new Dictionary<string, string>(source.Sections),
            RelatedIds = [.. source.RelatedIds],
            RelationRoles = new Dictionary<string, string>(source.RelationRoles),
            AttributeDefinitions = source.AttributeDefinitions.Select(d => new FieldDefinition
            {
                Name = d.Name,
                Type = d.Type,
                DefaultValue = d.DefaultValue,
                Description = d.Description,
                Unit = d.Unit,
                Group = d.Group,
                Options = d.Options
            }).ToList(),
            Attributes = source.Attributes.Select(a => new FieldValue
            {
                DefinitionId = a.DefinitionId,
                Key = a.Key,
                Value = a.Value,
                SourceNodeId = a.SourceNodeId
            }).ToList()
        };
        work.Glossary.Add(copy);
        Changed();
        Glossary(copy, 0);
    }

    void DeleteNode(Work work, Node target)
    {
        work.Glossary.Remove(target);
        foreach (var other in work.Glossary)
        {
            if (other.ParentId == target.Id) other.ParentId = "";
            if (other.BaseId == target.Id) other.BaseId = "";
            other.RelatedIds.Remove(target.Id);
            other.RelationRoles.Remove(target.Id);
        }
        Changed();
        Glossary(null);
    }

    void Archive(Work work,string query)
    {
        if(archiveWork!=work.Id){archiveWork=work.Id;archiveKind="";archiveQuery="";archivePinned=false;archivePage=0;archiveViewMode=0;}
        if(query!="")archiveQuery=query;
        var content=Column(20);content.MaxWidth=1420;content.Margin=new Thickness(12,8,12,32);
        var hero=new Grid{ColumnDefinitions=new ColumnDefinitions("1.25*,0.95*"),ClipToBounds=true};
        var heading=Column(10);heading.VerticalAlignment=VerticalAlignment.Center;heading.Margin=new Thickness(32,20);
        heading.Children.Add(new TextBlock{Text=work.Title,FontSize=15,Foreground=Brush.Parse("#D9C79D"),TextWrapping=TextWrapping.Wrap});
        var title=Display("Glosario y enciclopedia",44);title.Foreground=Brushes.Ivory;heading.Children.Add(title);
        var classes=work.Glossary.Select(n=>n.NodeKind).Distinct().Count();
        var pinnedCount=work.Glossary.Count(n=>n.Pinned);
        var linksCount=work.Glossary.Sum(n=>n.RelatedIds.Count + (n.ParentId!=""?1:0));
        var count=Label($"{work.Glossary.Count} fichas   ·   {classes} clases   ·   {pinnedCount} destacadas   ·   {linksCount} vínculos");
        count.Foreground=Brush.Parse("#D4D6CE");heading.Children.Add(count);

        var heroExports=new WrapPanel{Margin=new Thickness(0,4,0,0)};
        var btnFullPdf=Action("Exportar glosario completo (PDF)…",async()=>await Export(true,true));
        Emphasize(btnFullPdf);
        btnFullPdf.Margin=new Thickness(0,0,8,6);
        var btnSpecificPdf=Action("Glosario específico / Selección (PDF)…",async()=>await Guard(()=>ChooseCards(work)));
        btnSpecificPdf.Margin=new Thickness(0,0,8,6);
        var btnPreviewFull=QuietAction("Vista previa PDF completo",async()=>await Preview(true));
        btnPreviewFull.Foreground=Brush.Parse("#F5E3B8");
        btnPreviewFull.Margin=new Thickness(0,0,8,6);
        heroExports.Children.Add(btnFullPdf);
        heroExports.Children.Add(btnSpecificPdf);
        heroExports.Children.Add(btnPreviewFull);
        heading.Children.Add(heroExports);

        hero.Background=EditionPalette.Mix(palette.Binding,"#171C22",.67);hero.Children.Add(heading);
        var print=new Image{Source=Observatorio.Value,Height=216,Stretch=Stretch.UniformToFill};Grid.SetColumn(print,1);hero.Children.Add(print);
        content.Children.Add(new Border{Child=hero,ClipToBounds=true,CornerRadius=new CornerRadius(9)});

        var tools=new WrapPanel();
        var search=new TextBox{Watermark="Buscar nombre, alias o clase",Width=258,Text=archiveQuery};
        var filters=new ComboBox{ItemsSource=new[]{"Todas las clases"}.Concat(work.Glossary.Select(n=>n.NodeKind).Distinct().OrderBy(k=>k)).ToArray(),SelectedIndex=0};
        if(archiveKind!="" && filters.Items.Contains(archiveKind))filters.SelectedItem=archiveKind;
        var stars=new CheckBox{Content="Destacadas",IsChecked=archivePinned};
        var browseTree=new Button{Content="Jerarquía"};
        browseTree.Click+=(_,_)=>{
            var tree=new TreeView{Width=290,MaxHeight=440};
            var entries=work.Glossary.ToDictionary(n=>n.Id,n=>new TreeViewItem{Tag=n,Header=$"{n.Name} ({n.NodeKind})",IsExpanded=true});
            foreach(var n in work.Glossary){
                if(entries.TryGetValue(n.ParentId,out var parent) && !WorldModel.WouldCycle(work,n.Id,n.ParentId,""))parent.Items.Add(entries[n.Id]);else tree.Items.Add(entries[n.Id]);
            }
            var fly=new Flyout{Content=tree};
            tree.SelectionChanged+=(_,_)=>{if(tree.SelectedItem is TreeViewItem{Tag:Node selected}){fly.Hide();Glossary(selected);}};
            fly.ShowAt(browseTree);
        };
        var pdf=new MenuFlyout();
        foreach(var paper in Dossiers.Papers){
            var (width,height)=Dossiers.Size(paper);
            var option=new MenuItem{Header=(paper==work.GlossaryPaper?"✓  ":"")+ $"{paper}  ·  {width} × {height} mm"};
            option.Click+=(_,_)=>{work.GlossaryPaper=paper;Changed();Archive(work,archiveQuery);};pdf.Items.Add(option);
        }
        pdf.Items.Add(new Separator());
        pdf.Items.Add(MenuAction("Vista previa PDF (Glosario completo)",async()=>await Preview(true)));
        pdf.Items.Add(MenuAction("Exportar glosario completo (PDF)…",async()=>await Export(true,true)));
        pdf.Items.Add(MenuAction("Elegir fichas para un PDF específico…",async()=>await Guard(()=>ChooseCards(work))));
        pdf.Items.Add(MenuAction("Exportar código LaTeX del glosario (.zip)…",async()=>await Export(false,true)));

        Button modeCardsBtn=null!,modeClassBtn=null!,modeBookletBtn=null!;
        var groups=Column(18);
        modeCardsBtn=QuietAction("Tarjetas",()=>{archiveViewMode=0;Refresh();});
        modeClassBtn=QuietAction("Por clase",()=>{archiveViewMode=1;Refresh();});
        modeBookletBtn=QuietAction("Vista de cuadernillo",()=>{archiveViewMode=2;Refresh();});

        foreach(var control in new Control[]{NewEntry(work,null),search,filters,stars,browseTree,new Button{Content=$"Cuadernillo ({work.GlossaryPaper})  ▾",Flyout=pdf},modeCardsBtn,modeClassBtn,modeBookletBtn}){
            control.Margin=new Thickness(0,0,10,5);control.VerticalAlignment=VerticalAlignment.Center;tools.Children.Add(control);
        }
        content.Children.Add(tools);
        content.Children.Add(groups);

        Control BuildArchiveCard(Node entry)
        {
            var card=Column(7);card.Width=274;
            Control plate=ClassEmblem(entry.NodeKind);
            if(entry.ImagePath!="" && Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(entry.ImagePath)),420,164) is {} img){img.Stretch=Stretch.Uniform;img.HorizontalAlignment=HorizontalAlignment.Stretch;plate=img;}
            card.Children.Add(plate);

            var parentName=work.Glossary.FirstOrDefault(p=>p.Id==entry.ParentId)?.Name;
            var modelName=work.Glossary.FirstOrDefault(p=>p.Id==entry.BaseId)?.Name;
            var metaKicker=entry.NodeKind + (parentName!=null?"  ·  "+parentName:"") + (modelName!=null?"  ·  "+modelName:"");
            card.Children.Add(Kicker(metaKicker));

            var name=Display((entry.Pinned?"★  ":"")+entry.Name,22);name.MaxLines=2;name.TextTrimming=TextTrimming.CharacterEllipsis;card.Children.Add(name);
            var caption=new[]{entry.Role,entry.Background,entry.Aliases}.FirstOrDefault(t=>!string.IsNullOrWhiteSpace(t));
            if(caption!=null)card.Children.Add(new TextBlock{Text=caption,MaxLines=2,FontSize=13,Foreground=Quiet,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis});

            var eff=WorldModel.Effective(work,entry).Where(f=>!string.IsNullOrWhiteSpace(f.Value)).Take(2).ToArray();
            var links=Dossiers.Links(work,entry).Count();
            if(eff.Length>0 || links>0 || !string.IsNullOrWhiteSpace(entry.Age)){
                var badges=new WrapPanel();
                if(!string.IsNullOrWhiteSpace(entry.Age))
                    badges.Children.Add(new Border{Background=PanelColor,CornerRadius=new CornerRadius(3),Padding=new Thickness(6,2),Margin=new Thickness(0,0,4,4),Child=new TextBlock{Text=entry.Age,FontSize=11,Foreground=Quiet}});
                foreach(var f in eff)
                    badges.Children.Add(new Border{Background=PanelColor,CornerRadius=new CornerRadius(3),Padding=new Thickness(6,2),Margin=new Thickness(0,0,4,4),Child=new TextBlock{Text=$"{f.Definition.Name}: {f.Value}",FontSize=11,Foreground=Quiet,MaxWidth=160,TextTrimming=TextTrimming.CharacterEllipsis}});
                if(links>0)
                    badges.Children.Add(new Border{Background=PanelColor,CornerRadius=new CornerRadius(3),Padding=new Thickness(6,2),Margin=new Thickness(0,0,4,4),Child=new TextBlock{Text=$"{links} vínc.",FontSize=11,Foreground=Accent}});
                card.Children.Add(badges);
            }

            var quickRow=new WrapPanel{Margin=new Thickness(0,2,0,0)};
            quickRow.Children.Add(QuietAction("Editar",()=>Glossary(entry,0,search.Text??"")));
            quickRow.Children.Add(QuietAction("Vista PDF",async()=>await Preview(true,entry.Id)));
            quickRow.Children.Add(QuietAction("PDF…",async()=>await Export(true,true,entry.Id)));
            card.Children.Add(quickRow);

            var button=Action(entry.Name,()=>Glossary(entry,-1,search.Text??""));
            button.Content=card;button.Background=Sheet;button.BorderBrush=Edge;
            button.Padding=new Thickness(12);button.Margin=new Thickness(0,0,12,12);
            return button;
        }

        void Refresh(){
            groups.Children.Clear();
            modeCardsBtn.Foreground=archiveViewMode==0?Accent:Quiet;
            modeClassBtn.Foreground=archiveViewMode==1?Accent:Quiet;
            modeBookletBtn.Foreground=archiveViewMode==2?Accent:Quiet;

            var found=work.Glossary.Where(n=>(filters.SelectedIndex==0||n.NodeKind==filters.SelectedItem as string)
                && (stars.IsChecked!=true||n.Pinned) && (n.Name+" "+n.Aliases+" "+n.NodeKind+" "+n.Tags+" "+n.Type).Contains(search.Text?.Trim()??"",StringComparison.CurrentCultureIgnoreCase));
            archiveQuery=search.Text??"";archiveKind=filters.SelectedIndex>0?filters.SelectedItem as string??"":"";archivePinned=stars.IsChecked==true;
            var ordered=found.OrderByDescending(n=>n.Pinned).ThenBy(n=>n.Name).ToArray();
            archivePage=Math.Clamp(archivePage,0,Math.Max(0,(ordered.Length-1)/PageLength));

            if(archiveViewMode==2 && ordered.Length>0){
                var bookletInfo=new DockPanel{Margin=new Thickness(0,0,0,8)};
                bookletInfo.Children.Add(Kicker($"Vista previa de cuadernillo impreso  ·  Papel {work.GlossaryPaper}  ·  {ordered.Length} fichas"));
                groups.Children.Add(bookletInfo);
                var sheetsWrap=new WrapPanel();
                var coverWrap=Column(6);coverWrap.Margin=new Thickness(0,0,18,18);
                coverWrap.Children.Add(Kicker("Portada e índice del glosario"));
                coverWrap.Children.Add(BuildGlossaryCoverSheet(work));
                sheetsWrap.Children.Add(coverWrap);
                foreach(var entry in ordered.Skip(archivePage*PageLength).Take(PageLength)){
                    var sheetCol=Column(6);sheetCol.Margin=new Thickness(0,0,18,18);
                    var topBar=new DockPanel{Width=340};
                    var actions=Row();actions.Margin=new Thickness(0);
                    actions.Children.Add(QuietAction("Abrir",()=>Glossary(entry,-1,archiveQuery)));
                    actions.Children.Add(QuietAction("Editar",()=>Glossary(entry,0,archiveQuery)));
                    actions.Children.Add(QuietAction("PDF de esta ficha…",async()=>await Export(true,true,entry.Id)));
                    DockPanel.SetDock(actions,Dock.Right);
                    topBar.Children.Add(actions);
                    topBar.Children.Add(Kicker(entry.NodeKind));
                    sheetCol.Children.Add(topBar);
                    sheetCol.Children.Add(BuildGlossaryCardSheet(work,entry));
                    sheetsWrap.Children.Add(sheetCol);
                }
                groups.Children.Add(sheetsWrap);
            }
            else if(archiveViewMode==1 && ordered.Length>0){
                foreach(var grp in ordered.GroupBy(n=>n.NodeKind).OrderBy(g=>g.Key)){
                    var secCol=Column(10);
                    var grpHeader=new DockPanel();
                    var exportKindBtn=QuietAction($"Exportar PDF de {grp.Key} ({grp.Count()})…",async()=>{
                        var set=grp.Select(x=>x.Id).ToHashSet();
                        await Export(true,true,selection:set);
                    });
                    DockPanel.SetDock(exportKindBtn,Dock.Right);
                    grpHeader.Children.Add(exportKindBtn);
                    grpHeader.Children.Add(Display($"{grp.Key} ({grp.Count()})",24));
                    secCol.Children.Add(grpHeader);
                    secCol.Children.Add(Rule(Edge));
                    var cards=new WrapPanel();
                    foreach(var entry in grp)cards.Children.Add(BuildArchiveCard(entry));
                    secCol.Children.Add(cards);
                    groups.Children.Add(secCol);
                }
            }
            else if(ordered.Length>0){
                var cards=new WrapPanel();groups.Children.Add(cards);
                foreach(var entry in ordered.Skip(archivePage*PageLength).Take(PageLength)){
                    cards.Children.Add(BuildArchiveCard(entry));
                }
            }
            if(ordered.Length>PageLength && archiveViewMode!=1){
                var pager=new WrapPanel();var prev=QuietAction("‹ Anterior",()=>{archivePage--;Refresh();});prev.IsEnabled=archivePage>0;pager.Children.Add(prev);
                pager.Children.Add(Label($"{archivePage+1} / {(ordered.Length+PageLength-1)/PageLength}"));
                var next=QuietAction("Siguiente ›",()=>{archivePage++;Refresh();});next.IsEnabled=(archivePage+1)*PageLength<ordered.Length;pager.Children.Add(next);groups.Children.Add(pager);
            }
            if(groups.Children.Count==0)groups.Children.Add(Label(work.Glossary.Count==0?"Crea la primera ficha de esta obra.":"Ninguna ficha coincide con estos filtros."));
        }
        void FilterChanged(){archivePage=0;Refresh();}
        search.TextChanged+=(_,_)=>FilterChanged();filters.SelectionChanged+=(_,_)=>FilterChanged();stars.IsCheckedChanged+=(_,_)=>FilterChanged();Refresh();
        page.Content=new ScrollViewer{Content=content};
    }

    async Task ChooseCards(Work work)
    {
        var window=new Window{Title="Glosario específico · Selección de fichas para PDF",Width=820,Height=620,MinWidth=620,MinHeight=460,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Sheet};
        var root=new DockPanel{Margin=new Thickness(20)};
        var all=work.Glossary.OrderByDescending(n=>n.Pinned).ThenBy(n=>n.Name).ToArray();
        var selected=new HashSet<string>(all.Where(n=>n.Pinned).Select(n=>n.Id));
        if(selected.Count==0)foreach(var n in all)selected.Add(n.Id);
        var matches=all;int pageIndex=0;
        Node? previewNode=all.FirstOrDefault();

        int exitAction=0; // 0 = cancel, 1 = export PDF, 2 = preview PDF
        var footer=new DockPanel{Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var rightFooter=Row();rightFooter.Margin=new Thickness(0);
        var previewBtn=Action("Vista previa PDF de la selección",()=>{exitAction=2;window.Close(true);});
        var save=Action("Exportar PDF…",()=>{exitAction=1;window.Close(true);});Emphasize(save);
        rightFooter.Children.Add(previewBtn);
        rightFooter.Children.Add(save);
        rightFooter.Children.Add(QuietAction("Cancelar",()=>{exitAction=0;window.Close(false);}));
        DockPanel.SetDock(rightFooter,Dock.Right);
        footer.Children.Add(rightFooter);

        var paperRow=Row();paperRow.Margin=new Thickness(0);
        paperRow.Children.Add(Label("Tamaño de papel:",13));
        var paperCombo=new ComboBox{ItemsSource=Dossiers.Papers,SelectedItem=Dossiers.Papers.Contains(work.GlossaryPaper)?work.GlossaryPaper:Dossiers.Papers[0],Width=120};
        paperRow.Children.Add(paperCombo);
        footer.Children.Add(paperRow);

        var head=Column(6);DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
        head.Children.Add(Kicker("Exportación selectiva de glosario"));
        head.Children.Add(Display("Elegir fichas para el cuadernillo PDF",26));
        head.Children.Add(Label("Marca las fichas individuales, clases o ramas jerárquicas que deseas incluir en este PDF. Los atributos heredados de fichas externas se conservan automáticamente.",13));

        var mainSplit=new Grid{ColumnDefinitions=new ColumnDefinitions("1.15*,0.85*"),ColumnSpacing=18,Margin=new Thickness(0,10,0,0)};
        var leftCol=Column(8);
        var filterRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,160,Auto"),ColumnSpacing=8};
        var query=new TextBox{Watermark="Buscar por nombre, alias o clase"};
        var kindFilter=new ComboBox{ItemsSource=new[]{"Todas las clases"}.Concat(all.Select(n=>n.NodeKind).Distinct().OrderBy(k=>k)).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        var onlyPinned=new CheckBox{Content="★"};
        ToolTip.SetTip(onlyPinned,"Mostrar solo fichas destacadas");
        filterRow.Children.Add(query);
        Grid.SetColumn(kindFilter,1);filterRow.Children.Add(kindFilter);
        Grid.SetColumn(onlyPinned,2);filterRow.Children.Add(onlyPinned);
        leftCol.Children.Add(filterRow);

        var group=new WrapPanel();
        Button prev=null!,next=null!;var count=Label("",12.5);
        var previewHost=new ContentControl{HorizontalAlignment=HorizontalAlignment.Center};

        void UpdatePreview(Node? target)
        {
            previewNode=target??all.FirstOrDefault(n=>selected.Contains(n.Id))??all.FirstOrDefault();
            if(previewNode==null){previewHost.Content=Label("Sin fichas para previsualizar.");return;}
            previewHost.Content=new Viewbox
            {
                Stretch=Stretch.Uniform,
                MaxHeight=410,
                Child=BuildGlossaryCardSheet(work,previewNode,280)
            };
        }

        paperCombo.SelectionChanged+=(_,_)=>{
            if(paperCombo.SelectedItem is string p){work.GlossaryPaper=p;Changed();UpdatePreview(previewNode);}
        };

        var choices=Column(4);
        void Count(){
            bool any=selected.Count>0;
            save.IsEnabled=any;
            previewBtn.IsEnabled=any;
            save.Content=$"Exportar PDF ({selected.Count})…";
        }
        void Draw(){
            choices.Children.Clear();
            foreach(var entry in matches.Skip(pageIndex*40).Take(40)){
                var itemNode=entry;
                var row=new DockPanel();
                var inspectBtn=QuietAction("Ver hoja",()=>UpdatePreview(itemNode));
                inspectBtn.FontSize=11.5;
                DockPanel.SetDock(inspectBtn,Dock.Right);
                row.Children.Add(inspectBtn);
                var parentName=work.Glossary.FirstOrDefault(p=>p.Id==itemNode.ParentId)?.Name;
                var labelText=(itemNode.Pinned?"★ ":"")+itemNode.Name+" · "+itemNode.NodeKind+(parentName!=null?" ("+parentName+")":"");
                var box=new CheckBox{Content=labelText,IsChecked=selected.Contains(itemNode.Id)};
                box.IsCheckedChanged+=(_,_)=>{if(box.IsChecked==true)selected.Add(itemNode.Id);else selected.Remove(itemNode.Id);Count();UpdatePreview(itemNode);};
                row.Children.Add(box);
                choices.Children.Add(row);
            }
            prev.IsEnabled=pageIndex>0;next.IsEnabled=(pageIndex+1)*40<matches.Length;
            count.Text=$"{pageIndex+1} / {Math.Max(1,(matches.Length+39)/40)}";Count();
        }
        foreach(var on in new[]{true,false})group.Children.Add(QuietAction(on?"Marcar visibles":"Desmarcar visibles",()=>{
            foreach(var entry in matches){if(on)selected.Add(entry.Id);else selected.Remove(entry.Id);}Draw();UpdatePreview(previewNode);
        }));
        group.Children.Add(QuietAction("Solo destacadas ★",()=>{
            selected.Clear();
            foreach(var entry in all.Where(n=>n.Pinned))selected.Add(entry.Id);
            Draw();UpdatePreview(previewNode);
        }));
        group.Children.Add(QuietAction("+ Incluir vinculadas",()=>{
            var snapshotIds=selected.ToArray();
            foreach(var id in snapshotIds){
                var n=all.FirstOrDefault(x=>x.Id==id);
                if(n==null)continue;
                if(n.ParentId!="")selected.Add(n.ParentId);
                if(n.BaseId!="")selected.Add(n.BaseId);
                foreach(var rel in n.RelatedIds)selected.Add(rel);
                foreach(var child in all.Where(c=>c.ParentId==n.Id || c.RelatedIds.Contains(n.Id)))selected.Add(child.Id);
            }
            Draw();UpdatePreview(previewNode);
        }));
        prev=QuietAction("‹",()=>{pageIndex--;Draw();});next=QuietAction("›",()=>{pageIndex++;Draw();});
        group.Children.Add(prev);group.Children.Add(count);group.Children.Add(next);
        leftCol.Children.Add(group);
        leftCol.Children.Add(new Border{Background=PanelColor,BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(10),Child=new ScrollViewer{Content=choices,MaxHeight=340}});

        void ApplyModalFilter(){
            var q=query.Text?.Trim()??"";
            var k=kindFilter.SelectedIndex>0?kindFilter.SelectedItem as string??"":"";
            bool pin=onlyPinned.IsChecked==true;
            matches=all.Where(n=>(k=="" || n.NodeKind==k) && (!pin || n.Pinned) && (n.Name+" "+n.Aliases+" "+n.NodeKind+" "+n.Tags).Contains(q,StringComparison.CurrentCultureIgnoreCase)).ToArray();
            pageIndex=0;Draw();
        }
        query.TextChanged+=(_,_)=>ApplyModalFilter();
        kindFilter.SelectionChanged+=(_,_)=>ApplyModalFilter();
        onlyPinned.IsCheckedChanged+=(_,_)=>ApplyModalFilter();

        var rightCol=Column(6);
        rightCol.Children.Add(Kicker("Vista previa de ficha en cuadernillo"));
        rightCol.Children.Add(previewHost);

        mainSplit.Children.Add(leftCol);
        Grid.SetColumn(rightCol,1);
        mainSplit.Children.Add(rightCol);

        Draw();
        UpdatePreview(previewNode);
        root.Children.Add(mainSplit);
        window.Content=root;
        if(await window.ShowDialog<bool>(this)){
            if(exitAction==2)await Preview(true,null,selected);
            else if(exitAction==1)await Export(true,true,selection:selected);
        }
    }

    void Dossier(Work work,Node node,string query)
    {
        var content=Column(18);content.Margin=new Thickness(16,6,16,36);content.MaxWidth=1180;
        var nav=new WrapPanel();nav.Children.Add(QuietAction("‹ Glosario",()=>Glossary(null,-1,query)));
        var parent=work.Glossary.FirstOrDefault(n=>n.Id==node.ParentId);
        if(parent!=null)nav.Children.Add(QuietAction("Ubicación: "+parent.Name,()=>Glossary(parent,-1,query)));
        var baseModel=work.Glossary.FirstOrDefault(n=>n.Id==node.BaseId);
        if(baseModel!=null)nav.Children.Add(QuietAction("Modelo base: "+baseModel.Name,()=>Glossary(baseModel,-1,query)));
        content.Children.Add(nav);
        var cover=new DockPanel();var portrait=node.ImagePath==""?null:Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(node.ImagePath)),420,164);
        Control emblem=(Control?)portrait??ClassEmblem(node.NodeKind);emblem.Width=164;emblem.Margin=new Thickness(0,0,25,0);
        DockPanel.SetDock(emblem,Dock.Left);cover.Children.Add(emblem);
        var title=Column(5);title.VerticalAlignment=VerticalAlignment.Center;
        title.Children.Add(Kicker(node.NodeKind + (node.Type != "" ? "  ·  " + node.Type : "") + (node.Status != "" ? "  ·  " + node.Status : "")));
        title.Children.Add(Display((node.Pinned?"★  ":"")+node.Name,42));
        if(node.Role!="")title.Children.Add(Label(node.Role,18));
        if(node.Aliases!="")title.Children.Add(Label("Alias: "+node.Aliases));cover.Children.Add(title);
        content.Children.Add(cover);

        var commands=new WrapPanel();
        var edit=Action("Editar ficha",()=>Glossary(node,0,query));Emphasize(edit);commands.Children.Add(edit);
        commands.Children.Add(NewEntry(work,node));
        commands.Children.Add(Action("Vista previa PDF de esta ficha",async()=>await Preview(true,node.Id)));
        commands.Children.Add(Action("Exportar ficha PDF…",async()=>await Export(true,true,node.Id)));
        commands.Children.Add(QuietAction("Elegir varias fichas para PDF…",async()=>await Guard(()=>ChooseCards(work))));
        commands.Children.Add(QuietAction(node.Pinned?"★ Destacada":"☆ Destacar",()=>{node.Pinned=!node.Pinned;Changed();Dossier(work,node,query);}));
        commands.Children.Add(QuietAction("Duplicar ficha",()=>DuplicateNode(work,node)));
        var delBtn=QuietAction("Eliminar ficha",()=>{});bool confirmDel=false;
        delBtn.Click+=(_,_)=>{if(!confirmDel){confirmDel=true;delBtn.Content="Confirmar eliminación";return;}DeleteNode(work,node);};
        commands.Children.Add(delBtn);
        content.Children.Add(commands);content.Children.Add(Rule(Edge));

        var narrative=Column(14);var index=Column(12);
        void Passage(string label,string value){
            if(string.IsNullOrWhiteSpace(value))return;narrative.Children.Add(Display(label,24));
            narrative.Children.Add(new SelectableTextBlock{Text=value,FontFamily=WritingFamily,FontSize=18,LineHeight=30,TextWrapping=TextWrapping.Wrap});
        }
        Passage("Trasfondo",node.Background);
        foreach(var (label,value) in node.Sections)Passage(label,value);
        Passage("Relaciones",node.Relations);Passage("Notas de autor",node.Notes);

        var mentions=Dossiers.MentioningChapters(work,node);
        if(mentions.Count>0){
            narrative.Children.Add(Rule(Edge));
            narrative.Children.Add(Display($"Apariciones en el manuscrito ({mentions.Count})",22));
            var mentionWrap=new WrapPanel();
            foreach(var ch in mentions){
                var targetCh=ch;
                var mBtn=Action(targetCh.Title,()=>{chapter=targetCh;RememberSection(work,targetCh);Editor();});
                mBtn.Margin=new Thickness(0,0,8,8);
                mentionWrap.Children.Add(mBtn);
            }
            narrative.Children.Add(mentionWrap);
        }

        var previewCard=Column(8);
        previewCard.Children.Add(Kicker($"Hoja impresa ({work.GlossaryPaper})"));
        previewCard.Children.Add(new Viewbox{Stretch=Stretch.Uniform,MaxHeight=380,Child=BuildGlossaryCardSheet(work,node,340)});
        index.Children.Add(Card(previewCard));

        foreach(var (label,value) in Dossiers.Details(work,node)){
            if(string.IsNullOrWhiteSpace(value))continue;index.Children.Add(Kicker(label));index.Children.Add(Label(value,16));index.Children.Add(Rule(Edge));
        }
        var linked=Dossiers.Links(work,node).ToList();
        if(linked.Count!=0){index.Children.Add(Display("Fichas relacionadas",21));foreach(var (related,role) in linked){index.Children.Add(Kicker(role));index.Children.Add(QuietAction(related.Name+"  →",()=>Glossary(related,-1,query)));}}
        if(narrative.Children.Count==0 && index.Children.Count<=1)narrative.Children.Add(QuietAction("Añadir el trasfondo de esta ficha →",()=>Glossary(node,1,query)));
        var spread=new Grid{ColumnDefinitions=new ColumnDefinitions("1.65*,1.05*"),ColumnSpacing=30};spread.Children.Add(narrative);Grid.SetColumn(index,1);spread.Children.Add(index);
        content.Children.Add(spread);page.Content=new ScrollViewer{Content=content};
    }
}
