using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace Nimbo.Core;

public class Extensible { [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; } }
public class Library : Extensible
{
    public int Version { get; set; } = 4;
    public Profile Profile { get; set; } = new(); public Preferences Settings { get; set; } = new();
    public List<Collection> Collections { get; set; } = []; public List<Work> Projects { get; set; } = [];
}
public class Profile : Extensible { public string Name { get; set; } = ""; public string PenName { get; set; } = ""; public string Bio { get; set; } = ""; public string ImagePath { get; set; } = ""; }
public class Preferences : Extensible
{
    public double MusicVolume { get; set; } = .35; public string InterfaceFont { get; set; } = "Segoe UI"; public string HeadingFont { get; set; } = "Georgia"; public string SurfaceTone { get; set; } = "Lino"; public bool CompactControls { get; set; }
    public string AccentHex { get; set; } = "";
    public string Theme { get; set; } = "Océano"; public string LibraryBackgroundImage { get; set; } = ""; public double BackgroundBlur { get; set; } = 5; public double BackgroundShade { get; set; } = 55;
}
public class Collection : Extensible { public string Id { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } = "Colección"; public string Description { get; set; } = ""; public string Color { get; set; } = "#315F65"; }
public sealed record EditionFile(string Name, string Key);
public class Work : Extensible
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } = "Sin título"; public string Type { get; set; } = "Historia";
    public string Author { get; set; } = ""; public string Subtitle { get; set; } = ""; public string Description { get; set; } = ""; public string CollectionId { get; set; } = "";
    public string CoverImage { get; set; } = ""; public string CoverColor { get; set; } = "#315F65"; public string Genre { get; set; } = ""; public string Status { get; set; } = "En progreso";
    public string WritingPaper { get; set; } = "Marfil"; public int WritingWidth { get; set; } = 900; public bool FocusWriting { get; set; }
    public bool CheckSpelling { get; set; }
    public double WritingPadding { get; set; } = 40; public int WritingZoom { get; set; } = 100; public string DeskTone { get; set; } = "Niebla"; public bool ShowReadingTime { get; set; } = true;
    public string GlossaryLayout { get; set; } = "Índice"; public string GlossaryOrder { get; set; } = "Nombre"; public bool OnlyPinned { get; set; }
    public string GlossaryPaper { get; set; } = "A6";
    public int WordGoal { get; set; } = 50000; public string CreatedAt { get; set; } = DateTime.Now.ToString("o"); public string? ModifiedAt { get; set; }
    public string Template {get;set;}="";
    public Dictionary<string,string> Contact {get;set;}=[];
    public Dictionary<string,string> DocumentFacts {get;set;}=[];
    public string PrintSpacing {get;set;}="Normal";
    public string PrintIndent {get;set;}="Automática";
    public string PrintAccent {get;set;}="";
    public List<EditionFile> Editions { get; set; } = [];
    public List<Chapter> Chapters { get; set; } = []; public List<Node> Glossary { get; set; } = [];
    public Publication Publication { get; set; } = new(); public bool IsDemo { get; set; }
    public string LastChapterId { get; set; } = "";
    public List<TextStyleSpec> TextStyles { get; set; } = [];
    [JsonIgnore] public bool IsNovel => Type is "Historia" or "Novela";
}
public class Chapter : Extensible
{
    public override string ToString() => Title;
    public string Id { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } = "Capítulo 1"; public string Level { get; set; } = "Chapter"; public string ParentId { get; set; } = "";
    public int Order { get; set; }
    public string Summary { get; set; } = ""; public string ContentPackage { get; set; } = ""; public string PlainText { get; set; } = ""; public string LatexContent { get; set; } = "";
    public string RevisionState { get; set; } = "Borrador"; public string AuthorMemo { get; set; } = ""; public int SectionGoal { get; set; }
    public string Detail { get; set; } = ""; public string Layout { get; set; } = "Texto"; public List<string> Images { get; set; } = []; public Dictionary<int,string> ImageLayouts { get; set; } = []; public string BodyStyle { get; set; } = "Normal";
    public List<ResumeEntry> Entries {get;set;}=[];
    public int CaretOffset { get; set; }
    public Dictionary<int,string> ImageCaptions { get; set; } = [];
}
public class TextStyleSpec { public string Id { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } = "Estilo propio"; public string Font { get; set; } = "Georgia"; public double SizePt { get; set; } = 12; public double LineSpacing { get; set; } = 1.5; public double IndentMm { get; set; } = 0; public double BeforePt { get; set; } = 0; public double AfterPt { get; set; } = 8; public bool Bold { get; set; } public bool Italic { get; set; } public string Alignment { get; set; } = "Justificar"; public override string ToString() => Name; }
public class Node : Extensible
{
    public bool Pinned { get; set; }
    public Dictionary<string,string> Sections { get; set; } = [];
    public override string ToString() => Name;
    public string Id { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } = "Nueva ficha"; public string NodeKind { get; set; } = "Concepto";
    public string ParentId { get; set; } = ""; public string BaseId { get; set; } = ""; public string ImagePath { get; set; } = "";
    public string Type { get; set; } = ""; public string Age { get; set; } = ""; public string Measures { get; set; } = ""; public string Aliases { get; set; } = "";
    public string Background { get; set; } = ""; public string Notes { get; set; } = ""; public string Relations { get; set; } = ""; public string Tags { get; set; } = ""; public string Role { get; set; } = ""; public string Status { get; set; } = "Activo";
    public List<FieldDefinition> AttributeDefinitions { get; set; } = []; public List<FieldValue> Attributes { get; set; } = []; public List<string> RelatedIds { get; set; } = []; public Dictionary<string, string> RelationRoles { get; set; } = [];
}
public class FieldDefinition : Extensible
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } = "Nuevo campo"; public string Type { get; set; } = "Texto";
    public string DefaultValue { get; set; } = ""; public string Description { get; set; } = ""; public string Unit { get; set; } = ""; public string Group { get; set; } = "General"; public string Options { get; set; } = "";
}
public class FieldValue : Extensible { public string DefinitionId { get; set; } = ""; public string Key { get; set; } = ""; public string Value { get; set; } = ""; public string SourceNodeId { get; set; } = ""; }
public record EffectiveField(FieldDefinition Definition, string Value, Node Origin, bool Inherited, bool Overridden);
public class Publication : Extensible
{
    public string Paper { get; set; } = "A5"; public double MarginCm { get; set; } = 2; public double TopMm { get; set; } = 20; public double BottomMm { get; set; } = 20;
    public double InnerMm { get; set; } = 22; public double OuterMm { get; set; } = 18; public double GutterMm { get; set; } = 4;
    public double WidthMm { get; set; } = 148; public double HeightMm { get; set; } = 210; public bool Duplex { get; set; }
    public bool OpenRight { get; set; }
    public string RunningHead { get; set; } = "Sin encabezado"; public bool BackCover { get; set; } = true; public bool TitlePage { get; set; } = true; public bool Contents { get; set; } = true; public bool Glossary { get; set; }
    public bool PageNumbers { get; set; } = true;
    public bool RomanPrelims { get; set; } = true; public string Header { get; set; } = "";
    public (double W, double H) Size() => Paper switch { "A4" => (210, 297), "Carta" => (215.9, 279.4), "A5" => (148, 210), _ => (WidthMm, HeightMm) };
}
public static class WorldModel
{
    public static bool Geographic(Node n) => n.NodeKind is "Mundo" or "Lugar";
    public static List<EffectiveField> Effective(Work w, Node node)
    {
        var index = w.Glossary.ToDictionary(n => n.Id); var seen = new HashSet<string>(); var sources = new List<Node>();
        void Visit(Node n)
        {
            if (!seen.Add(n.Id)) return;
            if (Geographic(n)) { var parent = n.ParentId; var chain = new HashSet<string>(); while (index.TryGetValue(parent, out var p) && chain.Add(parent)) { if (Geographic(p)) { Visit(p); break; } parent = p.ParentId; } }
            if (index.TryGetValue(n.BaseId, out var b)) Visit(b); sources.Add(n);
        }
        Visit(node);
        var fields = new Dictionary<string, EffectiveField>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var def in source.AttributeDefinitions.Where(d => !string.IsNullOrWhiteSpace(d.Name))) fields[def.Name.Trim()] = new(def, def.DefaultValue, source, source.Id != node.Id, false);
            // A territory's override must propagate to geographic descendants.
            foreach (var val in source.Attributes)
            {
                var field = fields.Values.FirstOrDefault(f => val.DefinitionId != "" ? f.Definition.Id == val.DefinitionId : f.Definition.Name.Equals(val.Key, StringComparison.OrdinalIgnoreCase));
                if (field != null) fields[field.Definition.Name.Trim()] = field with { Value = val.Value, Overridden = source.Id == node.Id, Origin = source.Id == node.Id ? field.Origin : source };
                else if (source.Id == node.Id && val.DefinitionId == "" && val.Key != "") fields[val.Key] = new(new() { Id = "own:" + val.Key, Name = val.Key }, val.Value, node, false, true);
            }
        }
        return fields.Values.ToList();
    }
    public static bool WouldCycle(Work w, string nodeId, string parentId, string baseId)
    {
        var index = w.Glossary.ToDictionary(n => n.Id); var stack = new Stack<string>(); stack.Push(parentId); stack.Push(baseId); var seen = new HashSet<string>();
        while (stack.TryPop(out var id)) { if (id == nodeId) return true; if (seen.Add(id) && index.TryGetValue(id, out var n)) { stack.Push(n.ParentId); stack.Push(n.BaseId); } }
        return false;
    }
    public static bool ValidValue(FieldDefinition f, string v) => v == "" || f.Type switch
    {
        "Número" => double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && double.IsFinite(n),
        "Fecha" => DateTime.TryParse(v, out _),
        "Sí/No" => new[] { "true", "false", "sí", "no", "si" }.Contains(v.ToLowerInvariant()),
        "Opciones" => f.Options.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(v),
        _ => true
    };
    public static IEnumerable<Chapter> Ordered(Work w)
    {
        var done = new HashSet<string>(); var result = new List<Chapter>(); void Visit(Chapter c) { if (!done.Add(c.Id)) return; result.Add(c); foreach (var child in w.Chapters.Where(n => n.ParentId == c.Id).OrderBy(n => n.Order)) Visit(child); }
        foreach (var c in w.Chapters.Where(c => c.ParentId == "").OrderBy(c => c.Order)) Visit(c); foreach (var c in w.Chapters.OrderBy(c => c.Order)) Visit(c); return result;
    }
}






