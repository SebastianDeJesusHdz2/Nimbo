using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nimbo.Core;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    StackPanel WritingMargin(Work work, Chapter section)
    {
        var margin=Column(14);
        margin.Children.Add(Kicker("Cuaderno de trabajo"));
        var notes=new TextBox{Watermark="Deja una nota para cuando vuelvas a esta sección…",Text=section.AuthorMemo,
            AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=125,MaxHeight=220,Background=Sheet,Padding=new Thickness(12)};
        Avalonia.Automation.AutomationProperties.SetName(notes,"Notas de la sección");
        notes.TextChanged+=(_,_)=>{if(section.AuthorMemo==notes.Text)return;section.AuthorMemo=notes.Text??"";Changed();};
        margin.Children.Add(Display("Notas de la sección",21));margin.Children.Add(notes);
        if(!work.IsNovel)return margin;
        margin.Children.Add(Rule(Edge));margin.Children.Add(Display("Consultar glosario",21));
        var search=new TextBox{Watermark="Buscar una ficha…"};margin.Children.Add(search);
        var list=Column(5);var preview=Column(7);margin.Children.Add(list);margin.Children.Add(preview);
        void Inspect(Node entry)
        {
            preview.Children.Clear();preview.Children.Add(Rule(Edge));preview.Children.Add(Kicker(entry.NodeKind));preview.Children.Add(Display(entry.Name,22));
            foreach(var text in new[]{entry.Role,entry.Background}.Where(s=>s.Length>0))
                preview.Children.Add(new SelectableTextBlock{Text=text,TextWrapping=TextWrapping.Wrap,MaxLines=8});
            preview.Children.Add(QuietAction("Abrir ficha completa →",()=>Glossary(entry)));
        }
        void Find()
        {
            var q=search.Text?.Trim()??"";
            list.Children.Clear();preview.Children.Clear();
            var found=work.Glossary.Where(n=>(n.Name+" "+n.Aliases+" "+n.NodeKind).Contains(q,StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(n=>n.Pinned).ThenBy(n=>n.Name).Take(6);
            foreach(var n in found)list.Children.Add(QuietAction(n.Name,()=>Inspect(n)));
            if(list.Children.Count==0)list.Children.Add(Label(work.Glossary.Count==0?"Esta obra aún no tiene fichas.":"Sin coincidencias.",13));
        }
        search.TextChanged+=(_,_)=>Find();Find();
        return margin;
    }
}
