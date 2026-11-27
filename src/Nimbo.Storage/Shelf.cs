using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Nimbo.Core;

namespace Nimbo.Storage;

public sealed record Entry(
    string Id,
    string Title,
    string Type,
    long Revision,
    string Cover,
    string Color,
    string Author,
    string Description,
    string Collection,
    string Lettering = "Automática",
    string Subtitle = "",
    string Template = "",
    int ChapterCount = 1,
    string FirstSection = "",
    string SecondSection = "",
    string ThirdSection = "",
    string PrimaryFact = "",
    string SecondaryFact = "")
{
    public override string ToString() => Title;

    public static Entry FromWork(Work work, long revision = 0)
    {
        var ordered = WorldModel.Ordered(work).ToList();
        string Fact(params string[] keys)
        {
            foreach (var k in keys)
            {
                if (work.Contact.TryGetValue(k, out var cv) && !string.IsNullOrWhiteSpace(cv)) return cv.Trim();
                if (work.DocumentFacts.TryGetValue(k, out var dv) && !string.IsNullOrWhiteSpace(dv)) return dv.Trim();
            }
            return "";
        }
        var (pFact, sFact) = work.Type switch
        {
            "CV" => (Fact("Profesión", "Nombre profesional"), Fact("Ciudad", "Correo", "Portafolio")),
            "Apuntes" => (Fact("Asignatura", "Universidad"), Fact("Universidad", "Clave de clase", "Periodo")),
            "Informe" => (Fact("Materia o área", "Institución", "Departamento"), Fact("Periodo del informe", "Fecha", "Realizada por")),
            "Carta" => (Fact("Destinatario", "Asunto"), Fact("Lugar y fecha", "Dirección")),
            "Propuesta" => (Fact("Cliente", "Responsable"), Fact("Presupuesto", "Vigencia")),
            "Acta" => (Fact("Fecha de reunión", "Lugar"), Fact("Lugar", "Secretaría")),
            "Artículo" => (Fact("Afiliación", "Palabras clave"), Fact("Palabras clave", "Correspondencia")),
            "Presentación" => (Fact("Evento", "Institución"), Fact("Fecha", "Institución")),
            _ => (Fact("Organización", "Referencia"), Fact("Referencia"))
        };
        return new Entry(
            work.Id,
            work.Title,
            work.Type,
            revision,
            work.CoverImage,
            work.CoverColor,
            work.Author,
            work.Description,
            work.CollectionId,
            work.CoverLettering,
            work.Subtitle,
            work.Template,
            Math.Max(1, ordered.Count),
            ordered.Count > 0 ? ordered[0].Title : "",
            ordered.Count > 1 ? ordered[1].Title : "",
            ordered.Count > 2 ? ordered[2].Title : "",
            pFact,
            sFact);
    }
}
public sealed class Shelf : Nimbo.Application.IWorkRepository
{
    public string Root { get; }
    public string Media => Path.Combine(Root, "media");
    readonly string connectionString;
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public Shelf(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Media);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(Root, "nimbo.sqlite") }.ToString();
        using var db = Connect();
        using var init = db.CreateCommand();
        init.CommandText = """
          PRAGMA journal_mode=WAL;
          CREATE TABLE IF NOT EXISTS works(id TEXT PRIMARY KEY, title TEXT NOT NULL, type TEXT NOT NULL,
          revision INTEGER NOT NULL, content TEXT NOT NULL, edited TEXT NOT NULL);
          """;
        init.ExecuteNonQuery();
    }
    SqliteConnection Connect() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public List<Entry> List()
    {
        using var db = Connect(); using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT
              id,
              title,
              type,
              revision,
              coalesce(json_extract(content,'$.CoverImage'),''),
              coalesce(json_extract(content,'$.CoverColor'),''),
              coalesce(json_extract(content,'$.Author'),''),
              coalesce(json_extract(content,'$.Description'),''),
              coalesce(json_extract(content,'$.CollectionId'),''),
              coalesce(json_extract(content,'$.CoverLettering'),'Automática'),
              coalesce(json_extract(content,'$.Subtitle'),''),
              coalesce(json_extract(content,'$.Template'),''),
              coalesce(json_array_length(content,'$.Chapters'),1),
              coalesce(json_extract(content,'$.Chapters[0].Title'),''),
              coalesce(json_extract(content,'$.Chapters[1].Title'),''),
              coalesce(json_extract(content,'$.Chapters[2].Title'),''),
              coalesce(
                json_extract(content,'$.Contact.Profesión'),
                json_extract(content,'$.DocumentFacts.Asignatura'),
                json_extract(content,'$.DocumentFacts."Materia o área"'),
                json_extract(content,'$.DocumentFacts.Institución'),
                json_extract(content,'$.DocumentFacts.Destinatario'),
                json_extract(content,'$.DocumentFacts.Cliente'),
                json_extract(content,'$.DocumentFacts."Fecha de reunión"'),
                json_extract(content,'$.DocumentFacts.Afiliación'),
                json_extract(content,'$.DocumentFacts.Evento'),
                json_extract(content,'$.DocumentFacts.Organización'),
                ''),
              coalesce(
                json_extract(content,'$.Contact.Ciudad'),
                json_extract(content,'$.DocumentFacts.Universidad'),
                json_extract(content,'$.DocumentFacts."Periodo del informe"'),
                json_extract(content,'$.DocumentFacts."Lugar y fecha"'),
                json_extract(content,'$.DocumentFacts.Presupuesto'),
                json_extract(content,'$.DocumentFacts.Lugar'),
                json_extract(content,'$.DocumentFacts."Palabras clave"'),
                json_extract(content,'$.DocumentFacts.Fecha'),
                json_extract(content,'$.DocumentFacts.Referencia'),
                '')
            FROM works ORDER BY edited DESC,title
            """;
        using var reader = cmd.ExecuteReader(); var items = new List<Entry>();
        while (reader.Read())
        {
            items.Add(new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetInt32(12),
                reader.GetString(13),
                reader.GetString(14),
                reader.GetString(15),
                reader.GetString(16),
                reader.GetString(17)));
        }
        return items;
    }
    public (Work Work, long Revision) Open(string id)
    {
        using var db = Connect(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT content,revision FROM works WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("La obra ya no está en la biblioteca.");
        return (JsonSerializer.Deserialize<Work>(reader.GetString(0), Json)!, reader.GetInt64(1));
    }
    public long Save(Work work, long revision)
    {
        if (string.IsNullOrWhiteSpace(work.Title)) throw new InvalidDataException("Escribe un título para guardar.");
        using var db = Connect(); using var cmd = db.CreateCommand();
        cmd.CommandText = revision == 0 ?
          "INSERT INTO works VALUES($id,$title,$type,1,$content,$edited)" :
          "UPDATE works SET title=$title,type=$type,revision=revision+1,content=$content,edited=$edited WHERE id=$id AND revision=$revision";
        cmd.Parameters.AddWithValue("$id", work.Id); cmd.Parameters.AddWithValue("$title", work.Title);
        cmd.Parameters.AddWithValue("$type", work.Type); cmd.Parameters.AddWithValue("$content", JsonSerializer.Serialize(work, Json));
        cmd.Parameters.AddWithValue("$edited", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$revision", revision);
        if (cmd.ExecuteNonQuery() != 1) throw new IOException("La obra cambió en otra ventana. Guarda una copia para conservar ambas versiones.");
        return revision + 1;
    }
    public string AddImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg")) throw new InvalidDataException("Elige una imagen PNG o JPEG.");
        return AddAsset(path);
    }
    public string AddAsset(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        using var source = File.OpenRead(path);
        var name = Convert.ToHexStringLower(SHA256.HashData(source)) + ext;
        var target = Path.Combine(Media, name);
        if (File.Exists(target)) return name;
        source.Position = 0; var tmp = target + "." + Guid.NewGuid().ToString("N");
        try
        {
            using (var dest = File.Create(tmp)) source.CopyTo(dest);
            try { File.Move(tmp, target); } catch (IOException) when (File.Exists(target)) { }
        }
        finally { File.Delete(tmp); }
        return name;
    }
}



