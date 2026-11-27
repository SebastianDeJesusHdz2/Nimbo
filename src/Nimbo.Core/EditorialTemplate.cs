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
                new("Cuaderno Cornell", "Cornell", ["Tema 1"]),
                new("Guía de estudio y examen", "Moderna", ["Conceptos clave", "Desarrollo de temas", "Formulario o reglas", "Preguntas de repaso"]),
                new("Bitácora de laboratorio", "Académica", ["Objetivo y materiales", "Registro de observaciones", "Análisis y notas", "Conclusiones"])
            ],
            "Informe" =>
            [
                standard,
                new("Reporte", "Reporte", ["Resumen", "Conclusiones", "Bibliografía"]),
                new("Informe ejecutivo", "Moderna", ["Resumen ejecutivo", "Hallazgos", "Recomendaciones"]),
                new("Diagnóstico técnico", "Ejecutiva", ["Alcance y metodología", "Estado actual", "Hallazgos críticos", "Plan de acción"]),
                new("Memoria de proyecto", "Académica", ["Resumen", "Objetivos alcanzados", "Actividades realizadas", "Entregables y métricas", "Siguientes pasos"])
            ],
            "Artículo" =>
            [
                standard,
                new("Artículo académico", "Académica", ["Resumen", "Introducción", "Desarrollo", "Conclusiones", "Referencias"]),
                new("Artículo científico (IMRyD)", "Académica", ["Resumen", "Introducción", "Metodología", "Resultados", "Discusión", "Referencias"]),
                new("Ensayo crítico", "Clásica", ["Planteamiento", "Argumentación", "Contraargumentos", "Síntesis", "Bibliografía"]),
                new("Artículo de divulgación", "Moderna", ["Síntesis", "Contexto", "Análisis principal", "Perspectivas", "Lecturas recomendadas"])
            ],
            "Propuesta" =>
            [
                standard,
                new("Propuesta de proyecto", "Moderna", ["Contexto", "Alcance", "Entregables", "Presupuesto"]),
                new("Documento de diseño (GDD)", "Ejecutiva", ["Visión y pilares", "Mecánicas y sistemas", "Diseño de nivel o mundo", "Interfaz y arte", "Hitos de producción"]),
                new("Cotización y servicios", "Ejecutiva", ["Alcance del servicio", "Fases y entregables", "Inversión y desglose", "Condiciones comerciales"]),
                new("Protocolo de investigación", "Académica", ["Planteamiento del problema", "Justificación y objetivos", "Marco teórico", "Metodología", "Cronograma", "Referencias"])
            ],
            "Acta" =>
            [
                standard,
                new("Acta de reunión", "Clásica", ["Orden del día", "Acuerdos", "Compromisos"]),
                new("Minuta ejecutiva", "Moderna", ["Resumen de la sesión", "Avances revisados", "Bloqueos y riesgos", "Acciones y responsables"]),
                new("Dictamen de comité", "Ejecutiva", ["Antecedentes", "Considerandos", "Dictamen resolutivo", "Firmas de conformidad"])
            ],
            "Carta" =>
            [
                standard,
                new("Solicitud formal", "Clásica", ["Motivo", "Solicitud"]),
                new("Carta de presentación", "Moderna", ["Presentación y propósito", "Trayectoria y aportación", "Despedida"]),
                new("Oficio institucional", "Ejecutiva", ["Asunto y fundamento", "Disposiciones", "Atentamente"]),
                new("Carta de recomendación", "Clásica", ["Relación y contexto", "Cualidades y desempeño", "Aval y contacto"])
            ],
            "Documento" =>
            [
                standard,
                new("Guía de trabajo", "Moderna", ["Propósito", "Desarrollo", "Referencias"]),
                new("Manual de procedimientos (SOP)", "Ejecutiva", ["Objetivo y alcance", "Responsabilidades", "Procedimiento paso a paso", "Control y verificación"]),
                new("Especificación técnica", "Académica", ["Resumen técnico", "Arquitectura y requisitos", "Casos de uso", "Validación y pruebas"]),
                new("Convenio o contrato", "Clásica", ["Declaraciones", "Cláusulas y obligaciones", "Vigencia y cierre"])
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
                new("Revisión de arquitectura de software", "Moderna", ["Portada", "Arquitectura en capas", "Flujo de proceso (Pipeline)", "Matriz de riesgos y seguridad", "Tabla visual", "Checklist de validación", "Cita o conclusión destacada"]),
                new("Presentación de clase / Cátedra", "Académica", ["Portada", "Agenda de la sesión", "Conceptos fundamentales", "Comparativa de enfoques", "Caso práctico", "Síntesis y preguntas"]),
                new("Propuesta comercial y presupuesto", "Ejecutiva", ["Portada", "Diagnóstico del cliente", "Solución en 3 pilares", "Cronograma de ejecución", "Inversión y KPIs", "Cierre y contacto"])
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
            else if (Name == "Presentación de clase / Cátedra")
            {
                string[] layouts = ["Portada", "Agenda / Índice", "Tres columnas", "Comparativa (Antes / Después)", "Texto e imagen", "Cita o conclusión destacada"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (Name == "Propuesta comercial y presupuesto")
            {
                string[] layouts = ["Portada", "Dos columnas", "Tres columnas", "Línea de tiempo / Roadmap", "Métricas e indicadores (KPIs)", "Cierre y contacto"];
                for (int i = 0; i < work.Chapters.Count && i < layouts.Length; i++) work.Chapters[i].Layout = layouts[i];
            }
            else if (work.Chapters.Count > 0 && work.Chapters[0].Title == "Portada")
            {
                work.Chapters[0].Layout = "Portada";
            }
        }
        return work;
    }

    public static readonly string[] DocumentMiniatureStyles =
    [
        "Automática",
        "Ejecutiva",
        "Académica",
        "Minimalista",
        "Tejuelo",
        "Oro",
        "Plata",
        "Cobre",
        "Carbón"
    ];

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

    public static string[] AllFactKeys(Work work)
    {
        var baseKeys = FrontMatter(work.Type);
        var extraKeys = work.DocumentFacts.Keys
            .Where(k => !baseKeys.Contains(k, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k)))
            .ToArray();
        return [.. baseKeys, .. extraKeys];
    }

    public static string FormatGuidance(string type, string templateName) => type switch
    {
        "Apuntes" when templateName == "Libreta" => "Portada institucional + Índice automático + secciones numeradas con pleca.",
        "Apuntes" when templateName == "Cuaderno Cornell" => "Método Cornell en 3 zonas: preguntas/conceptos clave, notas de desarrollo y síntesis.",
        "Apuntes" => "Cuaderno de estudio con conceptos clave, esquemas, tablas y recuadro de síntesis.",
        "Informe" when templateName == "Reporte" => "Portada centrada + Resumen + Índice de Contenido automático + secciones + Conclusiones y Bibliografía.",
        "Informe" => "Informe estructurado con tabla de metadatos, indicadores, figuras y recomendaciones.",
        "CV" => "Currículum profesional con encabezado de contacto, perfil y entradas cronológicas o por proyectos.",
        "Carta" => "Correspondencia formal en hoja membretada con lugar y fecha, asunto, destinatario y firmas.",
        "Acta" => "Registro oficial de sesión con cuadro de quórum, orden del día, matriz de acuerdos y firmas.",
        "Artículo" => "Publicación académica o de divulgación con afiliación, resumen, apartados numerados y referencias.",
        "Propuesta" => "Dossier técnico o comercial con tabla de cliente/vigencia, alcance, entregables y presupuesto.",
        "Documento" => "Documento técnico o normativo con tabla de referencia, procedimientos, tablas y anexos.",
        "Presentación" => "Presentación panorámica 16:9 (960 × 540 pt) con 28 composiciones y auto-ajuste anti-desborde.",
        _ => "Manuscrito literario con portada y contraportada a hoja completa, índice de contenido y capítulos."
    };

    public static (string Label, string Content)[] QuickSnippets(string type) => type switch
    {
        "Acta" =>
        [
            ("Tabla de acuerdos y responsables", "\n| Acuerdo aprobado | Responsable | Fecha límite | Estado |\n|---|---|---|---|\n| Entregar versión revisada | Coordinación | 30/11/2026 | En curso |\n| Validar pruebas finales | Equipo técnico | 05/12/2026 | Pendiente |\n"),
            ("Registro de asistencia y quórum", "\n| Participante | Cargo o área | Asistencia |\n|---|---|---|\n| Nombre 1 | Presidencia | Presente |\n| Nombre 2 | Secretaría | Presente |\n"),
            ("Orden del día numerado", "\n1. Verificación de quórum y apertura de la sesión.\n2. Revisión de avances y entregables comprometidos.\n3. Lectura, votación y aprobación de acuerdos.\n4. Asuntos generales y clausura.\n")
        ],
        "Carta" =>
        [
            ("Cierre formal con Atentamente y firma", "\nSin otro particular por el momento, quedo a su entera disposición para cualquier aclaración.\n\n**ATENTAMENTE**\n[[Firmas: Nombre y Cargo del Remitente]]\n"),
            ("Cierre con doble firma y Vo. Bo.", "\nAgradeciendo de antemano su atención a la presente solicitud, reiteramos nuestra consideración distinguida.\n[[Firmas: Solicitante | Vo. Bo. Autoridad / Coordinación]]\n"),
            ("Tabla de anexos o documentos adjuntos", "\n| Documento adjunto | Folio o referencia | Observaciones |\n|---|---|---|\n| Expediente técnico | REF-01 | Original firmado |\n")
        ],
        "Propuesta" =>
        [
            ("Tabla de entregables y cronograma", "\n| Fase / Hito | Entregable principal | Plazo estimado | Responsable |\n|---|---|---|---|\n| Fase 1 | Diagnóstico y arquitectura base | Semana 1 – 2 | Dirección técnica |\n| Fase 2 | Desarrollo e integración | Semana 3 – 5 | Equipo de ingeniería |\n| Fase 3 | Pruebas, validación y entrega | Semana 6 | Aseguramiento de calidad |\n"),
            ("Desglose de presupuesto e inversión", "\n| Concepto o partida | Alcance / Unidad | Costo estimado |\n|---|---|---|\n| Diseño y planificación | Fase inicial completa | $ 0.00 |\n| Implementación y pruebas | Entrega verificada | $ 0.00 |\n| **Total estimado** | **Proyecto llave en mano** | **$ 0.00** |\n"),
            ("Matriz de mecánicas o requerimientos", "\n| Requerimiento / Mecánica | Especificación operativa | Prioridad | Criterio de éxito |\n|---|---|---|---|\n| Módulo principal | Operación fluida sin errores | Alta | Validación al 100 % |\n")
        ],
        "Informe" =>
        [
            ("Tabla de indicadores y resultados", "\n| Indicador / Métrica | Valor esperado | Resultado obtenido | Observación |\n|---|---|---|---|\n| Desempeño general | 95 % | 98.4 % | Supera el umbral objetivo |\n| Tiempo de respuesta | < 2.0 s | 1.1 s | Dentro del rango óptimo |\n"),
            ("Ficha de hallazgo y recomendación", "\n### Hallazgo principal\n- **Observación:** Descripción objetiva de la situación identificada.\n- **Impacto:** Efecto directo sobre la operación o el proyecto.\n- **Recomendación:** Acción correctiva o mejora propuesta.\n"),
            ("Referencias bibliográficas numeradas", "\n1. Autor, A. (2026). *Título de la obra o manual técnico*. Editorial o Institución.\n2. Organización / Laboratorio (2026). *Reporte de estándares y especificaciones*.\n")
        ],
        "Apuntes" =>
        [
            ("Tabla comparativa de conceptos", "\n| Concepto / Término | Definición clave | Ejemplo o aplicación |\n|---|---|---|\n| Concepto 1 | Principio fundamental del tema | Aplicación práctica en taller |\n| Concepto 2 | Relación con el modelo general | Caso de estudio |\n"),
            ("Bloque de preguntas de repaso", "\n### Preguntas clave de repaso\n1. **¿Cuál es el principio central de este tema?** — Síntesis de la respuesta.\n2. **¿Cómo se relaciona con la sesión anterior?** — Conexión conceptual.\n"),
            ("Fórmula o regla destacada", "\n> **Regla / Principio fundamental:** Enunciado claro de la propiedad, teorema o criterio que debe recordarse.\n")
        ],
        "Artículo" =>
        [
            ("Tabla comparativa de literatura / resultados", "\n| Estudio / Enfoque | Metodología | Resultado principal | Limitaciones |\n|---|---|---|---|\n| Modelo propuesto | Evaluación experimental | Alta precisión y estabilidad | Requiere calibración inicial |\n| Enfoque tradicional | Revisión documental | Referencia base | Menor adaptabilidad |\n"),
            ("Referencias en formato académico (APA / IEEE)", "\n1. Apellido, N. & Apellido, M. (2026). Título del artículo de investigación. *Revista de Estudios Técnicos*, 14(2), 45–62.\n2. Autor, A. (2025). *Fundamentos y metodología aplicada*. Editorial Universitaria.\n")
        ],
        "Documento" =>
        [
            ("Procedimiento paso a paso (SOP)", "\n| Paso | Actividad / Instrucción | Responsable | Evidencia o registro |\n|---|---|---|---|\n| 01 | Verificar condiciones iniciales y requisitos | Operador / Autor | Lista de control |\n| 02 | Ejecutar procedimiento estándar | Área técnica | Bitácora firmada |\n| 03 | Validar resultado y archivar entrega | Supervisión | Reporte final |\n"),
            ("Control de versiones del documento", "\n| Versión | Fecha | Autor | Descripción del cambio |\n|---|---|---|---|\n| 1.0 | Noviembre 2026 | Autor principal | Emisión inicial del documento |\n"),
            ("Cláusula o disposición normativa", "\n### PRIMERA. — Objeto y alcance\nLas partes acuerdan que el presente documento establece los lineamientos técnicos y operativos aplicables al proyecto.\n")
        ],
        "Novela" =>
        [
            ("Separador de escena", "\n* * *\n"),
            ("Diálogo con raya larga (—)", "\n—"),
            ("Carta o inscripción citada", "\n> ")
        ],
        _ => []
    };

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
