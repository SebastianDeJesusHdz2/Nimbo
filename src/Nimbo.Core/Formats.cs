namespace Nimbo.Core;

public sealed record Format(string Name, string Section, string[] Initial, string Paper)
{
    public static readonly Format[] All = [
        new("Novela", "Capítulo", ["Capítulo 1"], "A5"),
        new("Documento", "Sección", ["Primera sección"], "A4"),
        new("Informe", "Sección", ["Síntesis", "Antecedentes", "Análisis", "Conclusiones"], "A4"),
        new("Artículo", "Apartado", ["Resumen", "Introducción", "Método", "Resultados", "Referencias"], "A4"),
        new("Apuntes", "Lección", ["Primera lección"], "A4"),
        new("CV", "Entrada", ["Perfil", "Experiencia", "Formación", "Competencias"], "A4"),
        new("Carta", "Párrafo", ["Saludo", "Mensaje", "Despedida"], "A4"),
        new("Propuesta", "Bloque", ["Objetivo", "Alcance", "Calendario", "Recursos"], "A4"),
        new("Acta", "Punto", ["Asistentes", "Orden del día", "Acuerdos", "Seguimiento"], "A4"),
        new("Presentación", "Diapositiva", ["Portada", "Idea principal", "Conclusiones"], "16:9")
    ];
    public override string ToString() => Name;
    public Work Create(string title) => new()
    {
        Title = title.Trim(),
        Type = Name,
        Publication = new() { Paper = Paper },
        Chapters = Initial.Select((label, i) => new Chapter { Title = label, Order = i }).ToList()
    };
}
