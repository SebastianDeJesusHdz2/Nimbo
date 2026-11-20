namespace Nimbo.Core;

public sealed record EditorialTemplate(string Name, string Style, string[] Sections)
{
    public override string ToString() => Name;
    public static EditorialTemplate[] For(Format f)
    {
        EditorialTemplate standard = new("Clásica", "Clásica", f.Initial);
        return f.Name switch
        {
            "CV" => [new("Trayectoria profesional","Cronológica",["Perfil","Experiencia laboral","Formación","Competencias","Idiomas","Certificaciones"]),
                    new("Primera experiencia","Moderna",["Perfil","Formación","Proyectos","Prácticas y voluntariado","Competencias","Idiomas"]),
                    new("Currículum académico","Académica",["Perfil investigador","Formación","Docencia","Investigación","Publicaciones","Ponencias","Becas y reconocimientos"])],
            "Informe" => [standard, new("Informe ejecutivo", "Moderna", ["Resumen ejecutivo", "Objetivos", "Indicadores", "Hallazgos", "Recomendaciones", "Próximos pasos"])],
            "Artículo" => [standard, new("Artículo de revisión", "Académica", ["Resumen", "Pregunta de revisión", "Criterios de selección", "Síntesis de evidencia", "Limitaciones", "Referencias"])],
            "Apuntes" => [standard, new("Cuaderno de prácticas", "Moderna", ["Objetivo", "Materiales", "Procedimiento", "Observaciones", "Reflexión"])],
            "Acta" => [standard, new("Comité de proyecto", "Moderna", ["Participantes", "Revisión de compromisos", "Decisiones", "Acciones y fechas"])],
            "Carta" => [standard, new("Solicitud formal", "Clásica", ["Saludo", "Motivo", "Solicitud", "Despedida"])],
            "Propuesta" => [standard, new("Oferta de servicios", "Moderna", ["Necesidad", "Solución", "Entregables", "Calendario", "Inversión", "Condiciones"])],
            "Presentación" => [standard, new("Presentación de proyecto", "Moderna", ["Portada", "Problema", "Propuesta", "Evidencias", "Calendario", "Cierre"])],
            "Documento" => [standard, new("Guía de trabajo", "Moderna", ["Finalidad", "Preparación", "Instrucciones", "Comprobación"])],
            _ => [standard, new("Novela por arcos", "Clásica", ["Primer arco", "Capítulo 1"])]
        };
    }
    public Work Create(Format format, string title)
    {
        var work = format.Create(title);
        work.Template = Name;
        work.Chapters = Sections.Select((s, i) => new Chapter { Title = s, Order = i }).ToList();
        if (Name == "Novela por arcos") { work.Chapters[0].Level = "Arc"; work.Chapters[1].ParentId = work.Chapters[0].Id; }
        return work;
    }
    public static string[] FrontMatter(string type) => type switch
    {
        "Carta" => ["Destinatario", "Dirección", "Lugar y fecha", "Asunto"],
        "Acta" => ["Fecha de reunión", "Lugar", "Participantes", "Secretaría"],
        "Informe" => ["Institución", "Departamento", "Periodo del informe"],
        "Artículo" => ["Afiliación", "Palabras clave", "Correspondencia"],
        "Apuntes" => ["Asignatura", "Docente", "Periodo"],
        "Propuesta" => ["Cliente", "Responsable", "Vigencia", "Presupuesto"],
        "Presentación" => ["Institución", "Evento", "Fecha"],
        "Documento" => ["Organización", "Referencia"],
        _ => []
    };
    public static string DetailLabel(string type) => type switch
    {
        "Acta" => "Responsable y fecha límite",
        "CV" => "Periodo y ubicación",
        "Apuntes" => "Preguntas y conceptos",
        "Informe" => "Indicador o dato destacado",
        "Artículo" => "Fuente o referencia",
        "Carta" => "Referencia",
        "Propuesta" => "Plazo y responsable",
        "Documento" => "Nota de sección",
        _ => "Texto secundario"
    };
}
public sealed class ResumeEntry
{
    public string Heading { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Period { get; set; } = "";
    public string Description { get; set; } = "";
}

