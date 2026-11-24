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
                    new("Currículum académico","Académica",["Perfil investigador","Formación","Docencia","Investigación","Publicaciones","Ponencias","Becas y reconocimientos"]),
                    new("Perfil creativo y técnico","Moderna",["Perfil profesional","Proyectos","Experiencia laboral","Formación","Herramientas y tecnologías", "Certificaciones"])],
            "Informe" => [standard,
                    new("Informe ejecutivo", "Moderna", ["Resumen ejecutivo", "Objetivos", "Indicadores", "Hallazgos", "Recomendaciones", "Próximos pasos"]),
                    new("Dossier de arte conceptual", "Moderna", ["Diseño conceptual del mundo", "Landmarks y regiones", "Moodboards y paletas", "Arte conceptual final", "Control de entrega"]),
                    new("Reporte técnico experimental", "Académica", ["Resumen", "Arquitectura y topología", "Desarrollo experimental", "Pruebas y resultados", "Conclusión", "Bibliografía"])],
            "Artículo" => [standard,
                    new("Artículo de revisión", "Académica", ["Resumen", "Pregunta de revisión", "Criterios de selección", "Síntesis de evidencia", "Limitaciones", "Referencias"]),
                    new("Estudio técnico aplicado", "Moderna", ["Resumen", "Introducción", "Arquitectura del sistema", "Evaluación experimental", "Conclusiones", "Referencias"]),
                    new("Ensayo crítico", "Clásica", ["Planteamiento", "Marco conceptual", "Análisis comparativo", "Conclusiones", "Fuentes"])],
            "Apuntes" => [standard,
                    new("Cuaderno de prácticas", "Moderna", ["Objetivo", "Materiales", "Procedimiento", "Observaciones", "Reflexión"]),
                    new("Libreta Cornell de cátedra", "Académica", ["Encuadre y criterios", "Principios fundamentales", "Análisis de referentes", "Síntesis de unidad"]),
                    new("Bitácora de taller", "Moderna", ["Sesión 1: Fundamentos", "Sesión 2: Exploración visual", "Sesión 3: Retroalimentación y ajustes"])],
            "Acta" => [standard,
                    new("Comité de proyecto", "Moderna", ["Participantes", "Revisión de compromisos", "Decisiones", "Acciones y fechas"]),
                    new("Dictamen de revisión técnica", "Académica", ["Quórum y apertura", "Entregables evaluados", "Observaciones técnicas", "Acuerdos y firmas"]),
                    new("Minuta de seguimiento", "Moderna", ["Contexto de la reunión", "Avances por área", "Bloqueos detectados", "Compromisos"])],
            "Carta" => [standard,
                    new("Solicitud formal", "Clásica", ["Saludo", "Motivo", "Solicitud", "Despedida"]),
                    new("Oficio institucional", "Moderna", ["Antecedentes", "Exposición de motivos", "Petición formal", "Cierre y firmas"]),
                    new("Carta de presentación", "Clásica", ["Apertura", "Trayectoria y aportación", "Propuesta de colaboración", "Despedida"])],
            "Propuesta" => [standard,
                    new("Oferta de servicios", "Moderna", ["Necesidad", "Solución", "Entregables", "Calendario", "Inversión", "Condiciones"]),
                    new("Documento de diseño (GDD)", "Moderna", ["Contexto de la IP y USP", "Mecánicas base", "Cartografía y nivel", "Walkthrough táctico", "Jefe o prueba principal"]),
                    new("Proyecto de ingeniería", "Académica", ["Definición del problema", "Arquitectura propuesta", "Plan de implementación", "Matriz de riesgos", "Presupuesto"])],
            "Presentación" => [standard,
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
                    new("Revisión de arquitectura de software", "Moderna", ["Portada", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Matriz de riesgos y seguridad", "Tabla visual", "Checklist de validación", "Cita o conclusión destacada"])],
            "Documento" => [standard,
                    new("Guía de trabajo", "Moderna", ["Finalidad", "Preparación", "Instrucciones", "Comprobación"]),
                    new("Manual de estándares", "Moderna", ["Alcance y objetivos", "Especificaciones técnicas", "Criterios de validación", "Anexos"]),
                    new("Memoria descriptiva", "Clásica", ["Introducción", "Descripción general", "Desarrollo por componentes", "Conclusiones"])],
            _ => [standard,
                    new("Novela por arcos", "Clásica", ["Primer arco", "Capítulo 1"]),
                    new("Estructura en tres actos", "Clásica", ["Acto I: El umbral", "Capítulo 1", "Acto II: El descenso", "Acto III: El retorno"])]
        };
    }
    public Work Create(Format format, string title)
    {
        var work = format.Create(title);
        work.Template = Name;
        work.Chapters = Sections.Select((s, i) => new Chapter { Title = s, Order = i }).ToList();
        if (Name is "Novela por arcos" or "Estructura en tres actos") { work.Chapters[0].Level = "Arc"; if (work.Chapters.Count > 1) work.Chapters[1].ParentId = work.Chapters[0].Id; }
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
