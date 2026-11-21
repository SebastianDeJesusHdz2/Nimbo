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
    page.PlainText="Mara sabía que el puerto estaba despierto por el roce del cabo contra la madera.\n[[Imagen 1]][[Imagen 2]]\n> Cuenta las brazas antes de entrar.\n[[Imagen 3]]\nAl otro lado del canal ya amanecía.";
    shelf.Save(guide,0);guide=shelf.Open(guide.Id).Work;
    async Task<string> Source(bool glossary){
        var output=Path.Combine(root,"prueba-maqueta.zip");await Publisher.Export(guide,shelf.Media,output,false,glossary);
        using var zip=ZipFile.OpenRead(output);using var reader=new StreamReader(zip.GetEntry("obra.tex")!.Open());return reader.ReadToEnd();
    }
    var pages=await Source(false);Check(pages.Contains(@"\begin{minipage}[t]{.475\linewidth}") && pages.Contains(@"Orilla \& marea") && pages.Contains(@"\begin{quote}") && pages.Contains("El faro"),"disposiciones y citas imprimibles");
    var pocket=await Source(true);Check(pocket.Contains("paperwidth=90mm,paperheight=140mm") && pocket.Contains("Lugar de guardia") && pocket.Contains("Marea de referencia (m) · Puerto Espejo") && pocket.Contains("Cuenta las brazas en voz baja."),"cuadernillo conserva campos heredados y vínculos");
    if(args.Contains("--pdf")){
        var output=Environment.GetEnvironmentVariable("NIMBO_SAMPLE_DIR")??root;
        await Publisher.Export(guide,shelf.Media,Path.Combine(output,"Cuadernillo de bolsillo.pdf"),true,true,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
        await Publisher.Export(guide,shelf.Media,Path.Combine(output,"Composición ilustrada.pdf"),true,false,Environment.GetEnvironmentVariable("NIMBO_XELATEX"));
    }
    var paragraph = new Chapter { Title = "Prueba de estilo", PlainText = "Una palabra", Order = 1 };
    paragraph.ContentPackage = "LW5:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("<FlowDocument><Paragraph><Run>Una </Run><Bold><Run>palabra</Run></Bold></Paragraph></FlowDocument>"));
    old.Chapters.Add(paragraph);
    var filename = Path.Combine(studio, "library.json");
    File.WriteAllText(filename, JsonSerializer.Serialize(new Library { Projects = [old] }));
    var reading = Directory.CreateDirectory(Path.Combine(legacy, "Obras", old.Title, "Edición de lectura")).FullName;
    var previous = Path.Combine(reading, "Libro.pdf");
    File.WriteAllText(previous, "%PDF-reference");
    Check(Legacy.Import(shelf, legacy) == 1 && Legacy.Import(shelf, legacy) == 0, "importación repetible");
    Check(shelf.Open(old.Id).Work.Chapters[1].PlainText == "Una **palabra**", "conversión del énfasis");
    var savedEdition = shelf.Open(old.Id).Work.Editions.Single(); Check(File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(Path.Combine(shelf.Media, savedEdition.Key))), "edición anterior idéntica");
    Check(WorldModel.Effective(shelf.Open(old.Id).Work, elf).Single().Value == "8 cm", "herencia");
    if (args.Contains("--pdf"))
    {
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










