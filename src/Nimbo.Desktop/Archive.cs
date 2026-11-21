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
    const int PageLength=24;
    Button NewEntry(Work work,Node? parent)
    {
        var choices=new MenuFlyout();
        foreach(var kind in new[]{"Personaje","Lugar","Mundo","Raza","Organización","Objeto","Categoría","Concepto"})
            choices.Items.Add(MenuAction(kind,()=>{
                var next=new Node{NodeKind=kind,Name=kind+" sin nombre",ParentId=parent?.Id??""};
                work.Glossary.Add(next);Changed();Glossary(next,0);
            }));
        var button=new Button{Content=parent==null?"+ Nueva ficha":"+ Ficha hija",Flyout=choices};Emphasize(button);return button;
    }

    void Archive(Work work,string query)
    {
        if(archiveWork!=work.Id){archiveWork=work.Id;archiveKind="";archiveQuery="";archivePinned=false;archivePage=0;}
        if(query!="")archiveQuery=query;
        var content=Column(22);content.MaxWidth=1400;content.Margin=new Thickness(12,8,12,32);
        var hero=new Grid{ColumnDefinitions=new ColumnDefinitions("1.1*,1*"),ClipToBounds=true};
        var heading=Column(10);heading.VerticalAlignment=VerticalAlignment.Center;heading.Margin=new Thickness(32,18);
        heading.Children.Add(new TextBlock{Text=work.Title,FontSize=15,Foreground=Brush.Parse("#D9C79D"),TextWrapping=TextWrapping.Wrap});
        var title=Display("Glosario",48);title.Foreground=Brushes.Ivory;heading.Children.Add(title);
        var classes=work.Glossary.Select(n=>n.NodeKind).Distinct().Count();
        var count=Label($"{work.Glossary.Count} fichas   ·   {classes} clases");count.Foreground=Brush.Parse("#D4D6CE");heading.Children.Add(count);
        hero.Background=EditionPalette.Mix(palette.Binding,"#171C22",.67);hero.Children.Add(heading);
        var print=new Image{Source=Observatorio.Value,Height=202,Stretch=Stretch.UniformToFill};Grid.SetColumn(print,1);hero.Children.Add(print);
        content.Children.Add(new Border{Child=hero,ClipToBounds=true,CornerRadius=new CornerRadius(9)});
        var tools=new WrapPanel();
        var search=new TextBox{Watermark="Buscar nombre, alias o clase",Width=258,Text=archiveQuery};
        var filters=new ComboBox{ItemsSource=new[]{"Todas las clases"}.Concat(work.Glossary.Select(n=>n.NodeKind).Distinct().OrderBy(k=>k)).ToArray(),SelectedIndex=0};
        if(archiveKind!="" && filters.Items.Contains(archiveKind))filters.SelectedItem=archiveKind;
        var stars=new CheckBox{Content="Destacadas",IsChecked=archivePinned};
        var browseTree=new Button{Content="Jerarquía"};
        browseTree.Click+=(_,_)=>{
            var tree=new TreeView{Width=270,MaxHeight=420};
            var entries=work.Glossary.ToDictionary(n=>n.Id,n=>new TreeViewItem{Tag=n,Header=n.Name,IsExpanded=true});
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
        pdf.Items.Add(new Separator());pdf.Items.Add(MenuAction("Vista previa",async()=>await Preview(true)));pdf.Items.Add(MenuAction("Exportar cuadernillo…",async()=>await Export(true,true)));
        foreach(var control in new Control[]{NewEntry(work,null),search,filters,stars,browseTree,new Button{Content="Cuadernillo  ▾",Flyout=pdf}}){
            control.Margin=new Thickness(0,0,10,5);control.VerticalAlignment=VerticalAlignment.Center;tools.Children.Add(control);
        }
        content.Children.Add(tools);
        var groups=Column(18);content.Children.Add(groups);
        void Refresh(){
            groups.Children.Clear();
            var found=work.Glossary.Where(n=>(filters.SelectedIndex==0||n.NodeKind==filters.SelectedItem as string)
                && (stars.IsChecked!=true||n.Pinned) && (n.Name+" "+n.Aliases+" "+n.NodeKind+" "+n.Tags+" "+n.Type).Contains(search.Text?.Trim()??"",StringComparison.CurrentCultureIgnoreCase));
            archiveQuery=search.Text??"";archiveKind=filters.SelectedIndex>0?filters.SelectedItem as string??"":"";archivePinned=stars.IsChecked==true;
            var ordered=found.OrderByDescending(n=>n.Pinned).ThenBy(n=>n.Name).ToArray();
            archivePage=Math.Clamp(archivePage,0,Math.Max(0,(ordered.Length-1)/PageLength));
            if(ordered.Length>0){
                var cards=new WrapPanel();groups.Children.Add(cards);
                foreach(var entry in ordered.Skip(archivePage*PageLength).Take(PageLength)){
                    var card=Column(8);card.Width=262;
                    Control plate=ClassEmblem(entry.NodeKind);
                    if(entry.ImagePath!="" && Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(entry.ImagePath)),420,168) is {} img){img.Stretch=Stretch.UniformToFill;img.HorizontalAlignment=HorizontalAlignment.Stretch;plate=img;}
                    card.Children.Add(plate);card.Children.Add(Kicker(entry.NodeKind));
                    var name=Display((entry.Pinned?"★  ":"")+entry.Name,22);name.MaxLines=2;name.TextTrimming=TextTrimming.CharacterEllipsis;card.Children.Add(name);
                    var caption=new[]{entry.Role,entry.Background,entry.Aliases,work.Glossary.FirstOrDefault(p=>p.Id==entry.ParentId)?.Name}.FirstOrDefault(t=>!string.IsNullOrWhiteSpace(t));
                    if(caption!=null)card.Children.Add(new TextBlock{Text=caption,MaxLines=2,FontSize=13,Foreground=Quiet,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis});
                    var button=Action(entry.Name,()=>Glossary(entry,-1,search.Text??""));button.Content=card;button.Background=Sheet;button.BorderBrush=Edge;
                    button.Padding=new Thickness(12);button.Margin=new Thickness(0,0,12,10);cards.Children.Add(button);
                }
            }
            if(ordered.Length>PageLength){
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

    void Dossier(Work work,Node node,string query)
    {
        var content=Column(18);content.Margin=new Thickness(16,6,16,36);content.MaxWidth=1080;
        var nav=new WrapPanel();nav.Children.Add(QuietAction("‹ Glosario",()=>Glossary(null,-1,query)));
        var parent=work.Glossary.FirstOrDefault(n=>n.Id==node.ParentId);
        if(parent!=null)nav.Children.Add(QuietAction(parent.Name,()=>Glossary(parent,-1,query)));
        content.Children.Add(nav);
        var cover=new DockPanel();var portrait=node.ImagePath==""?null:Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(node.ImagePath)),420,164);
        Control emblem=(Control?)portrait??ClassEmblem(node.NodeKind);emblem.Width=164;emblem.Margin=new Thickness(0,0,25,0);
        DockPanel.SetDock(emblem,Dock.Left);cover.Children.Add(emblem);
        var title=Column(5);title.VerticalAlignment=VerticalAlignment.Center;title.Children.Add(Kicker(node.NodeKind));title.Children.Add(Display(node.Name,44));
        if(node.Role!="")title.Children.Add(Label(node.Role,18));
        if(node.Aliases!="")title.Children.Add(Label(node.Aliases));cover.Children.Add(title);
        content.Children.Add(cover);
        var commands=new WrapPanel();var edit=Action("Editar ficha",()=>Glossary(node,0,query));Emphasize(edit);commands.Children.Add(edit);
        commands.Children.Add(NewEntry(work,node));commands.Children.Add(QuietAction(node.Pinned?"★ Destacada":"☆ Destacar",()=>{node.Pinned=!node.Pinned;Changed();Dossier(work,node,query);}));
        commands.Children.Add(QuietAction("Exportar ficha PDF…",async()=>await Export(true,true,node.Id)));
        content.Children.Add(commands);content.Children.Add(Rule(Edge));
        var narrative=Column(12);var index=Column(10);
        void Passage(string label,string value){
            if(string.IsNullOrWhiteSpace(value))return;narrative.Children.Add(Display(label,24));
            narrative.Children.Add(new SelectableTextBlock{Text=value,FontFamily=WritingFamily,FontSize=18,LineHeight=30,TextWrapping=TextWrapping.Wrap});
        }
        Passage("Trasfondo",node.Background);
        foreach(var (label,value) in node.Sections)Passage(label,value);
        Passage("Relaciones",node.Relations);Passage("Notas de autor",node.Notes);
        foreach(var (label,value) in Dossiers.Details(work,node)){
            if(string.IsNullOrWhiteSpace(value))continue;index.Children.Add(Kicker(label));index.Children.Add(Label(value,16));index.Children.Add(Rule(Edge));
        }
        var linked=Dossiers.Links(work,node).ToList();
        if(linked.Count!=0){index.Children.Add(Display("Fichas relacionadas",21));foreach(var (related,role) in linked){index.Children.Add(Kicker(role));index.Children.Add(QuietAction(related.Name+"  →",()=>Glossary(related,-1,query)));}}
        if(narrative.Children.Count==0 && index.Children.Count==0)narrative.Children.Add(QuietAction("Añadir el trasfondo de esta ficha →",()=>Glossary(node,1,query)));
        var spread=new Grid{ColumnDefinitions=new ColumnDefinitions("2*,*"),ColumnSpacing=30};spread.Children.Add(narrative);Grid.SetColumn(index,1);spread.Children.Add(index);
        content.Children.Add(spread);page.Content=new ScrollViewer{Content=content};
    }
}
