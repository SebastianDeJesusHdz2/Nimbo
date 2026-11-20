using System.Xml.Linq;
using Nimbo.Core;

namespace Nimbo.Storage;

internal static class LegacyText
{
    // Read FlowDocument as data. Keep the original package beside the editable representation.
    public static string Read(XElement root, Dictionary<XElement, int> pictures)
    {
        string Visit(XNode node)
        {
            if (node is XText text) return text.Value;
            if (node is not XElement el) return "";
            var kind = el.Name.LocalName;
            if (kind.Contains('.') || kind is "BitmapImage" or "TableColumn") return "";
            if (kind == "Image") return pictures.TryGetValue(el, out var index) ? "\n" + Illustrations.At(index) + "\n" : "";
            var content = string.Concat(el.Nodes().Select(Visit));
            return kind switch
            {
                "Run" => Decorate(el, (string?)el.Attribute("Text") ?? content),
                "Bold" => "**" + content + "**",
                "Italic" => "*" + content + "*",
                "LineBreak" => "\n",
                "Paragraph" => content + "\n",
                "ListItem" => "- " + content.TrimEnd() + "\n",
                "TableRow" => "| " + string.Join(" | ", el.Elements().Where(e => e.Name.LocalName == "TableCell")
                    .Select(e => string.Concat(e.Nodes().Select(Visit)).Trim().ReplaceLineEndings(" "))) + " |\n",
                _ => content
            };
        }
        return Visit(root).TrimEnd();
    }
    static string Decorate(XElement el, string text)
    {
        if (text.Length == 0) return text;
        if ((string?)el.Attribute("FontWeight") == "Bold") return "**" + text + "**";
        if ((string?)el.Attribute("FontStyle") == "Italic") return "*" + text + "*";
        return text;
    }
    public static bool SameWords(string previous, string candidate)
    {
        static string Letters(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
        return Letters(previous) == Letters(Illustrations.Marker().Replace(candidate, ""));
    }
}
