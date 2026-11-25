namespace Nimbo.Core;

public sealed record EditorialTemplate(string Name, string Style, string[] Sections)
{
    public override string ToString() => Name;

    public static readonly string[] NovelCoverStyles =
    [
        "Al pie",
        "Centrada",
        "Cabecera",
        "Tejuelo clásico",
        "Marco áureo",
        "Banda completa"
    ];

    public static bool HasDedicatedCover(string type) =>
        type is "Novela" or "Apuntes" or "Informe";

    public static EditorialTemplate[] For(Format f)
    {
        EditorialTemplate standard = new("Clásica", "Clásica", f.Initial);
        return f.Name switch
        {
            "Novela" => [standard],
            "CV" =>
            [
                new("Trayectoria profesional", "Cronológica", ["Perfil", "Experiencia laboral", "Formación", "Competencias", "Idiomas", "Certificaciones"]),
                new("Primera experiencia", "Moderna", ["Perfil", "Formación", "Proyectos", "Prácticas y voluntariado", "Competencias", "Idiomas"]),
                new("Currículum académico", "Académica", ["Perfil investigador", "Formación", "Docencia", "Investigación", "Publicaciones", "Ponencias", "Becas y reconocimientos"]),
                new("Perfil creativo y técnico", "Moderna", ["Perfil profesional", "Proyectos", "Experiencia laboral", "Formación", "Herramientas y tecnologías", "Certificaciones"]),
                new("Ingeniería de software y sistemas", "Ejecutiva", ["Resumen técnico", "Experiencia laboral", "Proyectos", "Formación", "Stack tecnológico", "Certificaciones"]),
                new("Dirección de arte y videojuegos", "BandaLateral", ["Perfil artístico", "Proyectos", "Experiencia laboral", "Formación", "Software y motores", "Reconocimientos"]),
                new("Ejecutivo corporativo", "Ejecutiva", ["Resumen ejecutivo", "Experiencia laboral", "Logros clave", "Formación", "Competencias directivas", "Idiomas"]),
                new("Consultoría e investigación", "Académica", ["Síntesis profesional", "Experiencia laboral", "Proyectos", "Formación", "Publicaciones", "Certificaciones"]),
                new("Formato ATS compacto", "Minimalista", ["Perfil", "Experiencia laboral", "Formación", "Proyectos", "Habilidades técnicas", "Idiomas"]),
                new("Portafolio técnico bilingüe", "DosColumnas", ["Perfil", "Experiencia laboral", "Proyectos", "Formación", "Competencias", "Idiomas"])
            ],
            "Apuntes" =>
            [
                standard,
                new("Libreta", "Libreta", ["Tema 1"]),
                new("Cuaderno Cornell", "Cornell", ["Tema 1"])
            ],
            "Informe" =>
            [
                standard,
                new("Reporte", "Reporte", ["Resumen", "Conclusiones", "Bibliografía"]),
                new("Informe ejecutivo", "Moderna", ["Resumen ejecutivo", "Hallazgos", "Recomendaciones"])
            ],
            "Artículo" =>
            [
                standard,
                new("Artículo académico", "Académica", ["Resumen", "Introducción", "Desarrollo", "Conclusiones", "Referencias"])
            ],
            "Propuesta" =>
            [
                standard,
                new("Propuesta de proyecto", "Moderna", ["Contexto", "Alcance", "Entregables", "Presupuesto"])
            ],
            "Acta" =>
            [
                standard,
                new("Acta de reunión", "Clásica", ["Orden del día", "Acuerdos", "Compromisos"])
            ],
            "Carta" =>
            [
                standard,
                new("Solicitud formal", "Clásica", ["Motivo", "Solicitud"])
            ],
            "Documento" =>
            [
                standard,
                new("Guía de trabajo", "Moderna", ["Propósito", "Desarrollo", "Referencias"])
            ],
            "Presentación" =>
            [
                standard,
                new("Presentación de proyecto", "Moderna", ["Portada", "Problema", "Propuesta", "Evidencias", "Calendario", "Cierre"]),
                new("Catálogo de fichas de concepto", "Moderna", ["Portada", "Concepto 1", "Concepto 2", "Concepto 3"]),
                new("Arquitectura visual y HUD", "Moderna", ["Portada", "Matriz de interfaz (HUD)", "Moodboard y paleta", "Plano y leyenda", "Escena protagonista"]),
                new("Pitch ejecutivo y KPIs", "Moderna", ["Portada", "Oportunidad y contexto", "Propuesta en 3 pilares", "Métricas e indicadores (KPIs)", "Comparativa (Antes / Después)", "Línea de tiempo / Roadmap", "Conclusión ejecutiva"]),
                new("Dossier de dirección de arte", "Moderna", ["Portada", "Pilares estéticos", "Moodboard y paleta", "Galería triple de entorno", "Cuadrícula 2x2 de referencias", "Lámina protagonista"]),
                new("Defensa técnica / Académica", "Académica", ["Portada", "Planteamiento y objetivos", "Arquitectura en 3 capas", "Tabla de evaluación", "Resultados comparativos", "Conclusiones"]),
                new("Roadmap y estrategia de producto", "Moderna", ["Portada", "Diagnóstico actual", "Matriz 2x2 de prioridades", "Roadmap de entregas", "Indicadores de éxito", "Acuerdos de cierre"]),
                new("Auditoría de seguridad y riesgos", "Moderna", ["Portada", "Divisor de sección / Impacto", "Matriz de riesgos y seguridad", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Checklist de validación", "Cierre y contacto"]),
                new("Lanzamiento y Go-To-Market", "Moderna", ["Portada", "Embudo o pirámide estratégica", "Cuatro columnas", "Ficha de personaje / Equipo", "Línea de tiempo / Roadmap", "Métricas e indicadores (KPIs)", "Cierre y contacto"]),
                new("Biblia de mundo y personajes", "Moderna", ["Portada", "Divisor de sección / Impacto", "Ficha de concepto", "Ficha de personaje / Equipo", "Moodboard y paleta", "Plano y leyenda", "Galería triple"]),
                new("Revisión de arquitectura de software", "Moderna", ["Portada", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Matriz de riesgos y seguridad", "Tabla visual", "Checklist de validación", "Cita o conclusión destacada"])
            ],
            _ => [standard]
        };
    }

    public Work Create(Format format, string title)
    {
        var work = format.Create(title);
        work.Template = Name;
        work.Chapters = Sections.Select((s, i) => new Chapter { Title = s, Order = i }).ToList();
        if (format.Name == "Novela")
        {
            work.CoverStyle = NovelCoverStyles[0];
            work.Publication.Contents = true;
            work.Publication.BackCover = true;
        }
        else if (Style == "Libreta")
        {
            work.Publication.Paper = "Carta";
            work.Publication.Contents = true;
            work.Publication.PageNumbers = true;
        }
        else if (Style == "Reporte")
        {
            work.Publication.Paper = "Carta";
            work.Publication.Contents = true;
            work.Publication.PageNumbers = true;
        }
        if (format.Name == "Presentación")
        {
            if (Name == "Catálogo de fichas de concepto")
            {
                work.Chapters[0].Layout = "Portada";
                for (int i = 1; i < work.Chapters.Count; i++) work.Chapters[i].Layout = "Ficha de concepto";
            }
            else if (Name == "Arquitectura visual y HUD")
            {
                string[] layouts = ["Portada", "Tabla visual", "Moodboard y paleta", "Plano y leyenda", "Imagen protagonista"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Pitch ejecutivo y KPIs")
            {
                string[] layouts = ["Portada", "Texto e imagen", "Tres columnas", "Métricas e indicadores (KPIs)", "Comparativa (Antes / Después)", "Línea de tiempo / Roadmap", "Cita o conclusión destacada"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Dossier de dirección de arte")
            {
                string[] layouts = ["Portada", "Tres columnas", "Moodboard y paleta", "Galería triple", "Cuadrícula 2x2", "Imagen protagonista"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Defensa técnica / Académica")
            {
                string[] layouts = ["Portada", "Dos columnas", "Tres columnas", "Tabla visual", "Comparativa (Antes / Después)", "Cita o conclusión destacada"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Roadmap y estrategia de producto")
            {
                string[] layouts = ["Portada", "Dos columnas", "Cuadrícula 2x2", "Línea de tiempo / Roadmap", "Métricas e indicadores (KPIs)", "Cita o conclusión destacada"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Auditoría de seguridad y riesgos")
            {
                string[] layouts = ["Portada", "Divisor de sección / Impacto", "Matriz de riesgos y seguridad", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Checklist de validación", "Cierre y contacto"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Lanzamiento y Go-To-Market")
            {
                string[] layouts = ["Portada", "Embudo o pirámide estratégica", "Cuatro columnas", "Ficha de personaje / Equipo", "Línea de tiempo / Roadmap", "Métricas e indicadores (KPIs)", "Cierre y contacto"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Biblia de mundo y personajes")
            {
                string[] layouts = ["Portada", "Divisor de sección / Impacto", "Ficha de concepto", "Ficha de personaje / Equipo", "Moodboard y paleta", "Plano y leyenda", "Galería triple"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Revisión de arquitectura de software")
            {
                string[] layouts = ["Portada", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Matriz de riesgos y seguridad", "Tabla visual", "Checklist de validación", "Cita o conclusión destacada"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (work.Chapters.Count > 0 && work.Chapters[0].Title == "Portada")
            {
                work.Chapters[0].Layout = "Portada";
            }
        }
        return work;
    }

    public static string[] CoverLayoutsFor(string type) => type switch
    {
        "Novela" => NovelCoverStyles,
        _ => []
    };

    public static EditorialTemplate Resolve(Format? format, string? templateName)
    {
        if (format == null) return new("Clásica", "Clásica", []);
        var templates = For(format);
        if (string.IsNullOrWhiteSpace(templateName)) return templates[0];
        var exact = templates.FirstOrDefault(t => t.Name == templateName);
        if (exact != null) return exact;
        if (format.Name == "Apuntes" && templateName.Contains("Libreta", StringComparison.OrdinalIgnoreCase))
            return templates.First(t => t.Style == "Libreta");
        if (format.Name == "Informe" && (templateName.Contains("Reporte", StringComparison.OrdinalIgnoreCase) || templateName.Contains("Práctica", StringComparison.OrdinalIgnoreCase)))
            return templates.First(t => t.Style == "Reporte");
        return templates[0];
    }

    public static string[] FrontMatter(string type) => type switch
    {
        "Carta" => ["Destinatario", "Dirección", "Lugar y fecha", "Asunto"],
        "Acta" => ["Fecha de reunión", "Lugar", "Participantes", "Secretaría"],
        "Informe" => ["Institución", "Departamento", "Periodo del informe", "Materia o área", "Realizada por", "Profesor", "Alumno", "Clave", "Fecha"],
        "Artículo" => ["Afiliación", "Palabras clave", "Correspondencia"],
        "Apuntes" => ["Universidad", "Facultad", "Asignatura", "Clave de clase", "Docente", "Carrera", "Clave U", "Alumno", "Periodo", "Lugar y fecha"],
        "Propuesta" => ["Cliente", "Responsable", "Vigencia", "Presupuesto"],
        "Presentación" => ["Institución", "Evento", "Fecha"],
        "Documento" => ["Organización", "Referencia"],
        _ => []
    };

    public static string DetailLabel(string type) => type switch
    {
        "Acta" => "Responsable y fecha límite",
        "CV" => "Periodo y ubicación",
        "Apuntes" => "Preguntas o conceptos clave",
        "Informe" => "Alcance o indicador",
        "Artículo" => "Fuente o referencia",
        "Carta" => "Referencia o expediente",
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
