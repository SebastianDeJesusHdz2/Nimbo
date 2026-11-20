using System.Text.RegularExpressions;

namespace Nimbo.Core;

public static partial class Illustrations
{
    [GeneratedRegex(@"\[\[Imagen ([1-9][0-9]*)\]\]", RegexOptions.CultureInvariant)]
    public static partial Regex Marker();
    public static string At(int index) => $"[[Imagen {index + 1}]]";
    public static void Remove(Chapter chapter, int index)
    {
        chapter.Images.RemoveAt(index);
        chapter.PlainText = Marker().Replace(chapter.PlainText, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var number)) return match.Value;
            if (number == index + 1) return "";
            return number > index + 1 ? At(number - 2) : match.Value;
        });
    }
}
