using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Nimbo.Core;
using System.Security.Cryptography;
using System.Text;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    string SectionPath(Work work) => Config("seccion-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(work.Id))));
    void RememberSection(Work work, Chapter section)
    {
        work.LastChapterId=section.Id;
        try { File.WriteAllText(SectionPath(work),section.Id); }
        catch(IOException ex) { status.Text=ex.Message; }
        catch(UnauthorizedAccessException ex) { status.Text=ex.Message; }
    }

    static void AddSearch(Control root, StackPanel body, Panel toolbar, DraftSurface draft)
    {
        var panel=new WrapPanel();
        panel.IsVisible=false;
        var query=new TextBox { Watermark="Buscar en esta sección",MinWidth=185 };
        var replacement=new TextBox { Watermark="Reemplazar con…",MinWidth=155 };
        var result=Label("");
        TextBox? last=null;int previousIndex=-1;string previousTerm="";
        void Find(bool backwards)
        {
            var term=query.Text??"";
            if(term.Length==0)return;
            var hits=new List<(TextBox Box,int Index)>();
            foreach(var box in draft.Blocks){
                var text=box.Text??"";int from=0;
                while(from<text.Length){
                    int index=text.IndexOf(term,from,StringComparison.OrdinalIgnoreCase);
                    if(index<0)break;hits.Add((box,index));from=index+term.Length;
                }
            }
            if(hits.Count==0){result.Text="Sin coincidencias";return;}
            int current=term==previousTerm?hits.FindIndex(h=>h.Box==last&&h.Index==previousIndex):-1;
            int next=current<0?(backwards?hits.Count-1:0):(current+(backwards?-1:1)+hits.Count)%hits.Count;
            var hit=hits[next];hit.Box.Focus();hit.Box.SelectionStart=hit.Index;hit.Box.SelectionEnd=hit.Index+term.Length;
            last=hit.Box;previousIndex=hit.Index;previousTerm=term;result.Text=$"{next+1} de {hits.Count}";
        }
        void Show(){panel.IsVisible=true;query.Focus();query.SelectAll();}
        void Hide(){panel.IsVisible=false;draft.Active.Focus();}
        var findButton=Action("Buscar",Show);findButton.Background=Avalonia.Media.Brushes.Transparent;findButton.BorderThickness=new Avalonia.Thickness(0);toolbar.Children.Add(findButton);
        panel.Children.Add(query);
        panel.Children.Add(Action("Anterior",()=>Find(true)));
        panel.Children.Add(Action("Siguiente",()=>Find(false)));
        panel.Children.Add(replacement);
        panel.Children.Add(Action("Reemplazar",()=>{
            string term=query.Text??"";
            if(term.Length==0)return;
            if(last==null || previousTerm!=term || last.SelectionStart==last.SelectionEnd ||
                Math.Min(last.SelectionStart,last.SelectionEnd)!=previousIndex || !string.Equals(last.SelectedText,term,StringComparison.OrdinalIgnoreCase))Find(false);
            if(last==null || !string.Equals(last.SelectedText,term,StringComparison.OrdinalIgnoreCase))return;
            last.SelectedText=replacement.Text??"";last.Focus();previousIndex=-1;result.Text="Coincidencia reemplazada";
        }));
        panel.Children.Add(Action("Cerrar",Hide));
        body.Children.Add(panel);
        panel.Children.Add(result);
        query.TextChanged+=(_,_)=>{result.Text="";};
        root.AddHandler(InputElement.KeyDownEvent,(_,e)=>{
            if(e.Key==Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control)){Show();e.Handled=true;}
            else if(panel.IsVisible && e.Key==Key.Escape){Hide();e.Handled=true;}
            else if(panel.IsVisible && (e.Key==Key.F3 || (e.Key==Key.Enter && query.IsFocused))){
                Find(e.KeyModifiers.HasFlag(KeyModifiers.Shift));e.Handled=true;
            }
        },RoutingStrategies.Tunnel);
    }
}
