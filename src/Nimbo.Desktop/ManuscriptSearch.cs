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

    static void AddSearch(Control root, StackPanel body, StackPanel toolbar, TextBox manuscript)
    {
        var panel=Row();
        panel.IsVisible=false;
        var query=new TextBox { Watermark="Buscar en esta sección",MinWidth=185 };
        var result=Label("");
        int match=-1;
        string? lastText=null;
        string? lastQuery=null;
        void Find(bool previous)
        {
            var text=manuscript.Text??"";
            var term=query.Text??"";
            if(term.Length==0){result.Text="";return;}
            if(text!=lastText || term!=lastQuery) match=-1;
            lastText=text;lastQuery=term;
            const StringComparison mode=StringComparison.OrdinalIgnoreCase;
            if(previous){
                var end=match>0?match-1:text.Length-1;
                match=end>=0?text.LastIndexOf(term,end,mode):-1;
                if(match<0)match=text.LastIndexOf(term,mode);
            }else{
                int start=match<0?0:Math.Min(text.Length,match+term.Length);
                match=text.IndexOf(term,start,mode);
                if(match<0)match=text.IndexOf(term,mode);
            }
            if(match<0){result.Text="Sin coincidencias";return;}
            manuscript.Focus();manuscript.SelectionStart=match;manuscript.SelectionEnd=match+term.Length;
            result.Text="Coincidencia seleccionada";
        }
        void Show(){panel.IsVisible=true;query.Focus();query.SelectAll();}
        void Hide(){panel.IsVisible=false;manuscript.Focus();}
        toolbar.Children.Add(Action("Buscar",Show));
        panel.Children.Add(query);
        panel.Children.Add(Action("Anterior",()=>Find(true)));
        panel.Children.Add(Action("Siguiente",()=>Find(false)));
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
