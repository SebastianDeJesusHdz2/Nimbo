namespace Nimbo.Core;

public static class Dossiers
{
    public static readonly string[] Papers = ["A6", "Bolsillo", "B6"];
    public static (int Width, int Height) Size(string paper) => paper switch
    {
        "Bolsillo" => (90, 140), "B6" => (125, 176), _ => (105, 148)
    };
    public static IEnumerable<string> Prompts(Node node)
    {
        string[] labels = node.NodeKind.ToLowerInvariant() switch
        {
            "personaje" => ["Deseo y motivación", "Conflicto", "Evolución", "Voz y gestos"],
            "raza" => ["Rasgos físicos", "Ciclo de vida", "Costumbres"],
            "lugar" or "mundo" => ["Paisaje y clima", "Población y cultura", "Lugares de interés", "Historia local"],
            "organización" => ["Propósito", "Integrantes", "Recursos e influencia"],
            "objeto" => ["Aspecto", "Procedencia", "Uso y límites"],
            _ => ["Descripción", "Origen", "Papel en la obra"]
        };
        return labels.Concat(node.Sections.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<(string Label, string Value)> Details(Work work, Node node)
    {
        var parent=work.Glossary.FirstOrDefault(n=>n.Id==node.ParentId);
        var model=work.Glossary.FirstOrDefault(n=>n.Id==node.BaseId);
        var identity=new[]{("Pertenece a",parent?.Name??""),("Modelo",model?.Name??""),
            ("Clasificación",node.Type),("Alias",node.Aliases),("Estado",node.Status),
            ("Edad o época",node.Age),("Medidas o extensión",node.Measures),("Etiquetas",node.Tags)};
        foreach(var item in identity)if(!string.IsNullOrWhiteSpace(item.Item2))yield return item;
        foreach(var field in WorldModel.Effective(work,node))
        {
            if(string.IsNullOrWhiteSpace(field.Value))continue;
            string label=field.Definition.Name;
            if(field.Definition.Unit!="")label+=" ("+field.Definition.Unit+")";
            if(field.Inherited)label+=" · "+field.Origin.Name;
            yield return (label,field.Value);
        }
    }
    public static IEnumerable<(Node Target,string Label)> Links(Work work,Node node)
    {
        foreach(var n in work.Glossary.Where(n=>n!=node))
        {
            var roles=new List<string>();
            if(n.ParentId==node.Id)roles.Add("Contiene");
            if(node.RelatedIds.Contains(n.Id))roles.Add(node.RelationRoles.GetValueOrDefault(n.Id,"Vínculo"));
            if(n.RelatedIds.Contains(node.Id))roles.Add("Vínculo entrante · "+n.RelationRoles.GetValueOrDefault(node.Id,"Relacionado"));
            if(roles.Count>0)yield return (n,string.Join(" / ",roles));
        }
    }
}
