using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Nimbo.Core;

namespace Nimbo.Storage;

public sealed record Entry(string Id, string Title, string Type, long Revision, string Cover, string Color, string Author, string Description, string Collection)
{
    public override string ToString() => Title;
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
        cmd.CommandText = "SELECT id,title,type,revision,coalesce(json_extract(content,'$.CoverImage'),''),coalesce(json_extract(content,'$.CoverColor'),''),coalesce(json_extract(content,'$.Author'),''),coalesce(json_extract(content,'$.Description'),''),coalesce(json_extract(content,'$.CollectionId'),'') FROM works ORDER BY edited DESC,title";
        using var reader = cmd.ExecuteReader(); var items = new List<Entry>();
        while (reader.Read()) items.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8)));
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
    internal string AddAsset(string path)
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



