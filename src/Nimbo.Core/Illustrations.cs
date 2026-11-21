using System.Text.RegularExpressions;

namespace Nimbo.Core;

public static partial class Illustrations
{
    [GeneratedRegex(@"\[\[Imagen ([1-9][0-9]*)\]\]", RegexOptions.CultureInvariant)]
    public static partial Regex Marker();
    public static string At(int index) => $"[[Imagen {index + 1}]]";
    public static string Layout(Chapter chapter,int index) => chapter.ImageLayouts.GetValueOrDefault(index,chapter.Layout);

    public const string Gallery = "Galería";
    public record Placement(int Start, int Length, int Image)
    {
        public int End => Start + Length;
    }
    // A paragraph or a different layout ends the current strip.
    public static IEnumerable<List<Placement>> Rows(Chapter chapter)
    {
        static bool Adjacent(string gap)=>string.IsNullOrWhiteSpace(gap) && gap.ReplaceLineEndings("\n").Count(c=>c=='\n')<2;
        var row = new List<Placement>();
        foreach (Match marker in Marker().Matches(chapter.PlainText))
        {
            if (!int.TryParse(marker.Groups[1].Value, out var number) || number > chapter.Images.Count) continue;
            int image = number - 1;
            if (row.Count > 0 && (row.Count == 3 || Layout(chapter, image) != Gallery ||
                Layout(chapter, row[0].Image) != Gallery ||
                !Adjacent(chapter.PlainText[row[^1].End..marker.Index])))
            {
                yield return row;
                row = [];
            }
            row.Add(new(marker.Index, marker.Length, image));
        }
        if (row.Count > 0) yield return row;
    }

    // Move only the selected occurrence. Legacy chapters can reuse an illustration.
    public static int Move(Chapter chapter,int index,int? source,int target)
    {
        var marker=At(index);
        var text=chapter.PlainText;
        target=Math.Clamp(target,0,text.Length);
        if(source is int pos){
            if(pos<0 || pos+marker.Length>text.Length || !text.AsSpan(pos,marker.Length).SequenceEqual(marker))return target;
            if(target>=pos && target<=pos+marker.Length)return pos+marker.Length;
            text=text.Remove(pos,marker.Length);
            if(pos<target)target-=marker.Length;
        }
        chapter.PlainText=text.Insert(target,marker);
        return target+marker.Length;
    }
    public static void Remove(Chapter chapter, int index)
    {
        chapter.Images.RemoveAt(index);
        chapter.ImageCaptions=chapter.ImageCaptions.Where(p=>p.Key!=index).ToDictionary(p=>p.Key>index?p.Key-1:p.Key,p=>p.Value);
        chapter.ImageLayouts=chapter.ImageLayouts.Where(p=>p.Key!=index).ToDictionary(p=>p.Key>index?p.Key-1:p.Key,p=>p.Value);
        chapter.PlainText = Marker().Replace(chapter.PlainText, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var number)) return match.Value;
            if (number == index + 1) return "";
            return number > index + 1 ? At(number - 2) : match.Value;
        });
    }
}
