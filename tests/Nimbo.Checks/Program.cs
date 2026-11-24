using System.IO.Compression;
using Nimbo.Core;
using Nimbo.Storage;
using Nimbo.Publishing;
using System.Text.Json;
using System.Text;

var root = Directory.CreateTempSubdirectory("nimbo-check-").FullName;
try
{
    if(args is ["--importar-libro",var biblioteca,var origen]){
        Console.WriteLine($"Obras añadidas: {Legacy.Import(new Shelf(biblioteca),origen)}");
        return;
    }
    if(args is ["--generar-ejemplos",var bibliotecaDestino,var carpetaLibretas,var carpetaPdf,var motorTex]){
        await GenerateShowcase(new Shelf(bibliotecaDestino),carpetaLibretas,carpetaPdf,motorTex);
        return;
    }
    if(args is ["--libro",var carpeta,var destino,var motor]){
        var prueba=new Shelf(root);
        if(Legacy.Import(prueba,carpeta)!=1)throw new Exception("Se esperaba una obra.");
        var obra=prueba.Open(prueba.List().Single().Id).Work;
        await Publisher.Export(obra,prueba.Media,destino,true,false,motor);
        return;
    }
    var profile=Path.Combine(root,"perfil");
    var previousShelf=new Shelf(Path.Combine(profile,"Nimbo"));
    var proof=Format.All[0].Create("Obra anterior");previousShelf.Save(proof,0);
    var data=UserLibrary.OpenDirectory(profile);
    Check(!data.StartsWith(previousShelf.Root+Path.DirectorySeparatorChar),"biblioteca fuera del instalador");
    Check(new Shelf(data).Open(proof.Id).Work.Title==proof.Title,"migración de biblioteca alfa");
    var shelf = new Shelf(root);
    foreach (var format in Format.All)
    {
        var work = format.Create("Prueba de " + format.Name + " & 50%");
        work.Chapters[0].PlainText = "Acentos, ñ y **énfasis**.\nUna segunda línea.";
        var revision = shelf.Save(work, 0);
        var reopened = new Shelf(root).Open(work.Id);
        Check(reopened.Work.Chapters[0].PlainText == work.Chapters[0].PlainText, "texto");
        var next = shelf.Save(work, revision); Check(next == 2, "revisión");
        try { shelf.Save(work, revision); throw new Exception("No detectó el conflicto."); } catch (IOException) { }
        var output = Path.Combine(root, "source.zip"); await Publisher.Export(work, shelf.Media, output, false);
        using (var zip = ZipFile.OpenRead(output)) { using var reader = new StreamReader(zip.GetEntry("obra.tex")!.Open()); Check(reader.ReadToEnd().Contains(@"50\%"), "escape"); }
    }
    foreach(var format in Format.All){
        foreach(var template in EditorialTemplate.For(format)){
            var doc=template.Create(format,"Ejemplo "+format.Name);
            Check(doc.Chapters.Count==template.Sections.Length && doc.Template==template.Name,"estructura de plantilla");
            var keys=EditorialTemplate.FrontMatter(format.Name);
            foreach(var k in keys)doc.DocumentFacts[k]="Ensayo & revisión";
            if(format.Name=="CV"){
                doc.Contact["Nombre profesional"]="Marina Rivera";
                doc.Contact["Profesión"]="Diseñadora editorial";
                doc.Contact["Correo"]="marina@example.org";
                doc.Contact["Ciudad"]="San Luis Potosí";
                doc.Chapters[0].PlainText="Diseño publicaciones para equipos culturales y coordino su preparación para imprenta.";
                var experience=doc.Chapters.First(c=>c.Title is "Experiencia laboral" or "Proyectos" or "Investigación");
                experience.Entries.Add(new(){Heading="Diseño de publicaciones",Organization="Estudio de ejemplo",Period="2022–2025",Description="- Diseñé una colección de catálogos.\n- Coordiné pruebas de impresión con el equipo editorial."});
                doc.Chapters.First(c=>c.Title=="Formación").Entries.Add(new(){Heading="Licenciatura en Diseño",Organization="Universidad de ejemplo",Period="2017–2021"});
            }
            shelf.Save(doc,0);var reopened=shelf.Open(doc.Id).Work;
            var zipPath=Path.Combine(root,"plantilla.zip");await Publisher.Export(reopened,shelf.Media,zipPath,false);
            using(var zip=ZipFile.OpenRead(zipPath)){using var sr=new StreamReader(zip.GetEntry("obra.tex")!.Open());var tex=sr.ReadToEnd();
                if(keys.Length>0)Check(tex.Contains(@"Ensayo \& revisión"),"datos específicos impresos");
                if(format.Name=="CV")Check(tex.Contains("Marina Rivera") && tex.Contains("Estudio de ejemplo") && !tex.Contains("Certificaciones"),"CV con entradas y secciones opcionales");
            }
            if(format.Name=="CV" && args.Contains("--pdf")){
                var output=Environment.GetEnvironmentVariable("NIMBO_SAMPLE_DIR")??root;
                Directory.CreateDirectory(output);
                await Publisher.Export(reopened,shelf.Media,Path.Combine(output,template.Name+".pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
            }
        }
    }
    var figures=new Chapter{Images=["a","b"],PlainText="uno[[Imagen 1]] dos[[Imagen 2]] fin",ImageLayouts=new(){[1]="Página completa"}};
    Illustrations.Move(figures,0,3,figures.PlainText.Length);
    Check(figures.PlainText=="uno dos[[Imagen 2]] fin[[Imagen 1]]","trasladar imagen preserva el texto");
    Illustrations.Remove(figures,0);
    Check(figures.PlainText=="uno dos[[Imagen 1]] fin" && Illustrations.Layout(figures,0)=="Página completa","renumerar imágenes y su composición");
    var spread=new Chapter{Images=["a","b","c","d","e","f"],Layout=Illustrations.Gallery,
        PlainText="[[Imagen 1]][[Imagen 2]] [[Imagen 3]][[Imagen 4]] pie [[Imagen 5]][[Imagen 6]]",ImageLayouts=new(){[5]="Centrada"},
        ImageCaptions=new(){[2]="Fotografía", [3]="Grabado"}};
    Check(Illustrations.Rows(spread).Select(r=>r.Count).SequenceEqual([3,1,1,1]),"galerías respetan prosa y composiciones distintas");
    Illustrations.Remove(spread,2);
    Check(spread.ImageCaptions.Count==1 && spread.ImageCaptions[2]=="Grabado","los pies siguen a la imagen");
    var legacy = Path.Combine(root, "anterior"); var studio = Directory.CreateDirectory(Path.Combine(legacy, "Data", "Studio")).FullName;
    var old = Format.All[0].Create("Prueba migrada");
    var race = new Node { Name = "Elfos", AttributeDefinitions = [new() { Name = "Orejas", DefaultValue = "8 cm" }] };
    var elf = new Node { Name = "Ada", BaseId = race.Id }; old.Glossary.AddRange([race, elf]);
    old.Chapters[0].PlainText = "## Inicio\nLínea inicial.\n- Segunda línea.\n| Columna A | Columna B |\n| Uno | Dos |";
    var png = Path.Combine(root, "figura.png");
    File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAzSURBVEhLY4ift+A/LTEDugC18agFBPGoBQTxqAUE8agFBPGoBQTxqAUE8agFBDHNLQAAbzffTAnMZXYAAAAASUVORK5CYII="));
    var image = shelf.AddImage(png);
    var guide=Format.All.Single(f=>f.Name=="Novela").Create("El archivo de la sal");
    guide.Author="Nimbo";guide.GlossaryPaper="Bolsillo";
    var port=new Node{Name="Puerto Espejo",NodeKind="Lugar",Role="Puerto de invierno de las barcas de sal",Sections=new(){["Paisaje y clima"]="El canal se vacía con la marea. Una hilera de faroles marca el paso entre los bajos."},
        AttributeDefinitions=[new(){Name="Marea de referencia",Unit="m",DefaultValue="2,4"}]};
    var dock=new Node{Name="Muelle de levante",NodeKind="Lugar",ParentId=port.Id,Sections=new(){["Historia local"]="Las tablillas conservan la fecha de cada crecida."}};
    var keeper=new Node{Name="Mara Vélez",NodeKind="Personaje",Type="Práctica de puerto",Age="29 años",Tags="Navegación, mareas",
        Role="Guía los barcos hasta la bocana.",Notes="Busca su nombre en el registro de 1912.",RelatedIds=[dock.Id],
        RelationRoles=new(){[dock.Id]="Lugar de guardia"},Sections=new(){["Deseo y motivación"]="Conservar el farol del muelle.",["Voz y gestos"]="Cuenta las brazas en voz baja."}};
    guide.Glossary.AddRange([port,dock,keeper]);
    var page=guide.Chapters[0];page.Title="El paso de levante";page.Images=[image,image,image];
    if(Environment.GetEnvironmentVariable("NIMBO_SAMPLE_ART") is string artwork){
        var observatory=shelf.AddImage(Path.Combine(artwork,"observatorio.jpg"));
        var engravings=shelf.AddImage(Path.Combine(artwork,"coleccion.jpg"));
        page.Images=[observatory,engravings,observatory];port.ImagePath=observatory;
    }
    page.ImageLayouts=new(){[0]=Illustrations.Gallery,[1]=Illustrations.Gallery,[2]="Página completa"};
    page.ImageCaptions=new(){[0]="Orilla & marea",[1]="El espigón",[2]="El faro"};
    page.PlainText="Mara sabía que el puerto estaba despierto por el roce del cabo contra la madera.\n### Bitácora del muelle\n1. Revisar amarras del canal.\n[[Paleta: #E4D1A5 Farol | #182A27 Sombra]]\n[[Imagen 1]][[Imagen 2]]\n> Cuenta las brazas antes de entrar.\n[[Imagen 3]]\nAl otro lado del canal ya amanecía.\n[[Firmas: Mara Vélez | Capitanía de Puerto]]";
    shelf.Save(guide,0);guide=shelf.Open(guide.Id).Work;
    async Task<string> Source(bool glossary){
        var output=Path.Combine(root,"prueba-maqueta.zip");await Publisher.Export(guide,shelf.Media,output,false,glossary);
        using var zip=ZipFile.OpenRead(output);using var reader=new StreamReader(zip.GetEntry("obra.tex")!.Open());return reader.ReadToEnd();
    }
    var pages=await Source(false);Check(pages.Contains(@"\begin{minipage}[t]{.475\linewidth}") && pages.Contains(@"Orilla \& marea") && pages.Contains(@"\begin{quote}") && pages.Contains("El faro") && pages.Contains("nimboswatch") && pages.Contains("Capitanía de Puerto"),"disposiciones, paletas, firmas y citas imprimibles");
    var pocket=await Source(true);Check(pocket.Contains("paperwidth=90mm,paperheight=140mm") && pocket.Contains("Lugar de guardia") && pocket.Contains("Marea de referencia (m) · Puerto Espejo") && pocket.Contains("Cuenta las brazas en voz baja."),"cuadernillo conserva campos heredados y vínculos");
    if(args.Contains("--pdf")){
        var output=Environment.GetEnvironmentVariable("NIMBO_SAMPLE_DIR")??root;
        await Publisher.Export(guide,shelf.Media,Path.Combine(output,"Cuadernillo de bolsillo.pdf"),true,true,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        await Publisher.Export(guide,shelf.Media,Path.Combine(output,"Ficha de Mara Vélez.pdf"),true,true,Environment.GetEnvironmentVariable("NIMBO_XELATEX"),keeper.Id);
        await Publisher.Export(guide,shelf.Media,Path.Combine(output,"Composición ilustrada.pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        var cover=Format.All.Single(f=>f.Name=="Novela").Create("Edición ilustrada");
        cover.Author="Autora de prueba";cover.CoverImage=page.Images[0];cover.BackImage=page.Images[1];cover.Description="Una sinopsis de prueba.";cover.Publication.Paper="Bolsillo";
        foreach(var frame in new[]{"Filete","Doble filete","Esquinas"}){
            cover.FrameStyle=frame;await Publisher.Export(cover,shelf.Media,Path.Combine(output,frame+".pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        }
        var custom=Path.Combine(root,"prueba.sty");File.WriteAllText(custom,@"\ProvidesPackage{prueba}\newcommand{\resultado}{Diapositiva personalizada}");
        cover.UseTexSource=true;cover.TexSource=@"\documentclass{beamer}\usepackage{prueba}\begin{document}\begin{frame}\resultado\end{frame}\end{document}";
        cover.TexResources["prueba.sty"]=shelf.AddAsset(custom);
        await Publisher.Export(cover,shelf.Media,Path.Combine(output,"Fuente personalizada.pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        var smallBook=Format.All.Single(f=>f.Name=="Novela").Create("Ajuste de ilustraciones");
        smallBook.Publication.Paper="Bolsillo";smallBook.Publication.Contents=false;
        var sample=smallBook.Chapters[0];
        for(int i=0;i<12;i++){
            sample.Images.Add(image);
            sample.ImageLayouts[i]=i%2==0?"Ajustar a la derecha":"Ajustar a la izquierda";
            sample.ImageCaptions[i]="Figura de referencia "+(i+1);
            sample.PlainText+=string.Concat(Enumerable.Repeat("Una línea de referencia para probar las dos orillas del papel.\n",8+i));
            sample.PlainText+=Illustrations.At(i)+"\n";
        }
        sample.PlainText+="La última figura acompaña al cierre del capítulo.";
        await Publisher.Export(smallBook,shelf.Media,Path.Combine(output,"Figuras ajustadas.pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
    }
    var paragraph = new Chapter { Title = "Prueba de estilo", PlainText = "Una palabra", Order = 1 };
    var subset=Path.Combine(root,"seleccion.zip");
    await Publisher.Export(guide,shelf.Media,subset,false,true,selection:new HashSet<string>{keeper.Id,dock.Id});
    using(var zip=ZipFile.OpenRead(subset)){
        using var reader=new StreamReader(zip.GetEntry("obra.tex")!.Open());var selectionSource=reader.ReadToEnd();
        Check(selectionSource.Contains(@"\abrirficha{Personaje}{Mara Vélez}") && selectionSource.Contains(@"\abrirficha{Lugar}{Muelle de levante}") && !selectionSource.Contains(@"\abrirficha{Lugar}{Puerto Espejo}"),"selección de fichas");
        Check(selectionSource.Contains("Marea de referencia (m) · Puerto Espejo"),"la selección conserva la herencia externa");
    }
    File.Copy(png,Path.Combine(legacy,"muestra.png"));paragraph.Images=["muestra.png"];
    paragraph.ContentPackage = "LW5:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("<FlowDocument><Paragraph><Run>Una </Run><Bold><Run>palabra</Run></Bold></Paragraph></FlowDocument>"));
    old.Chapters.Add(paragraph);
    var filename = Path.Combine(studio, "library.json");
    File.WriteAllText(filename, JsonSerializer.Serialize(new Library { Projects = [old] }));
    var reading = Directory.CreateDirectory(Path.Combine(legacy, "Obras", old.Title, "Edición de lectura")).FullName;
    var previous = Path.Combine(reading, "Libro.pdf");
    File.WriteAllText(previous, "%PDF-reference");
    Check(Legacy.Import(shelf, legacy) == 1 && Legacy.Import(shelf, legacy) == 0, "importación repetible");
    Check(shelf.Open(old.Id).Work.Chapters[1].PlainText == "Una **palabra**", "conversión del énfasis");
    Check(shelf.Open(old.Id).Work.Chapters[1].Images.Single()==image,"migrar ilustración ya posicionada");
    var savedEdition = shelf.Open(old.Id).Work.Editions.Single(); Check(File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(Path.Combine(shelf.Media, savedEdition.Key))), "edición anterior idéntica");
    Check(WorldModel.Effective(shelf.Open(old.Id).Work, elf).Single().Value == "8 cm", "herencia");
    if (args.Contains("--pdf"))
    {
        old = shelf.Open(old.Id).Work;
        old.Chapters[0].Images.Add(image); old.Chapters[0].Layout = "Centrada"; old.Chapters[0].PlainText += "\nInicio de figura\n" + Illustrations.At(0) + "\nFin de figura";
        old.Chapters[0].ImageLayouts[0]="Ajustar a la derecha";
        var book = Path.Combine(root, "book.pdf"); var glossary = Path.Combine(root, "glossary.pdf");
        await Publisher.Export(old, shelf.Media, book, true, false, Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        await Publisher.Export(old, shelf.Media, glossary, true, true, Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        var slides = Format.All.Single(f => f.Name == "Presentación").Create("Prueba de diapositivas");
        slides.Chapters[1].Layout = "Texto e imagen"; slides.Chapters[1].Images.Add(image); slides.Chapters[1].PlainText = "Idea central";
        var slidePdf = Path.Combine(root, "slides.pdf");
        await Publisher.Export(slides, shelf.Media, slidePdf, true, false, Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        foreach (var path in new[] { book, glossary, slidePdf }) Check(Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 5) == "%PDF-", "PDF");
    }
    if (Environment.GetEnvironmentVariable("NIMBO_TEST_LEGACY") is string source)
    {
        int count = Legacy.Import(shelf, source); Check(Legacy.Import(shelf, source) == 0, "biblioteca duplicada");
        Console.WriteLine($"Biblioteca local comprobada: {count} obras.");
    }
    Console.WriteLine("OK: 10 formatos, guardado, conflictos, migración, herencia y exportación.");
}
finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
static void Check(bool result, string check) { if (!result) throw new Exception(check); }

static async Task GenerateShowcase(Shelf shelf, string libretas, string pdfOut, string engine)
{
    Directory.CreateDirectory(pdfOut);
    string Asset(string relative)
    {
        var full = Path.Combine(libretas, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) throw new FileNotFoundException("No se encontró el recurso visual: " + full);
        return shelf.AddImage(full);
    }
    void Upsert(Work work)
    {
        var existing = shelf.List().FirstOrDefault(e => e.Title.Equals(work.Title, StringComparison.OrdinalIgnoreCase) && e.Type == work.Type);
        if (existing != null)
        {
            work.Id = existing.Id;
            shelf.Save(work, existing.Revision);
        }
        else shelf.Save(work, 0);
    }

    var l01Final = Asset("output/ACT3_Plantillas/ACPV/imagenes/L01/IFinal-01.png");
    var l01Concept = Asset("output/ACT3_Plantillas/ACPV/imagenes/L01/AConcept-01.jpg");
    var l01Refs = Enumerable.Range(1, 6).Select(i => Asset($"output/ACT3_Plantillas/ACPV/imagenes/L01/referencia-0{i}.jpg")).ToArray();
    var l02Final = Asset("output/ACT3_Plantillas/ACPV/imagenes/L02/IFinal-01.png");
    var l03Final = Asset("output/ACT3_Plantillas/ACPV/imagenes/L03/Camara-del-Sol-final-01.png");
    var l03Concept = Asset("output/ACT3_Plantillas/ACPV/imagenes/L03/AConcept-01.jpg");
    var mapDist = Asset("output/ACT3_Plantillas/DJ/imagenes/niveles/nivel-01/Piso 85 - Distribución.jpg");
    var mapScene = Asset("output/ACT3_Plantillas/DJ/imagenes/niveles/nivel-01/Piso 85 - Escenario.jpg");
    var mapFinal = Asset("output/ACT3_Plantillas/DJ/imagenes/niveles/nivel-01/Piso_85_final.png");
    var slideCover = Asset("output/Fichas_Juegos_LaTeX/portada3.png");
    var hudSketch = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Preliminar/J1_Boceto_1.jpg");
    var hudScene1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/V1_Escena_1.jpg");
    var hudScene3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/V3_Escena_2.jpg");
    var portraitAvatar = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Protagonista_noble.png");
    var colossus = Asset("Shadow-of-the-Colossus-jagatplay-7-1073652351.jpg");
    var nier = Asset("341753-NieR-Replicant-Video-Game-Characters-3551670508.jpg");
    var darkest = Asset("Darkest-Dungeon-correccion.jpg");
    var topology = Asset("topologia_t1.png");
    var serverImg = Asset("servidor.png");

    var examples = new List<(Work Work, string FileName)>();

    var informe = EditorialTemplate.For(Format.All.Single(f => f.Name == "Informe"))[1].Create(Format.All.Single(f => f.Name == "Informe"), "Diseño conceptual: Lantern & Labyrinth");
    informe.Author = "Taller de Arte Conceptual";
    informe.Subtitle = "Dossier de dirección artística, moodboards y paletas cromáticas";
    informe.CollectionId = "Ejemplos de complejidad";
    informe.CoverColor = "#1B5272";
    informe.CoverImage = l01Final;
    informe.Description = "Informe ejecutivo de arte conceptual con tabla técnica de IP, moodboard de 6 referencias en cuadrícula, paleta cromática de 5 muestras HEX y comparativa entre boceto estructural y render final.";
    informe.DocumentFacts["Institución"] = "Estudio de Arte Conceptual y Narrativa Interactiva";
    informe.DocumentFacts["Departamento"] = "Dirección de Arte · Proyecto Lantern & Labyrinth";
    informe.DocumentFacts["Periodo del informe"] = "Ciclo de preproducción 2026";
    informe.Chapters = [
        new Chapter
        {
            Title = "1. Síntesis y arquitectura del mundo",
            Order = 0,
            Detail = "RPG táctico por turnos en cuadrícula · 5 landmarks conectados",
            PlainText = """
El mundo de **Lantern & Labyrinth** articula una economía de gremios alrededor de la Fosa del Juramento, una mazmorra vertical de cien estratos situada en el corazón de Ciudad Miramar.

| Atributo de la IP | Especificación de diseño |
|---|---|
| Género y cámara | RPG táctico por turnos en cuadrícula isométrica y gestión de clan |
| Ambientación | Fantasía medieval de piedra húmeda, bosques subterráneos y arquitectura gótica |
| Sistema de magia | Magia elemental cotidiana (fuego, hielo, rayo y luz) estudiada en gremios |
| Conflicto central | Colapso territorial ante el invierno de la Bruja de Hielo y descenso al piso 100 |

### Landmarks principales del recorrido
1. **L01 — Entrada a la Fosa del Juramento:** Escalinata monumental en el centro de Miramar que desciende hacia el primer estrato boscoso.
2. **L02 — Ríos a la Necrópolis (Pisos 78–79):** Estrato de piedra pálida, cascadas subterráneas y enredaderas blancas.
3. **L03 — Cámara del Sol (Piso 85):** Cúpula de cristal entre árboles blancos y ríos carmesí que marca el récord histórico de exploración.
"""
        },
        new Chapter
        {
            Title = "2. Moodboard L01: Entrada a la Fosa del Juramento",
            Order = 1,
            Detail = "6 referencias visuales en cuadrícula 2x3 + paleta cromática Adobe Color",
            Layout = Illustrations.Gallery,
            Images = [.. l01Refs],
            ImageLayouts = Enumerable.Range(0, 6).ToDictionary(i => i, _ => Illustrations.Gallery),
            ImageCaptions = new()
            {
                [0] = "R01 — Arcos y niebla verdosa",
                [1] = "R02 — Antorchas de hierro en muro",
                [2] = "R03 — Bosque subterráneo profundo",
                [3] = "R04 — Escalinata curva de piedra",
                [4] = "R05 — Faroles cálidos y barandal",
                [5] = "R06 — Mampostería irregular húmeda"
            },
            PlainText = """
Gran hondonada central en Ciudad Miramar cuyas escaleras descienden entre niebla verdosa hasta el arco de ladrillo que abre el bosque subterráneo.
[[Imagen 1]][[Imagen 2]][[Imagen 3]]
[[Imagen 4]][[Imagen 5]][[Imagen 6]]
### Paleta cromática del estrato L01
[[Paleta: #E4D1A5 Luz de farol | #D99442 Ámbar dorado | #7B8581 Niebla pétrea | #55705C Musgo antiguo | #182A27 Sombra abisal]]
| Muestra HEX | Aplicación en el escenario | Función perceptual |
|---|---|---|
| #E4D1A5 y #D99442 | Centro de faroles, bordes de peldaños y reflejos cálidos | Guía visual de ruta segura y puntos de descanso |
| #7B8581 y #55705C | Niebla densa, muros de ladrillo húmedo y vegetación | Contraste frío que envuelve la arquitectura olvidada |
| #182A27 | Arcos profundos, pilares y fondo del bosque subterráneo | Profundidad, misterio y delimitación del espacio jugable |
"""
        },
        new Chapter
        {
            Title = "3. Evolución de arte conceptual a render final",
            Order = 2,
            Detail = "Comparativa entre layout cromático de zonas y composición ilustrada final",
            Layout = Illustrations.Gallery,
            Images = [l01Concept, l01Final, l03Concept, l03Final],
            ImageLayouts = new() { [0] = Illustrations.Gallery, [1] = Illustrations.Gallery, [2] = Illustrations.Gallery, [3] = Illustrations.Gallery },
            ImageCaptions = new()
            {
                [0] = "L01 — Boceto estructural de zonas",
                [1] = "L01 — Arte conceptual final iluminado",
                [2] = "L03 — Boceto de la Cámara del Sol",
                [3] = "L03 — Render final del piso 85"
            },
            PlainText = """
El flujo de producción parte de un boceto de zonas codificado por color para fijar la perspectiva y culmina en la ilustración final con texturas pintadas a mano.
[[Imagen 1]][[Imagen 2]]
[[Imagen 3]][[Imagen 4]]
> Toda composición prioriza la lectura inmediata de las rutas de descenso y los focos de iluminación cálida frente a la penumbra fría del laberinto.
"""
        }
    ];
    examples.Add((informe, "01-Informe-Diseno-Conceptual-ACPV.pdf"));

    var propuesta = EditorialTemplate.For(Format.All.Single(f => f.Name == "Propuesta"))[1].Create(Format.All.Single(f => f.Name == "Propuesta"), "GDD de Nivel: Piso 85 Cámara del Sol");
    propuesta.Author = "Equipo de Diseño de Niveles";
    propuesta.Subtitle = "Diseño táctico, cartografía con leyenda cromática y walkthrough de encuentro";
    propuesta.CollectionId = "Ejemplos de complejidad";
    propuesta.CoverColor = "#364A8A";
    propuesta.CoverImage = mapFinal;
    propuesta.Description = "Propuesta de diseño de nivel (GDD parcial) con matriz de mecánicas y USP, evolución cartográfica en 3 etapas, leyenda de colores, recorrido paso a paso y tabla de fases del jefe GRIM.";
    propuesta.DocumentFacts["Cliente"] = "Comité Editorial de Diseño de Juegos";
    propuesta.DocumentFacts["Responsable"] = "División de Arquitectura de Niveles y Sistemas Tácticos";
    propuesta.DocumentFacts["Vigencia"] = "Vertical Slice · Estratos 79 a 85";
    propuesta.DocumentFacts["Presupuesto"] = "105 minutos de juego efectivo (N01: 50 min, N02: 20 min, N03: 35 min)";
    propuesta.Chapters = [
        new Chapter
        {
            Title = "1. Mecánicas base y propuesta de valor (USP)",
            Order = 0,
            Detail = "Coordinación multiescuadrón y sincronización de patrullas por pulso",
            PlainText = """
El nivel **N01: Piso 85 Cámara del Sol** marca la frontera de la Necrópolis. Su diseño obliga a dividir el clan en dos grupos (ruta norte y ruta sur) para converger en la sala del cuarto jefe.

| Mecánica táctica | Descripción operativa | Controles | Relación con el USP |
|---|---|---|---|
| Avance por pulsos | Cada casilla recorrida desplaza simultáneamente a las patrullas enemigas | Direccionales / Cuadrícula | Exige medir distancias antes de cruzar intersecciones |
| Apoyo multiescuadrón | Los escuadrones adyacentes intervienen con Decisiones Tácticas en combate | Menú táctico de apoyo | Premia reunir al grupo norte y sur antes del jefe |
| Ruptura y linterna | Aturdimiento por debilidad elemental y campamento de recuperación | Barra de acción / Tecla R | Permite sostener expediciones largas sin muerte permanente |
"""
        },
        new Chapter
        {
            Title = "2. Cartografía del nivel y leyenda cromática",
            Order = 1,
            Detail = "Tránsito desde distribución conceptual y escenario hasta mapa Tiled",
            Layout = Illustrations.Gallery,
            Images = [mapDist, mapScene, mapFinal],
            ImageLayouts = new() { [0] = Illustrations.Gallery, [1] = Illustrations.Gallery, [2] = Illustrations.Gallery },
            ImageCaptions = new()
            {
                [0] = "N01-D — Distribución conceptual",
                [1] = "N01-E — Ambientación de ríos rojos",
                [2] = "N01-F — Mapa final terminado en Tiled"
            },
            PlainText = """
Las tres láminas documentan la estructura de salas (A–I), la distribución ambiental de agua roja y árboles blancos, y la integración final en tileset isométrico.
[[Imagen 1]][[Imagen 2]][[Imagen 3]]
### Código cromático de la distribución conceptual
[[Paleta: #5BC0EB Inicio (Sala A) | #6E6E6E Salas y pasillos | #E8833A Rutas de patrulla | #1F7A7A Acceso con herramienta | #7ED957 Salida (Sala I)]]
| Color en el plano | Significado espacial y reglas de tránsito |
|---|---|
| Azul celeste y Verde claro | Puntos de entrada del clan (Sala A) y casilla de victoria tras vencer a GRIM (Sala I) |
| Naranja | Recorridos cíclicos de patrullas y ruta en U del jefe secundario |
| Rojo y Azul verdoso | Barreras de agua roja infranqueables y atajos condicionados a herramientas |
"""
        },
        new Chapter
        {
            Title = "3. Walkthrough táctico y encuentro con GRIM",
            Order = 2,
            Detail = "Recorrido de 50 minutos en 6 escuadrones y matriz de habilidades del jefe",
            PlainText = """
### Secuencia resumida del recorrido (Salas A – I)
1. **Paso N01-P01 (Sala A, min 1):** Organización de 6 escuadrones en dos grupos equilibrados con clérigos y vanguardia.
2. **Paso N01-P03 (Ruta Norte A–C–E, min 17):** El grupo norte recoge Cristal Solar en D, limpia el doble anillo de C y descansa con linterna en la antesala E.
3. **Paso N01-P07 (Ruta Sur A–F–G–H, min 36):** El grupo sur despeja las salas inferiores y asegura el punto de reunión en H.
4. **Paso N01-P12 (Cámara del Sol, min 49):** Ambos grupos entran simultáneamente a la Sala I para flanquear a **GRIM** en su ruta naranja en U.

| Habilidad de GRIM | Descripción del ataque | Telegrafía | Contrajuego del jugador |
|---|---|---|---|
| Edicto de Ceniza | Aplica penalización de ataque a las clases físicas del escuadrón frontal | Destello azul celeste en la cabeza ígnea | Rotar el turno hacia magos/clérigos y activar apoyo lateral |
| Guadaña de Hacha Blanca | Triple barrido consecutivo de alto daño sobre la primera fila | Giro del hacha negra de hoja blanca | Concentrar ruptura elemental para aturdir antes del tercer golpe |

[[Firmas: Dirección de Diseño de Niveles | Coordinación de Balance y QA]]
"""
        }
    ];
    examples.Add((propuesta, "02-Propuesta-GDD-Nivel-Piso85.pdf"));

    var presentacion = EditorialTemplate.For(Format.All.Single(f => f.Name == "Presentación"))[2].Create(Format.All.Single(f => f.Name == "Presentación"), "Diseño de HUD e Interfaces Visuales");
    presentacion.Author = "Taller de Interfaces Interactivas";
    presentacion.Subtitle = "Fichas de concepto de videojuegos, arquitectura HUD 16:9 y tablas de respuesta";
    presentacion.CollectionId = "Ejemplos de complejidad";
    presentacion.CoverColor = "#1E5B88";
    presentacion.CoverImage = slideCover;
    presentacion.Description = "Presentación panorámica 16:9 con portada orgánica estilo Fichas de Juegos, 3 fichas completas de concepto con 10 recursos visuales cada una, tabla visual de componentes HUD con sprites integrados, moodboard con paleta, plano táctico con leyenda y lámina 1920x1080.";
    presentacion.DocumentFacts["Institución"] = "Estudio de Arte Conceptual y Diseño de Interfaces";
    presentacion.DocumentFacts["Evento"] = "Revisión de Fichas de Concepto y Sistemas HUD 16:9";
    presentacion.DocumentFacts["Fecha"] = "Noviembre 2026";

    var steamLogo = Asset("output/Fichas_Juegos_LaTeX/imagenes/steam.png");
    var esrb1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/Clasificación_1.png");
    var esrb2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/Clasificación_2.png");
    var v1Ref1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Pocket-Mirror.jpg");
    var v1Ref2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Little-Goody-Two-Shoes.jpg");
    var v1Ref3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Omori.jpg");
    var v1Ref4 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Corpse-Party.jpg");
    var v1Game1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/V1_Gameplay_1.jpg");
    var v1Game2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/V1_Gameplay_2.jpg");
    var v1Game3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/V1_Gameplay_3.jpg");
    var v1Fondo = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Fondo_V1.png");
    var v1Rosas = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Bases/ai-generated-black-rose-isolated-on-transparent-background-free-png-2936538963.png");
    var v1Vela = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Bases/candle-pixel-art-png-2988415674.png");
    var v1Corazon = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Bases/red-heart-pixel-art-png-2654343113.png");
    var v1Marco = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Bases/gold-circle-frame-texture-and-gradients-free-png-2179328681.png");
    var v1Niebla = Asset("output/Fichas_Juegos_LaTeX/imagenes/V1/Bases/pngtree-mist-transparant-png-png-image_8396343-2845193954.png");

    var v2Ref1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/Core-Keeper.jpg");
    var v2Ref2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/Travellers-Rest.jpg");
    var v2Ref3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/Graveyard-Keeper.jpg");
    var v2Ref4 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/Moonlighter.jpg");
    var v2Game1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/V2_Gameplay_1.jpg");
    var v2Game2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/V2_Gameplay_2.jpg");
    var v2Game3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/V2_Gameplay_3.jpg");
    var v2Fondo = Asset("output/Fichas_Juegos_LaTeX/imagenes/V2/Fondo_V2.png");

    var v3Ref1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/XCOM.jpg");
    var v3Ref2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/9-Kings.jpg");
    var v3Ref3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/Darkest-Dungeon.jpg");
    var v3Ref4 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/The-Last-Spell.jpg");
    var v3Game1 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/V3_Gameplay_1.jpg");
    var v3Game2 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/V3_Gameplay_2.jpg");
    var v3Game3 = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/V3_Gameplay_3.jpg");
    var v3Fondo = Asset("output/Fichas_Juegos_LaTeX/imagenes/V3/Fondo_V3.png");

    Chapter MakeConceptSlide(string title, int order, string genre, string concept, string[] images, string[] captions, string lists)
    {
        string[] roles =
        [
            "Plataforma",
            "Target & ESRB",
            "Referencia visual 1",
            "Referencia visual 2",
            "Referencia visual 3",
            "Referencia visual 4",
            "Gameplay 1",
            "Gameplay 2",
            "Gameplay 3",
            "Fondo de diapositiva"
        ];
        var slide = new Chapter
        {
            Title = title,
            Order = order,
            Layout = "Ficha de concepto",
            Detail = genre,
            Summary = concept,
            Images = [.. images],
            PlainText = lists
        };
        for (int i = 0; i < images.Length && i < roles.Length; i++)
            slide.ImageLayouts[i] = roles[i];
        slide.ImageCaptions[6] = captions[0];
        slide.ImageCaptions[7] = captions[1];
        slide.ImageCaptions[8] = captions[2];
        return slide;
    }

    presentacion.Chapters = [
        new Chapter
        {
            Title = "Fichas de Concepto y Arquitectura HUD",
            Order = 0,
            Layout = "Portada",
            Detail = "Sistemas de interfaz diegética, fichas comparativas y dirección visual 16:9",
            Images = [slideCover],
            PlainText = "Catálogo ejecutivo de propuestas interactivas y matrices de respuesta visual"
        },
        MakeConceptSlide(
            "Mansión de las Velas",
            1,
            "Terror psicológico",
            "Un juego de aventura narrativa, similar a Pocket Mirror, con una joven de 14 años que resuelve puzles en una mansión en su memoria mientras conserva la luz de sus velas frente a la oscuridad.",
            [steamLogo, esrb1, v1Ref1, v1Ref2, v1Ref3, v1Ref4, v1Game1, v1Game2, v1Game3, v1Fondo],
            ["Explorar", "Encender", "Dialogar"],
            """
### Referencias
1. Pocket Mirror ~ GoldenerTraum
2. Little Goody Two Shoes
3. Mad Father
4. Corpse Party
5. Omori
6. The Witch's House

### Key Words
1. Explorar
2. Encender
3. Dialogar
4. Recordar
5. Sobrevivir
6. Escapar
"""),
        MakeConceptSlide(
            "Chaos Tavern",
            2,
            "Simulación de gestión",
            "Un juego de gestión, similar a Graveyard Keeper, con una joven que debe administrar y mantener en funcionamiento la taberna de su difunto tío en un mundo de fantasía oscura lleno de misterios.",
            [steamLogo, esrb2, v2Ref1, v2Ref2, v2Ref3, v2Ref4, v2Game1, v2Game2, v2Game3, v2Fondo],
            ["Atender", "Disparar", "Mejorar"],
            """
### Referencias
1. Travellers Rest
2. Core Keeper
3. Graveyard Keeper
4. Moonlighter
5. Bear and Breakfast
6. Strange Horticulture

### Key Words
1. Atender
2. Disparar
3. Mejorar
4. Investigar
5. Preparar
6. Abastecer
"""),
        MakeConceptSlide(
            "Frenzied Dungeon",
            3,
            "Estrategia táctica por turnos",
            "Un juego de estrategia táctica por turnos, similar a XCOM: Enemy Unknown, con un grupo de aventureros que debe combatir y alcanzar el piso número 100 de una mazmorra en un mundo de fantasía medieval.",
            [steamLogo, esrb2, v3Ref1, v3Ref2, v3Ref3, v3Ref4, v3Game1, v3Game2, v3Game3, v3Fondo],
            ["Explorar", "Combatir", "Mejorar"],
            """
### Referencias
1. XCOM: Enemy Unknown
2. 9 Kings
3. Into the Breach
4. The Last Spell
5. Darkest Dungeon
6. Dungeon of the Endless

### Key Words
1. Explorar
2. Combatir
3. Mejorar
4. Planificar
5. Defender
6. Atacar
"""),
        new Chapter
        {
            Title = "Matriz de Componentes HUD 1: Mansión de las Velas",
            Order = 4,
            Layout = "Tabla visual",
            Detail = "Jerarquía periférica y respuesta reactiva sin cortes",
            Images = [portraitAvatar, v1Rosas, v1Corazon, v1Vela, v1Marco, v1Niebla],
            Summary = "La vela actúa como temporizador diegético: al consumirse la cera, las rosas negras aceleran su desgaste en la penumbra.",
            PlainText = """
| Componente visual | Elemento del HUD | Uso y función en pantalla | Respuesta reactiva al jugador |
|---|---|---|---|
| [[Imagen 1]] | Retrato noble | Identifica la condición física y emocional | Cambia de expresión según el nivel de peligro |
| [[Imagen 2]] | Rosas negras | Representan los puntos de salud mental | Se marchitan y pierden pétalos en la oscuridad |
| [[Imagen 3]] | Pulso vital | Indica la resistencia disponible al huir | Parpadea en carmesí antes de agotarse por completo |
| [[Imagen 4]] | Vela activa | Muestra el tiempo restante de iluminación | La cera desciende y la llama tiembla con corrientes |
| [[Imagen 5]] | Medallón dorado | Marco de reliquia equipada y llaves activas | Resalta con brillo áureo el objeto en uso |
| [[Imagen 6]] | Velo de penumbra | Señala el grado de exposición ante espectros | Envuelve los bordes cuando una amenaza se aproxima |
"""
        },
        new Chapter
        {
            Title = "Arquitectura de HUD: Boceto a Composición Final",
            Order = 5,
            Layout = "Texto e imagen",
            Detail = "Flujo de diseño de interfaz 16:9",
            Images = [hudSketch],
            ImageCaptions = new() { [0] = "Boceto preliminar de lectura periférica y zonas libres en proporción 16:9." },
            Summary = "Toda interfaz debe informar el estado crítico del personaje en menos de un segundo sin ocultar la acción principal.",
            PlainText = """
- **Prioridad periférica:** Agrupar retrato, salud mental y energía en la esquina superior izquierda.
- **Retroalimentación reactiva:** Cada indicador cambia de forma o brillo ante el peligro.
- **Coherencia estilística:** Integrar materiales del mundo (hierro, pergamino, cristal) en los marcos del HUD.
"""
        },
        new Chapter
        {
            Title = "Moodboard y Paleta: Fosa del Juramento",
            Order = 6,
            Layout = "Moodboard y paleta",
            Detail = "Contraste térmico de iluminación guía",
            Images = [l01Refs[0], l01Refs[1], l01Refs[3], l01Refs[4]],
            ImageCaptions = new() { [0] = "R01 Niebla", [1] = "R02 Antorcha", [2] = "R04 Escalinata", [3] = "R05 Faroles" },
            Summary = "Cuatro referencias de piedra húmeda y faroles de hierro forjado definen el contraste térmico del acceso.",
            PlainText = """
### Contraste entre piedra fría y luz guía
- La arquitectura emplea grises verdosos y musgo profundo.
- Las rutas interactivas se destacan con luz ámbar y crema.
[[Paleta: #E4D1A5 Luz | #D99442 Ámbar | #7B8581 Piedra | #55705C Musgo | #182A27 Sombra]]
"""
        },
        new Chapter
        {
            Title = "Plano Táctico y Leyenda: Cámara del Sol",
            Order = 7,
            Layout = "Plano y leyenda",
            Detail = "Nivel N01 · Piso 85",
            Images = [mapDist],
            ImageCaptions = new() { [0] = "Layout N01-D del Piso 85 con rutas de patrullaje en naranja." },
            Summary = "La simetría de los accesos norte y sur invita a dividir al clan y sincronizar la entrada final.",
            PlainText = """
| Color en plano | Función espacial en el mapa |
|---|---|
| Celeste | Sala inicial de despliegue del clan (Sala A) |
| Café | Pasillos transitables y puentes sobre agua roja |
| Naranja | Rutas cíclicas de patrullas y recorrido de GRIM |
| Verde | Casilla de victoria detrás del altar solar (Sala I) |
"""
        },
        new Chapter
        {
            Title = "Integración Táctica en Motor (1920x1080)",
            Order = 8,
            Layout = "Imagen protagonista",
            Detail = "Render final 16:9 con HUD en acción",
            Images = [hudScene3],
            ImageCaptions = new() { [0] = "Frenzied Dungeon: cuadrícula de alcance táctico, retrato de cristal y barra de acciones." },
            PlainText = "Vista final de combate por turnos con lectura simultánea de alcance azul/rojo y panel de habilidades."
        }
    ];
    examples.Add((presentacion, "03-Presentacion-Arquitectura-HUD-16x9.pdf"));

    var apuntes = EditorialTemplate.For(Format.All.Single(f => f.Name == "Apuntes"))[1].Create(Format.All.Single(f => f.Name == "Apuntes"), "Cuaderno de Arte Conceptual y Composición");
    apuntes.Author = "Notas de Taller Editorial";
    apuntes.Subtitle = "Fundamentos visuales, escala monumental y lenguaje del color";
    apuntes.CollectionId = "Ejemplos de complejidad";
    apuntes.CoverColor = "#376B48";
    apuntes.CoverImage = colossus;
    apuntes.Description = "Cuaderno de apuntes estilo Cornell con preguntas guía, galería comparativa de referentes artísticos, paleta cromática y recuadros de síntesis por lección.";
    apuntes.DocumentFacts["Asignatura"] = "Arte Conceptual para Videojuegos · AC-3601";
    apuntes.DocumentFacts["Docente"] = "Mtro. Darío Velázquez Solís";
    apuntes.DocumentFacts["Periodo"] = "Semestre Agosto – Diciembre 2026";
    apuntes.Chapters = [
        new Chapter
        {
            Title = "Lección 1: Escala, silueta y dirección de la mirada",
            Order = 0,
            Detail = "¿Cómo comunica vulnerabilidad o tensión la proporción entre el personaje y la arquitectura? · Conceptos: escala monumental, alto contraste y encuadre.",
            Summary = "La combinación de siluetas asimétricas, atmósfera densa y economía cromática permite dirigir la mirada del jugador hacia el conflicto sin saturar la pantalla.",
            Layout = Illustrations.Gallery,
            Images = [colossus, nier, darkest],
            ImageLayouts = new() { [0] = Illustrations.Gallery, [1] = Illustrations.Gallery, [2] = Illustrations.Gallery },
            ImageCaptions = new()
            {
                [0] = "Shadow of the Colossus — Escala monumental",
                [1] = "NieR Replicant — Composición de personajes",
                [2] = "Darkest Dungeon — Contraste expresionista"
            },
            PlainText = """
El análisis de referentes demuestra tres estrategias complementarias para construir atmósfera desde el primer encuadre:
[[Imagen 1]][[Imagen 2]][[Imagen 3]]
| Obra analizada | Principio artístico dominante | Efecto perceptual en el jugador |
|---|---|---|
| Shadow of the Colossus | Desproporción de escala y espacio negativo | Sensación de inmensidad, soledad y asombro |
| NieR Replicant | Equilibrio de siluetas y contraste lumínico | Identificación inmediata de roles dentro del grupo |
| Darkest Dungeon | Trazo pesado y sombras negras profundas | Tensión psicológica y opresión claustrofóbica |

### Notas de aplicación en taller
1. Definir primero la silueta general en dos valores (luz y sombra) antes de detallar texturas.
2. Reservar el mayor contraste tonal exclusivamente para el punto focal de la escena.
[[Paleta: #E4D1A5 Foco cálido | #D99442 Acento | #7B8581 Niebla | #55705C Musgo | #182A27 Sombra]]
"""
        }
    ];
    examples.Add((apuntes, "04-Apuntes-Cornell-Arte-Conceptual.pdf"));

    var articulo = EditorialTemplate.For(Format.All.Single(f => f.Name == "Artículo"))[1].Create(Format.All.Single(f => f.Name == "Artículo"), "Topologías WAN Híbridas y Segmentación Empresarial");
    articulo.Author = "Laboratorio de Redes y Sistemas";
    articulo.Subtitle = "Análisis comparativo de enlaces de consumo, fibra óptica y control de acceso";
    articulo.CollectionId = "Ejemplos de complejidad";
    articulo.CoverColor = "#4A3C6E";
    articulo.CoverImage = topology;
    articulo.Description = "Artículo técnico de revisión sobre arquitecturas WAN híbridas (coaxial, DSL, celular y enlaces dedicados), segmentación entre sedes y políticas de verificación ICMP.";
    articulo.DocumentFacts["Afiliación"] = "División de Ingeniería en Sistemas e Interacción de Redes";
    articulo.DocumentFacts["Palabras clave"] = "WAN, Fibra óptica, GigabitEthernet, Enlace serial, DHCP, Segmentación";
    articulo.DocumentFacts["Correspondencia"] = "laboratorio-redes@estudio-nimbo.org";
    articulo.Chapters = [
        new Chapter
        {
            Title = "1. Resumen y arquitectura de interconexión",
            Order = 0,
            Detail = "Topología híbrida con dos redes domésticas, red celular y núcleo corporativo",
            Layout = Illustrations.Gallery,
            Images = [topology, serverImg],
            ImageLayouts = new() { [0] = Illustrations.Gallery, [1] = Illustrations.Gallery },
            ImageCaptions = new()
            {
                [0] = "Figura 1 — Topología general WAN híbrida",
                [1] = "Figura 2 — Servidor web central y políticas de acceso"
            },
            PlainText = """
El presente estudio examina la coexistencia de tecnologías WAN de consumo (cable coaxial, línea telefónica DSL y red celular) junto con enlaces empresariales de fibra óptica y líneas seriales dedicadas.
[[Imagen 1]][[Imagen 2]]
| Segmento de red | Medio físico de enlace | Asignación y puerto | Política de aislamiento |
|---|---|---|---|
| Home Cable / DSL | Coaxial con splitter y par telefónico | DHCP dinámico del ISP hacia puerto Internet | Acceso a servicios públicos sin visibilidad a HQNet |
| Red móvil celular | Enlace inalámbrico a torre celular | IP 198.51.100.100 asignada vía DHCP celular | Tráfico aislado de las subredes corporativas |
| StoreNet y Sede HQ | Fibra óptica GigabitEthernet y enlace serial | Enlace punto a punto dedicado al proveedor | Aislamiento interno frente a pings externos no autorizados |
"""
        },
        new Chapter
        {
            Title = "2. Conclusiones y referencias",
            Order = 1,
            Detail = "Verificación de alcanzabilidad selectiva hacia el servidor público",
            PlainText = """
La validación experimental confirma que la conectividad hacia el servidor web público debe mantenerse abierta para todos los segmentos, mientras que las listas de control de acceso bloquean el tráfico ICMP directo entre redes domésticas y la sede corporativa (`HQNet`).

### Referencias bibliográficas
1. Laboratorio de Interacción de Redes (2026). *Guía experimental de conceptos WAN y segmentación en Packet Tracer*.
2. Tanenbaum, A. S. & Wetherall, D. J. *Redes de computadoras: arquitectura de enlaces dedicados y políticas de ruteo*.
"""
        }
    ];
    examples.Add((articulo, "05-Articulo-Topologias-WAN.pdf"));

    var cv = EditorialTemplate.For(Format.All.Single(f => f.Name == "CV"))[0].Create(Format.All.Single(f => f.Name == "CV"), "Elena Valdés · Dirección de Arte Técnico");
    cv.Author = "Elena Valdés";
    cv.Subtitle = "Arquitecta de Entornos Interactivos y Diseño Editorial";
    cv.CollectionId = "Ejemplos de complejidad";
    cv.CoverColor = "#7A3E65";
    cv.CoverImage = portraitAvatar;
    cv.Description = "Currículum vitae estructurado con perfil profesional, experiencia en dirección de arte y diseño de niveles, formación académica y competencias técnicas.";
    cv.Contact["Nombre profesional"] = "Elena Valdés Navarro";
    cv.Contact["Profesión"] = "Directora de Arte Conceptual y Diseñadora de Sistemas Visuales";
    cv.Contact["Correo"] = "elena.valdes@miramar-studio.org";
    cv.Contact["Teléfono"] = "+52 (444) 812 4900";
    cv.Contact["Ciudad"] = "San Luis Potosí, S.L.P.";
    cv.Contact["Portafolio"] = "miramar-studio.org/elena-valdes";
    cv.Contact["Perfil profesional en línea"] = "github.com/elena-valdes-art";
    cv.Chapters[0].PlainText = "Especialista en dirección de arte para RPG tácticos, diseño de interfaces diegéticas 16:9 y automatización de documentación técnica y editorial mediante LaTeX.";
    cv.Chapters[1].Entries.AddRange([
        new ResumeEntry
        {
            Heading = "Directora de Arte y Diseño de Niveles",
            Organization = "Estudio Miramar · Proyecto Lantern & Labyrinth",
            Period = "2024 – 2026",
            Description = "- Diseñé la identidad visual, moodboards y paletas cromáticas de 5 estratos subterráneos.\n- Coordiné la integración de mapas isométricos en Tiled y la arquitectura de 3 sistemas HUD."
        },
        new ResumeEntry
        {
            Heading = "Diseñadora de Sistemas Editoriales",
            Organization = "Taller Nimbo",
            Period = "2022 – 2024",
            Description = "- Estandaricé plantillas tipográficas automatizadas para informes técnicos, cuadernillos A6 y presentaciones Beamer."
        }
    ]);
    cv.Chapters[2].Entries.Add(new ResumeEntry
    {
        Heading = "Ingeniería en Sistemas Inteligentes",
        Organization = "Facultad de Ingeniería · Especialidad en Gráficos e Interfaces",
        Period = "2021 – 2026",
        Description = "Investigación aplicada en composición algorítmica, visualización 3D y herramientas de autor."
    });
    cv.Chapters[3].PlainText = "- **Arte y nivel:** Tiled, Krita, Blender, Adobe Color, diseño de HUD e iluminación ambiental.\n- **Ingeniería y edición:** C#, .NET, Avalonia UI, SQLite, XeLaTeX y control de versiones Git.";
    examples.Add((cv, "06-CV-Direccion-Arte-Tecnico.pdf"));

    var carta = EditorialTemplate.For(Format.All.Single(f => f.Name == "Carta"))[1].Create(Format.All.Single(f => f.Name == "Carta"), "Solicitud de Cierre y Reasignación de Proyecto");
    carta.Author = "Comité Técnico de Desarrollo";
    carta.Subtitle = "Oficio formal de redefinición de alcance hacia proyecto terminal";
    carta.CollectionId = "Ejemplos de complejidad";
    carta.CoverColor = "#9B342E";
    carta.Description = "Carta formal institucional con lugar y fecha alineados a la derecha, asunto, destinatario, exposición técnica de alcance y bloque triple de firmas.";
    carta.DocumentFacts["Lugar y fecha"] = "Ciudad Miramar, a 23 de noviembre de 2026";
    carta.DocumentFacts["Asunto"] = "Conclusión de etapa preliminar y transición a módulo de proyecto integrador";
    carta.DocumentFacts["Destinatario"] = "Dr. Aurelio Mendoza Esquivel";
    carta.DocumentFacts["Dirección"] = "Departamento de Vinculación y Proyectos Técnicos · División de Ingeniería";
    carta.Chapters = [
        new Chapter
        {
            Title = "Exposición de motivos",
            Order = 0,
            PlainText = """
Por medio de la presente, quienes suscribimos este documento, integrantes del equipo de arquitectura de software y diseño de sistemas, solicitamos de la manera más atenta la **autorización de cierre de la fase preliminar** del sistema de gestión departamental iniciado en el ciclo de verano.

Durante las sesiones de levantamiento de requerimientos, modelado de base de datos y definición de arquitectura modular, el proyecto experimentó una expansión sustantiva en sus alcances operativos. Tras evaluar la matriz de entregables junto con la coordinación técnica y el asesor de diseño, determinamos que la complejidad actual corresponde a un **proyecto terminal de ingeniería**, superando el marco acotado de la fase preliminar.

Por lo anterior, agradecemos se autorice la conclusión formal de esta primera etapa para dar continuidad al desarrollo bajo la modalidad de proyecto integrador, quedando a su disposición para entregar la documentación de arquitectura y esquemas generados hasta la fecha.

[[Firmas: Ing. Mateo Solís Rentería | Ing. Valeria Cárdenas Montes | Vo. Bo. Coordinación Técnica]]
"""
        }
    ];
    examples.Add((carta, "07-Carta-Formal-Reasignacion.pdf"));

    var acta = EditorialTemplate.For(Format.All.Single(f => f.Name == "Acta"))[1].Create(Format.All.Single(f => f.Name == "Acta"), "Acta de Comité: Aprobación de Vertical Slice");
    acta.Author = "Secretaría Técnica del Estudio";
    acta.Subtitle = "Sesión ordinaria de revisión de arte, niveles e interfaz";
    acta.CollectionId = "Ejemplos de complejidad";
    acta.CoverColor = "#475E4B";
    acta.Description = "Acta de sesión con cuadro de quórum, revisión de entregables, tabla de acuerdos con responsables y fechas límite, y firmas oficiales.";
    acta.DocumentFacts["Fecha de reunión"] = "23 de noviembre de 2026 · 18:30 h";
    acta.DocumentFacts["Lugar"] = "Sala de Revisión Editorial · Estudio Miramar";
    acta.DocumentFacts["Participantes"] = "Dirección de Arte, Diseño de Niveles, Ingeniería de Interfaz y QA";
    acta.DocumentFacts["Secretaría"] = "Coordinación de Producción Editorial";
    acta.Chapters = [
        new Chapter
        {
            Title = "1. Verificación de quórum y orden del día",
            Order = 0,
            Detail = "Asistencia completa de las 4 células de desarrollo",
            PlainText = """
Reunidos los responsables de cada área técnica, se declara instalada la sesión con el objetivo de dictaminar la entrega del **Vertical Slice (Estratos L01 a L03 y Piso 85)**.

1. Revisión de consistencia cromática en los 6 moodboards de la Fosa del Juramento.
2. Aprobación del mapa isométrico final del Piso 85 en Tiled y balance del jefe GRIM.
3. Congelación de las tres variantes de interfaz HUD en resolución 1920x1080.
"""
        },
        new Chapter
        {
            Title = "2. Acuerdos, responsables y firmas",
            Order = 1,
            Detail = "Cierre de entregables comprometido para el 30 de noviembre de 2026",
            PlainText = """
| Acuerdo aprobado | Área responsable | Fecha límite | Estado |
|---|---|---|---|
| Exportar catálogo definitivo de concept arts en PNG 1080p | Dirección de Arte | 26/11/2026 | Aprobado |
| Ajustar curva de daño de GRIM en Fase 2 con apoyo de clérigos | Diseño de Niveles | 28/11/2026 | En ejecución |
| Empaquetar dossier editorial completo en PDF y archivo LaTeX | Producción Editorial | 30/11/2026 | Completado |

[[Firmas: Presidencia del Comité Técnico | Secretaría de Actas y Acuerdos]]
"""
        }
    ];
    examples.Add((acta, "08-Acta-Comite-Vertical-Slice.pdf"));

    var documento = EditorialTemplate.For(Format.All.Single(f => f.Name == "Documento"))[1].Create(Format.All.Single(f => f.Name == "Documento"), "Guía de Producción Visual: Ríos a la Necrópolis");
    documento.Author = "Equipo de Documentación Técnica";
    documento.Subtitle = "Estándares de composición, nomenclatura y validación de recursos";
    documento.CollectionId = "Ejemplos de complejidad";
    documento.CoverColor = "#2B5B78";
    documento.CoverImage = l02Final;
    documento.Description = "Guía técnica de trabajo con imagen de referencia, protocolo de validación, paleta cromática de control y tabla de especificaciones de exportación.";
    documento.DocumentFacts["Organización"] = "Taller Editorial Nimbo · Área de Estándares";
    documento.DocumentFacts["Referencia"] = "GUIA-VIS-2026-L02";
    documento.Chapters = [
        new Chapter
        {
            Title = "1. Finalidad y criterios de composición",
            Order = 0,
            Detail = "Normativa aplicable a los pisos 78 y 79 antes del umbral de la Necrópolis",
            Images = [l02Final],
            ImageCaptions = new() { [0] = "L02 — Ríos a la Necrópolis: arquitectura pálida, cascadas y vegetación blanca." },
            PlainText = """
Esta guía establece los parámetros visuales y técnicos que deben cumplir todas las láminas de entorno correspondientes al estrato **L02 — Ríos a la Necrópolis**.
[[Imagen 1]]
> La transición entre los pisos 78 y 79 debe sustituir progresivamente el musgo oscuro de los niveles superiores por enredaderas blancas y piedra caliza húmeda.

[[Paleta: #ECEAE2 Caliza pálida | #B8C4C8 Agua fría | #7A8B90 Roca húmeda | #4B5D63 Penumbra | #1E282C Abismo]]
| Parámetro técnico | Especificación obligatoria | Herramienta de control |
|---|---|---|
| Resolución base | 1920 x 1080 píxeles en proporción 16:9 sin recorte | Verificador de activos PNG/JPG |
| Contraste de ruta | Diferencia mínima de 40 puntos de luminancia en escaleras | Comprobación en escala de grises |
"""
        }
    ];
    examples.Add((documento, "09-Documento-Guia-Produccion-L02.pdf"));

    var novela = EditorialTemplate.For(Format.All.Single(f => f.Name == "Novela"))[1].Create(Format.All.Single(f => f.Name == "Novela"), "Crónicas de la Fosa del Juramento");
    novela.Author = "Inés Salgado";
    novela.Subtitle = "El descenso a la Cámara del Sol";
    novela.CollectionId = "Ejemplos de complejidad";
    novela.CoverColor = "#284B52";
    novela.CoverLettering = "Tejuelo";
    novela.CoverImage = l03Final;
    novela.BackImage = l01Final;
    novela.Publication.Paper = "A5";
    novela.Description = "Durante tres generaciones ningún clan de Ciudad Miramar logró cruzar el piso ochenta y cinco. Cuando la capitana Mara Vélez enciende la última linterna frente a los árboles blancos de la Cámara del Sol, comprende que el laberinto recuerda el nombre de todos los que bajaron antes.";
    novela.Chapters[0].Title = "Arco I: El umbral de piedra";
    novela.Chapters[1].Title = "Capítulo 1: Las escaleras de Miramar";
    novela.Chapters[1].Images = [l01Final, l02Final];
    novela.Chapters[1].ImageLayouts = new() { [0] = "Ajustar a la derecha", [1] = "Lámina ilustrada" };
    novela.Chapters[1].ImageCaptions = new() { [0] = "La escalinata central envuelta en niebla.", [1] = "Las cascadas pálidas del estrato setenta y nueve." };
    novela.Chapters[1].PlainText = """
A medianoche, los faroles de la plaza apenas alcanzaban a teñir de ámbar los primeros veinte peldaños de la Fosa del Juramento. Mara ajustó la correa del escudo y contó a los integrantes del segundo escuadrón antes de dar la orden de descenso.
[[Imagen 1]]
Abajo, el rumor del agua subterránea subía entre los muros de ladrillo como una respiración antigua. Ninguno de los gremios había querido escoltarlos más allá del bosque inicial, pero las tablillas de cobre que llevaban en el morral marcaban con precisión las rutas de patrulla hasta el estrato setenta y nueve.

> —No miren la niebla —dijo Mara mientras bajaban el último tramo—. Miren únicamente la piedra donde alcanza la luz del farol.
[[Imagen 2]]
Cuando cruzaron los Ríos a la Necrópolis, las enredaderas dejaron de ser verdes. Sobre la bóveda de caliza caían cortinas de agua helada entre raíces blancas que parecían señalar el camino hacia la cúpula de cristal.
""";
    var mundoMiramar = new Node
    {
        Name = "Ciudad Miramar",
        NodeKind = "Lugar",
        Role = "Ciudad amurallada construida alrededor de la Fosa del Juramento",
        ImagePath = l01Final,
        Background = "Centro económico de los gremios de exploradores.",
        AttributeDefinitions = [new FieldDefinition { Name = "Profundidad registrada", Unit = "pisos", DefaultValue = "85" }]
    };
    var camaraSol = new Node
    {
        Name = "Cámara del Sol",
        NodeKind = "Lugar",
        ParentId = mundoMiramar.Id,
        Role = "Cúpula de cristal en el piso 85 custodiada por GRIM",
        ImagePath = l03Final,
        Background = "Bosque subterráneo de árboles blancos y ríos carmesí."
    };
    var maraNode = new Node
    {
        Name = "Mara Vélez",
        NodeKind = "Personaje",
        Role = "Capitana del sexto escuadrón de Miramar",
        Age = "29 años",
        ImagePath = portraitAvatar,
        RelatedIds = [camaraSol.Id],
        RelationRoles = new() { [camaraSol.Id] = "Líder de la expedición al piso 85" },
        Background = "Veterana de las rutas del norte que cartografió los pasos de la Necrópolis."
    };
    novela.Glossary.AddRange([mundoMiramar, camaraSol, maraNode]);
    examples.Add((novela, "10-Novela-Cronicas-Fosa-Juramento.pdf"));

    foreach (var (work, fileName) in examples)
    {
        Upsert(work);
        var targetPdf = Path.Combine(pdfOut, fileName);
        await Publisher.Export(work, shelf.Media, targetPdf, true, false, engine);
        Check(File.Exists(targetPdf) && new FileInfo(targetPdf).Length > 1000, "PDF exportado: " + fileName);
        Console.WriteLine($"Exportado: {fileName} ({new FileInfo(targetPdf).Length:N0} bytes)");
    }
    var glossaryPdf = Path.Combine(pdfOut, "11-Glosario-Cronicas-Fosa-Juramento.pdf");
    await Publisher.Export(novela, shelf.Media, glossaryPdf, true, true, engine);
    Check(File.Exists(glossaryPdf) && new FileInfo(glossaryPdf).Length > 1000, "Glosario PDF exportado");
    Console.WriteLine($"Exportado: 11-Glosario-Cronicas-Fosa-Juramento.pdf ({new FileInfo(glossaryPdf).Length:N0} bytes)");
}










