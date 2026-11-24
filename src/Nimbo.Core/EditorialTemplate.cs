namespace Nimbo.Core;

public sealed record EditorialTemplate(string Name, string Style, string[] Sections)
{
    public override string ToString() => Name;

    public static readonly string[] NovelCoverStyles =
    [
        "Velo editorial al pie",
        "Cartela central difuminada",
        "Franja superior de autor",
        "Marco áureo con velo",
        "Tejuelo clásico translúcido",
        "Banda diagonal moderna",
        "Doble filete de gala",
        "Minimalista a sangre"
    ];

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
                new("Cuaderno de prácticas", "Moderna", ["Objetivo", "Materiales", "Procedimiento", "Observaciones", "Reflexión"]),
                new("Libreta universitaria (UASLP)", "LibretaUASLP", ["Clase 17/08", "Clase 18/08", "Clase 20/08", "Selección de imágenes", "Síntesis parcial"]),
                new("Libreta de ingeniería y redes", "LibretaUASLP", ["Encuadre y evaluación", "Direccionamiento IPv4 e IPv6", "Subredes y VLSM", "Enrutamiento y OSPF", "Formulario y comandos"]),
                new("Libreta de arte y diseño de juegos", "LibretaUASLP", ["Principios artísticos", "Selección de referentes", "Luz, color y psicología", "Comunicación visual y HUD", "Diseño de niveles"]),
                new("Libreta de proyectos computacionales", "LibretaUASLP", ["Metodología y calendario", "Requerimientos funcionales", "Requerimientos no funcionales", "Modelado de base de datos", "Casos de uso"]),
                new("Libreta Cornell de cátedra", "Cornell", ["Encuadre y criterios", "Principios fundamentales", "Análisis de referentes", "Síntesis de unidad"]),
                new("Bitácora de taller", "Bitacora", ["Sesión 1: Fundamentos", "Sesión 2: Exploración visual", "Sesión 3: Retroalimentación y ajustes"])
            ],
            "Informe" =>
            [
                standard,
                new("Informe ejecutivo", "Moderna", ["Resumen ejecutivo", "Objetivos", "Indicadores", "Hallazgos", "Recomendaciones", "Próximos pasos"]),
                new("Reporte de práctica de redes (P3/P4/P5)", "PracticaRedes", ["Resumen", "Topología inicial", "Tabla de direccionamiento", "Configuración de dispositivos", "Pruebas de conectividad y verificación", "Conclusión", "Bibliografía"]),
                new("Práctica con políticas ACL y seguridad", "PracticaRedes", ["Resumen", "Topología inicial y tabla de direccionamiento", "Configuración de equipos y enlaces", "Pruebas previas a las políticas", "Políticas de seguridad y configuración ACL", "Comprobación de políticas", "Conclusión", "Bibliografía"]),
                new("Dossier de arte conceptual", "DossierArte", ["Diseño conceptual del mundo", "Landmarks y regiones", "Moodboards y paletas", "Arte conceptual final", "Control de entrega"]),
                new("Reporte técnico experimental", "Académica", ["Resumen", "Arquitectura y topología", "Desarrollo experimental", "Pruebas y resultados", "Conclusión", "Bibliografía"]),
                new("Informe de auditoría y QA", "Auditoría", ["Alcance de auditoría", "Matriz de hallazgos", "Evidencias técnicas", "Plan de mitigación", "Dictamen final"])
            ],
            "Artículo" =>
            [
                standard,
                new("Artículo de revisión", "Académica", ["Resumen", "Pregunta de revisión", "Criterios de selección", "Síntesis de evidencia", "Limitaciones", "Referencias"]),
                new("Estudio técnico aplicado", "Moderna", ["Resumen", "Introducción", "Arquitectura del sistema", "Evaluación experimental", "Conclusiones", "Referencias"]),
                new("Paper científico IEEE / Doble columna", "PaperDoble", ["Abstract / Resumen", "1. Introducción", "2. Marco teórico y arquitectura", "3. Metodología experimental", "4. Resultados y discusión", "5. Conclusiones", "Referencias"]),
                new("Ensayo crítico", "Clásica", ["Planteamiento", "Marco conceptual", "Análisis comparativo", "Conclusiones", "Fuentes"]),
                new("Monografía académica", "Monografía", ["Resumen", "Estado del arte", "Desarrollo temático", "Estudio de caso", "Conclusiones", "Bibliografía"])
            ],
            "Propuesta" =>
            [
                standard,
                new("Oferta de servicios", "Moderna", ["Necesidad", "Solución", "Entregables", "Calendario", "Inversión", "Condiciones"]),
                new("Documento de diseño (GDD)", "GDD", ["Contexto de la IP y USP", "Mecánicas base", "Cartografía y nivel", "Walkthrough táctico", "Jefe o prueba principal"]),
                new("Proyecto de ingeniería", "Académica", ["Definición del problema", "Arquitectura propuesta", "Plan de implementación", "Matriz de riesgos", "Presupuesto"]),
                new("Propuesta de arquitectura de software", "Moderna", ["Resumen ejecutivo", "Requerimientos y alcance", "Arquitectura en capas", "Cronograma de sprints", "Costos y licenciamiento"]),
                new("Cotización y alcance comercial", "Ejecutiva", ["Objetivo del cliente", "Alcance de entregables", "Fases y tiempos", "Desglose de inversión", "Términos y firmas"])
            ],
            "Acta" =>
            [
                standard,
                new("Comité de proyecto", "Moderna", ["Participantes", "Revisión de compromisos", "Decisiones", "Acciones y fechas"]),
                new("Dictamen de revisión técnica", "Académica", ["Quórum y apertura", "Entregables evaluados", "Observaciones técnicas", "Acuerdos y firmas"]),
                new("Minuta de seguimiento", "Moderna", ["Contexto de la reunión", "Avances por área", "Bloqueos detectados", "Compromisos"]),
                new("Acta constitutiva de equipo", "Ejecutiva", ["Instalación y participantes", "Definición de roles", "Reglas operativas y calendario", "Acuerdos y firmas"]),
                new("Cierre de sprint / Retrospectiva", "Moderna", ["Objetivos completados", "Métricas e incidencias", "Mejoras acordadas", "Compromisos del siguiente ciclo"])
            ],
            "Carta" =>
            [
                standard,
                new("Solicitud formal", "Clásica", ["Saludo", "Motivo", "Solicitud", "Despedida"]),
                new("Oficio institucional", "Moderna", ["Antecedentes", "Exposición de motivos", "Petición formal", "Cierre y firmas"]),
                new("Carta de presentación", "Clásica", ["Apertura", "Trayectoria y aportación", "Propuesta de colaboración", "Despedida"]),
                new("Memorándum interno", "Memo", ["Contexto", "Directriz o comunicado", "Acciones requeridas"]),
                new("Carta de entrega de proyecto", "Ejecutiva", ["Relación de entregables", "Alcance verificado", "Conformidad y cierre"])
            ],
            "Documento" =>
            [
                standard,
                new("Guía de trabajo", "Moderna", ["Finalidad", "Preparación", "Instrucciones", "Comprobación"]),
                new("Manual de estándares", "Moderna", ["Alcance y objetivos", "Especificaciones técnicas", "Criterios de validación", "Anexos"]),
                new("Especificación de requerimientos (SRS)", "SRS", ["1. Introducción y propósito", "2. Descripción general del sistema", "3. Requerimientos funcionales (RF)", "4. Requerimientos no funcionales (RNF)", "5. Casos de uso y validación"]),
                new("Manual de arquitectura y operación", "Ejecutiva", ["Topología y componentes", "Requisitos e instalación", "Configuración paso a paso", "Verificación y mantenimiento"]),
                new("Memoria descriptiva", "Clásica", ["Introducción", "Descripción general", "Desarrollo por componentes", "Conclusiones"])
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
        }
        else if (Style == "LibretaUASLP")
        {
            work.Publication.Paper = "Carta";
            work.Publication.Contents = true;
            work.Publication.PageNumbers = true;
            work.DocumentFacts["Universidad"] = "Universidad Autónoma de San Luis Potosí";
            work.DocumentFacts["Facultad"] = "Facultad de Ingeniería";
            work.DocumentFacts["Asignatura"] = title.Trim();
            work.DocumentFacts["Clave de clase"] = "28 36 01";
            work.DocumentFacts["Carrera"] = "Ingeniería en Sistemas Inteligentes";
            work.DocumentFacts["Lugar y fecha"] = "Facultad de Ingeniería de la UASLP, Zona Universitaria Poniente, C.P. 78290";
        }
        else if (Style == "PracticaRedes")
        {
            work.Publication.Paper = "Carta";
            work.Publication.Contents = false;
            work.DocumentFacts["Materia y ciclo"] = "Interacción de Redes · Ciclo 2026";
            work.DocumentFacts["Carrera y claves"] = "Ingeniería en Sistemas Inteligentes, 2021";
            work.DocumentFacts["Clave de materia"] = "Interacción de Redes, 282901";
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
        "Apuntes" => ["Por formato", "Libreta universitaria (UASLP)", "Portada de cátedra con recuadro", "Encabezado directo de cuaderno", "Al pie", "Centrada", "Cabecera"],
        "Informe" => ["Por formato", "Portada de Práctica (P3/P4/P5)", "Portada ejecutiva con bloque", "Portada institucional universitaria", "Al pie", "Centrada", "Cabecera"],
        "CV" => ["Por formato", "Cabecera ejecutiva", "Banda moderna", "Centrada académica", "Doble columna con contacto"],
        "Artículo" => ["Por formato", "Cabecera de revista científica (Paper)", "Portada de monografía", "Al pie", "Centrada", "Cabecera"],
        "Propuesta" => ["Por formato", "Portada comercial ejecutiva", "Portada de GDD ilustrado", "Al pie", "Centrada", "Cabecera"],
        "Acta" => ["Por formato", "Encabezado oficial de sesión", "Portada de comité", "Al pie", "Centrada", "Cabecera"],
        "Carta" => ["Por formato", "Oficio formal con membrete", "Memorándum directo", "Clásica epistolar"],
        "Documento" => ["Por formato", "Portada de especificación (SRS)", "Encabezado de guía técnica", "Al pie", "Centrada", "Cabecera"],
        _ => ["Por formato", "Al pie", "Centrada", "Cabecera"]
    };

    public static string[] SectionLayoutsFor(string type) => type switch
    {
        "Informe" => ["Estándar", "Tabla de direccionamiento / Red", "Secuencia de evidencias y capturas", "Comparativa antes / después", "Resumen y conclusiones"],
        "Apuntes" => ["Clase de libreta (con línea verde)", "Notas Cornell (Conceptos + Síntesis)", "Formulario / Comandos y subredes", "Galería de referentes comentada"],
        "CV" => ["Entradas cronológicas", "Competencias y tecnologías en columnas", "Proyectos destacados", "Perfil ejecutivo"],
        "Artículo" => ["Sección académica", "Abstract / Resumen destacado", "Evaluación experimental y tablas", "Referencias bibliográficas"],
        "Propuesta" => ["Bloque ejecutivo", "Matriz de mecánicas / Entregables", "Cartografía y código cromático", "Presupuesto y firmas"],
        "Acta" => ["Punto de orden del día", "Tabla de acuerdos y responsables", "Verificación de quórum", "Cierre y firmas"],
        "Carta" => ["Cuerpo de oficio formal", "Comunicado con tabla", "Cierre con firmas"],
        "Documento" => ["Sección de manual", "Catálogo de requerimientos (RF / RNF)", "Procedimiento paso a paso", "Tabla de validación"],
        _ => ["Texto"]
    };

    public static string[] FrontMatter(string type) => type switch
    {
        "Carta" => ["Destinatario", "Dirección", "Lugar y fecha", "Asunto"],
        "Acta" => ["Fecha de reunión", "Lugar", "Participantes", "Secretaría"],
        "Informe" => ["Institución", "Departamento", "Periodo del informe", "Materia y ciclo", "Profesor", "Alumno", "Realizada por", "Carrera y claves", "Clave de materia", "Fecha"],
        "Artículo" => ["Afiliación", "Palabras clave", "Correspondencia"],
        "Apuntes" => ["Universidad", "Facultad", "Asignatura", "Clave de clase", "Docente", "Carrera", "Clave U", "Alumno", "Periodo", "Lugar y fecha"],
        "Propuesta" => ["Cliente", "Responsable", "Vigencia", "Presupuesto"],
        "Presentación" => ["Institución", "Evento", "Fecha"],
        "Documento" => ["Organización", "Referencia", "Versión", "Estado"],
        _ => []
    };

    public static string DetailLabel(string type) => type switch
    {
        "Acta" => "Responsable y fecha límite",
        "CV" => "Periodo y ubicación",
        "Apuntes" => "Fecha de clase, preguntas o conceptos clave",
        "Informe" => "Dispositivo, subred o indicador técnico",
        "Artículo" => "Fuente, DOI o referencia",
        "Carta" => "Referencia o expediente",
        "Propuesta" => "Plazo y responsable",
        "Documento" => "Código o nota de sección",
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
