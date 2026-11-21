using Avalonia.Media;
namespace Nimbo.Desktop;

// Original swatches from Adobe Color Explore. Ordered here by their job in Nimbo.
internal sealed record EditionPalette(string Source,string Ink,string Binding,string Wood,string Foil,string Paper,string Detail)
{
    public static EditionPalette For(string name)=>name switch{
        "Bosque"=>new("Dining room of a canteen or cafeteria","#593E2E","#555934","#8C5B3E","#BF9B7A","#F2E6D8","#555934"),
        "Vino"=>new("color theme_Sabrina_07","#8C5C32","#A60321","#D9A577","#D97B66","#F2E9D8","#A60321"),
        "Aurora"=>new("Adobe Color · Triad","#0C5C60","#B93B8D","#796A0F","#F9DB2A","#26A6AC","#26A6AC"),
        "Porcelana"=>new("Adobe Color · Accessibility FFEBE8","#1D3ECF","#B72818","#E6AF00","#D7F7E1","#FFEBE8","#E6AF00"),
        "Grafito"=>new("color theme_Screenshot 2026-05-04 at 10","#0D0D0D","#732F3B","#73030D","#8C8C8C","#F2F2F2","#732F3B"),
        _=>new("Home blue warm","#14293A","#00508F","#4F3815","#FFCD82","#ADDBFF","#8F5601")
    };
    public string[] Swatches=>new[]{Ink,Binding,Wood,Foil,Paper,Detail}.Distinct().ToArray();
    public static SolidColorBrush Mix(string a,string b,double share){
        var first=Color.Parse(a);var second=Color.Parse(b);
        byte Blend(byte x,byte y)=>(byte)Math.Round(x*(1-share)+y*share);
        return new(Color.FromRgb(Blend(first.R,second.R),Blend(first.G,second.G),Blend(first.B,second.B)));
    }
}
