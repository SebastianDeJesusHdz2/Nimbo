namespace Nimbo.Core;

public static class Dossiers
{
    public static readonly string[] Papers = ["A6", "Bolsillo", "B6", "A5", "Carta", "A4"];
    public static readonly string[] Kinds =
    [
        "Personaje",
        "Lugar",
        "Mundo",
        "Raza",
        "Organización",
        "Objeto",
        "Evento",
        "Sistema o magia",
        "Criatura o flora",
        "Categoría",
        "Concepto"
    ];

    public static (int Width, int Height) Size(string paper) => paper switch
    {
        "Bolsillo" => (90, 140),
        "B6" => (125, 176),
        "A5" => (148, 210),
        "Carta" => (216, 279),
        "A4" => (210, 297),
        _ => (105, 148)
    };

    public static IEnumerable<string> Prompts(Node node)
    {
        string[] labels = node.NodeKind.ToLowerInvariant() switch
        {
            "personaje" => ["Deseo y motivación", "Conflicto", "Evolución", "Voz y gestos", "Apariencia y vestimenta"],
            "raza" or "criatura o flora" => ["Rasgos físicos", "Ciclo de vida", "Costumbres", "Hábitat y ecología"],
            "lugar" or "mundo" => ["Paisaje y clima", "Población y cultura", "Lugares de interés", "Historia local"],
            "organización" => ["Propósito", "Integrantes", "Recursos e influencia", "Jerarquía y símbolos"],
            "objeto" => ["Aspecto", "Procedencia", "Uso y límites", "Portadores conocidos"],
            "evento" => ["Antecedentes y causas", "Desarrollo del suceso", "Consecuencias", "Testimonios y memoria"],
            "sistema o magia" => ["Principios y fuente", "Reglas y límites", "Costos o riesgos", "Practicantes"],
            _ => ["Descripción", "Origen", "Papel en la obra"]
        };
        return labels.Concat(node.Sections.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static (string Name, string Unit, string DefaultValue)[] SuggestedAttributes(string kind) => kind.ToLowerInvariant() switch
    {
        "personaje" =>
        [
            ("Arquetipo o rol", "", "Protagonista"),
            ("Afiliación o gremio", "", ""),
            ("Origen", "", ""),
            ("Especialidad o don", "", "")
        ],
        "lugar" or "mundo" =>
        [
            ("Clima dominante", "", "Templado"),
            ("Población estimada", "hab.", ""),
            ("Gobierno o custodia", "", ""),
            ("Nivel de riesgo", "", "Moderado")
        ],
        "raza" or "criatura o flora" =>
        [
            ("Esperanza de vida", "años", ""),
            ("Hábitat principal", "", ""),
            ("Lengua o comunicación", "", ""),
            ("Rasgo distintivo", "", "")
        ],
        "organización" =>
        [
            ("Sede principal", "", ""),
            ("Liderazgo", "", ""),
            ("Alcance o influencia", "", "Regional"),
            ("Lema o divisa", "", "")
        ],
        "objeto" =>
        [
            ("Material principal", "", ""),
            ("Rareza o categoría", "", "Único"),
            ("Custodio actual", "", ""),
            ("Condición de uso", "", "")
        ],
        "evento" =>
        [
            ("Fecha o era", "", ""),
            ("Escenario principal", "", ""),
            ("Facciones involucradas", "", ""),
            ("Impacto histórico", "", "")
        ],
        "sistema o magia" =>
        [
            ("Fuente o catalizador", "", ""),
            ("Costo de activación", "", ""),
            ("Rango o límite", "", ""),
            ("Disciplina", "", "")
        ],
        _ =>
        [
            ("Categoría", "", ""),
            ("Referencia clave", "", ""),
            ("Importancia narrativa", "", "Alta")
        ]
    };

    public static List<Chapter> MentioningChapters(Work work, Node node)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(node.Name) && !node.Name.EndsWith("sin nombre", StringComparison.OrdinalIgnoreCase))
            terms.Add(node.Name.Trim());
        foreach (var alias in node.Aliases.Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (alias.Length >= 3) terms.Add(alias);
        }
        if (terms.Count == 0) return [];
        var matches = new List<Chapter>();
        foreach (var ch in WorldModel.Ordered(work))
        {
            var haystack = ch.Title + "\n" + ch.PlainText + "\n" + ch.Summary + "\n" + ch.AuthorMemo;
            if (terms.Any(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase)))
                matches.Add(ch);
        }
        return matches;
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
