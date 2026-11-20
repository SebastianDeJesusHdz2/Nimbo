using System.IO.Compression;
using Nimbo.Core;
using Nimbo.Storage;
using Nimbo.Publishing;
using System.Text.Json;
using System.Text;

var root = Directory.CreateTempSubdirectory("nimbo-check-").FullName;
try
{
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
    var legacy = Path.Combine(root, "anterior"); var studio = Directory.CreateDirectory(Path.Combine(legacy, "Data", "Studio")).FullName;
    var old = Format.All[0].Create("Prueba migrada");
    var race = new Node { Name = "Elfos", AttributeDefinitions = [new() { Name = "Orejas", DefaultValue = "8 cm" }] };
    var elf = new Node { Name = "Ada", BaseId = race.Id }; old.Glossary.AddRange([race, elf]);
    old.Chapters[0].PlainText = "## Inicio\nLínea inicial.\n- Segunda línea.\n| Columna A | Columna B |\n| Uno | Dos |";
    var png = Path.Combine(root, "figura.png");
    File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAzSURBVEhLY4ift+A/LTEDugC18agFBPGoBQTxqAUE8agFBPGoBQTxqAUE8agFBDHNLQAAbzffTAnMZXYAAAAASUVORK5CYII="));
    var image = shelf.AddImage(png);
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







