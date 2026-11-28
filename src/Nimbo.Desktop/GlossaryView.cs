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
    void Glossary(Node? node,int tab=-1,string query="")
    {
        Navigate("Glosario");var work=current!;
        if(node==null){Archive(work,query);return;}
        if(tab<0){Dossier(work,node,query);return;}
        var body=Column(10);
        var header=Column(14);header.MaxWidth=1120;header.Margin=new Thickness(18,10);
        var topNav=new DockPanel();
        var pdfActions=Row();pdfActions.Margin=new Thickness(0);
        pdfActions.Children.Add(Action("Vista previa PDF de esta ficha",async()=>await Preview(true,node.Id)));
        pdfActions.Children.Add(QuietAction("Exportar ficha PDF…",async()=>await Export(true,true,node.Id)));
        pdfActions.Children.Add(QuietAction("Glosario específico / Selección…",async()=>await Guard(()=>ChooseCards(work))));
        DockPanel.SetDock(pdfActions,Dock.Right);
        topNav.Children.Add(pdfActions);
        var close=QuietAction("‹ Volver a la ficha",()=>{if(TrySave())Glossary(node,-1,query);});
        topNav.Children.Add(close);
        header.Children.Add(topNav);

        header.Children.Add(Kicker("Edición · "+node.NodeKind));var heading=Display(node.Name,34);header.Children.Add(heading);
        {
            var identity=body;var story=Column();var model=Column();var links=Column();
            var plate=Column(8);plate.VerticalAlignment=VerticalAlignment.Top;
            var profile=new Grid{ColumnDefinitions=new ColumnDefinitions("*,280"),ColumnSpacing=24,Margin=new Thickness(0,12)};
            profile.Children.Add(identity);Grid.SetColumn(plate,1);profile.Children.Add(plate);
            var tabs=new TabControl{ItemsSource=new[]{new TabItem{Header="Identidad",Content=profile},new TabItem{Header="Historia y secciones",Content=story},new TabItem{Header="Vínculos",Content=links},new TabItem{Header="Modelo y campos",Content=model}},SelectedIndex=tab};
            tabs.SelectionChanged+=(_,_)=>tab=tabs.SelectedIndex;header.Children.Add(tabs);

            var liveCardHost=new ContentControl{HorizontalAlignment=HorizontalAlignment.Center};
            void RefreshLiveSheet(){
                liveCardHost.Content=new Viewbox{Stretch=Stretch.Uniform,MaxHeight=310,Child=BuildGlossaryCardSheet(work,node,260)};
            }

            Field(identity,"Nombre",node.Name,v=>{node.Name=v;heading.Text=v;RefreshLiveSheet();});
            var kinds=Dossiers.Kinds.Append(node.NodeKind).Distinct().ToArray();
            Choice(identity,"Clase de ficha",kinds,node.NodeKind,v=>{if(v==node.NodeKind)return;node.NodeKind=v;Changed();Glossary(node,tab,query);});
            Field(identity,"Alias",node.Aliases,v=>{node.Aliases=v;RefreshLiveSheet();});
            Field(identity,"Clasificación",node.Type,v=>{node.Type=v;RefreshLiveSheet();});
            Field(identity,"Etiquetas",node.Tags,v=>{node.Tags=v;RefreshLiveSheet();});
            Field(identity,"Estado",node.Status,v=>{node.Status=v;RefreshLiveSheet();});
            var pin=new CheckBox{Content="Destacar en el glosario (★)",IsChecked=node.Pinned};pin.IsCheckedChanged+=(_,_)=>{node.Pinned=pin.IsChecked==true;Changed();RefreshLiveSheet();};identity.Children.Add(pin);
            Field(story,"Función narrativa",node.Role,v=>{node.Role=v;RefreshLiveSheet();});
            Field(story,"Trasfondo",node.Background,v=>{node.Background=v;RefreshLiveSheet();},true);
            foreach(var prompt in Dossiers.Prompts(node).ToArray()){
                Field(story,prompt,node.Sections.GetValueOrDefault(prompt,""),v=>{
                    if(string.IsNullOrWhiteSpace(v))node.Sections.Remove(prompt);else node.Sections[prompt]=v;
                    RefreshLiveSheet();
                },true);
            }
            var extra=new WrapPanel();var sectionName=new TextBox{Watermark="Nombre del apartado propio",MinWidth=200};
            extra.Children.Add(sectionName);extra.Children.Add(QuietAction("Añadir apartado",()=>{
                var name=sectionName.Text?.Trim();if(string.IsNullOrEmpty(name)){sectionName.Focus();return;}
                if(Dossiers.Prompts(node).Contains(name,StringComparer.CurrentCultureIgnoreCase)){status.Text="Ya existe un apartado con ese nombre.";return;}
                node.Sections.Add(name,"");Changed();Glossary(node,1,query);
            }));story.Children.Add(extra);
            Field(story,"Notas de autor",node.Notes,v=>{node.Notes=v;RefreshLiveSheet();},true);
            Field(identity,"Edad o época",node.Age,v=>{node.Age=v;RefreshLiveSheet();});Field(identity,"Medidas o extensión",node.Measures,v=>{node.Measures=v;RefreshLiveSheet();});
            Field(links,"Relaciones",node.Relations,v=>{node.Relations=v;RefreshLiveSheet();},true);
            var options=work.Glossary.Where(n=>n!=node && !node.RelatedIds.Contains(n.Id)).OrderBy(n=>n.Name).ToArray();
            var target=new ComboBox{ItemsSource=options,PlaceholderText="Elegir una ficha",HorizontalAlignment=HorizontalAlignment.Stretch};
            links.Children.Add(target);links.Children.Add(Action("Vincular",()=>{if(target.SelectedItem is Node other){node.RelatedIds.Add(other.Id);Changed();Glossary(node,2,query);}}));
            foreach(var other in work.Glossary.Where(n=>node.RelatedIds.Contains(n.Id))){
                var row=new WrapPanel();row.Children.Add(Action(other.Name,()=>Glossary(other)));row.Children.Add(Action("Desvincular",()=>{node.RelatedIds.Remove(other.Id);node.RelationRoles.Remove(other.Id);Changed();Glossary(node,2,query);}));links.Children.Add(row);
                Field(links,"Relación con "+other.Name,node.RelationRoles.GetValueOrDefault(other.Id,""),v=>{if(v=="")node.RelationRoles.Remove(other.Id);else node.RelationRoles[other.Id]=v;RefreshLiveSheet();});
            }
            foreach(var incoming in work.Glossary.Where(n=>n.RelatedIds.Contains(node.Id)))links.Children.Add(Action("Mencionada en "+incoming.Name,()=>Glossary(incoming,2,query)));
            var portrait=node.ImagePath==""?null:Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(node.ImagePath)),440,170);
            plate.Children.Add((Control?)portrait??ClassEmblem(node.NodeKind));
            plate.Children.Add(Action("Elegir ilustración…", async () => await Guard(async () =>
            {
                var path = (await StorageProvider.OpenFilePickerAsync(new() { Title = "Imagen de la ficha" })).FirstOrDefault()?.TryGetLocalPath();
                if (path != null) { node.ImagePath = shelf.AddImage(path); Changed(); Glossary(node,tab,query); }
            })));
            if(node.ImagePath!="")plate.Children.Add(QuietAction("Quitar ilustración",()=>{node.ImagePath="";Changed();Glossary(node,tab,query);}));
            plate.Children.Add(Rule(Edge));
            plate.Children.Add(Kicker($"Vista previa de hoja ({work.GlossaryPaper})"));
            RefreshLiveSheet();
            plate.Children.Add(liveCardHost);

            body=model;
            body.Children.Add(Label("Ubicación en el índice"));
            var locations = work.Glossary.Where(n => !WorldModel.WouldCycle(work, node.Id, n.Id, node.BaseId)).ToArray();
            var location = new ComboBox { ItemsSource = locations, SelectedItem = locations.FirstOrDefault(n => n.Id == node.ParentId), PlaceholderText = "Raíz" };
            location.SelectionChanged += (_, _) => { if (location.SelectedItem is Node parent) { node.ParentId = parent.Id; Changed(); Glossary(node,tab,query); } };
            body.Children.Add(location);
            if (node.ParentId != "") body.Children.Add(Action("Mover a raíz", () => { node.ParentId = ""; Changed(); Glossary(node,tab,query); }));
            var baseOptions = work.Glossary.Where(n => !WorldModel.WouldCycle(work, node.Id, node.ParentId, n.Id)).ToArray();
            body.Children.Add(Label("Modelo del que recibe campos"));
            var bases = new ComboBox { ItemsSource = baseOptions, SelectedItem = baseOptions.FirstOrDefault(n => n.Id == node.BaseId), PlaceholderText = "Elegir ficha base" };
            bases.SelectionChanged += (_, _) => { if (bases.SelectedItem is Node parent) { node.BaseId = parent.Id; Changed(); Glossary(node,tab,query); } }; body.Children.Add(bases);
            if (node.BaseId != "") body.Children.Add(Action("Quitar ficha base", () => { node.BaseId = ""; Changed(); Glossary(node,tab,query); }));
            body.Children.Add(Display("Campos que transmite esta ficha",20));
            var fields = new WrapPanel(); var key = new TextBox { Watermark = "Campo heredable", MinWidth = 165 }; var initial = new TextBox { Watermark = "Valor inicial", MinWidth = 140 }; fields.Children.Add(key); fields.Children.Add(initial);
            var unit=new TextBox{Watermark="Unidad",Width=100};fields.Children.Add(unit);
            fields.Children.Add(Action("Añadir campo", () => { if (string.IsNullOrWhiteSpace(key.Text)) return; if(node.AttributeDefinitions.Any(f=>string.Equals(f.Name.Trim(),key.Text.Trim(),StringComparison.CurrentCultureIgnoreCase))){status.Text="Ya existe un campo con ese nombre.";return;} node.AttributeDefinitions.Add(new() { Name = key.Text.Trim(), DefaultValue = initial.Text ?? "", Unit=unit.Text?.Trim()??"" }); Changed(); Glossary(node,tab,query); }));
            fields.Children.Add(QuietAction($"+ Sugeridos para {node.NodeKind}",()=>{
                foreach(var (sName,sUnit,sDef) in Dossiers.SuggestedAttributes(node.NodeKind)){
                    if(!node.AttributeDefinitions.Any(f=>string.Equals(f.Name.Trim(),sName,StringComparison.CurrentCultureIgnoreCase)))
                        node.AttributeDefinitions.Add(new(){Name=sName,Unit=sUnit,DefaultValue=sDef});
                }
                Changed();Glossary(node,tab,query);
            }));
            body.Children.Add(fields);

            foreach (var field in WorldModel.Effective(work, node))
            {
                Field(body, field.Definition.Name+(field.Definition.Unit==""?"":" ("+field.Definition.Unit+")")+(field.Inherited?" · "+field.Origin.Name:""), field.Value, v =>
                {
                    var val = node.Attributes.FirstOrDefault(a => a.DefinitionId == field.Definition.Id || (a.DefinitionId=="" && string.Equals(a.Key,field.Definition.Name,StringComparison.CurrentCultureIgnoreCase)));
                    if (val == null) { val = new() { DefinitionId = field.Definition.Id }; node.Attributes.Add(val); }
                    val.Value = v;
                    RefreshLiveSheet();
                });
                if(field.Inherited && field.Overridden)body.Children.Add(Action("Recuperar valor heredado",()=>{
                    node.Attributes.RemoveAll(a=>a.DefinitionId==field.Definition.Id || (a.DefinitionId=="" && string.Equals(a.Key,field.Definition.Name,StringComparison.CurrentCultureIgnoreCase)));Changed();Glossary(node,3,query);
                }));
            }
        }

        page.Content=new ScrollViewer{Content=header};
    }
}
