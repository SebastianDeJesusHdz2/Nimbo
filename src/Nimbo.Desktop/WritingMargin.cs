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
        if(!work.IsNovel){
            margin.Children.Add(Rule(Edge));
            var fmt=Format.All.FirstOrDefault(f=>f.Name==work.Type);
            var activeTpl=EditorialTemplate.Resolve(fmt,work.Template);
            margin.Children.Add(Display("Plantilla · "+activeTpl.Name,18));
            margin.Children.Add(Label(EditorialTemplate.FormatGuidance(work.Type,activeTpl.Name),12.5));
            if(activeTpl.Sections.Length>0){
                margin.Children.Add(Kicker("Secciones recomendadas"));
                var secList=Column(4);
                foreach(var secTitle in activeTpl.Sections){
                    var targetName=secTitle;
                    var existing=work.Chapters.FirstOrDefault(c=>c.Title.Equals(targetName,StringComparison.OrdinalIgnoreCase));
                    if(existing!=null){
                        var jump=QuietAction("✓  "+targetName,()=>{chapter=existing;RememberSection(work,existing);Editor();});
                        if(existing==section)jump.Foreground=Accent;
                        secList.Children.Add(jump);
                    }else{
                        secList.Children.Add(QuietAction("+  Añadir «"+targetName+"»",()=>{
                            chapter=new(){Title=targetName,Level="Chapter",Order=work.Chapters.Count};
                            work.Chapters.Add(chapter);RememberSection(work,chapter);Changed();Editor();
                        }));
                    }
                }
                margin.Children.Add(secList);
            }
            var snippets=EditorialTemplate.QuickSnippets(work.Type);
            if(snippets.Length>0){
                margin.Children.Add(Rule(Edge));
                margin.Children.Add(Kicker("Bloques rápidos · "+work.Type));
                foreach(var (label,content) in snippets){
                    var blockText=content;
                    margin.Children.Add(QuietAction("+ "+label,()=>{
                        section.PlainText=(section.PlainText.TrimEnd()+"\n"+blockText.TrimStart()).Trim();
                        section.CaretOffset=section.PlainText.Length;
                        Changed();Editor();
                    }));
                }
            }
            return margin;
        }
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
