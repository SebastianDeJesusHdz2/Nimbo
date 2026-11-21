using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Nimbo.Core;

namespace Nimbo.Storage;

public static class Legacy
{
    public static int Import(Shelf shelf, string folder)
    {
        var root = Path.GetFullPath(folder);
        var library = Path.Combine(root, "Data", "Studio", "library.json");
        var documents = Path.Combine(root, "Data", "Documentos", "estudio.sqlite");
        if (!File.Exists(library) && !File.Exists(documents))
            throw new InvalidDataException("Selecciona la carpeta de LumenWriter que contiene Data.");
        var known = shelf.List().Select(e => e.Id).ToHashSet();
        int added = 0;
        string Image(string relative, string directory)
        {
            var path = relative.Replace('\\', Path.DirectorySeparatorChar);
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile) path = uri.LocalPath;
            var full = Path.GetFullPath(path, directory);
            var rel = Path.GetRelativePath(root, full);
            if (Path.IsPathRooted(rel) || rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar)) throw new InvalidDataException("La obra referencia una imagen fuera de su biblioteca. Copia el medio a LumenWriter antes de importar.");
            return shelf.AddImage(full);
        }
        void Save(Work w) { shelf.Save(w, 0); known.Add(w.Id); added++; }
        if (File.Exists(library))
        {
            using var input = File.OpenRead(library);
            var catalog = JsonSerializer.Deserialize<Library>(input, Shelf.Json) ?? throw new InvalidDataException("Biblioteca ilegible.");
            var workshops = Path.Combine(root, "Obras");
            var editionFolders = Directory.Exists(workshops) ? Directory.GetDirectories(workshops) : [];
            foreach (var w in catalog.Projects)
            {
                var editionFolder = editionFolders.FirstOrDefault(f => Path.GetFileName(f).Equals(w.Title, StringComparison.OrdinalIgnoreCase));
                if (editionFolder != null)
                {
                    var reading = Path.Combine(editionFolder, "Edición de lectura");
                    var pdfs = Directory.Exists(reading) ? Directory.EnumerateFiles(reading, "*.pdf") : Enumerable.Empty<string>();
                    foreach (var file in pdfs.Concat(Directory.EnumerateFiles(editionFolder, "*.zip")))
                        w.Editions.Add(new(Path.GetFileName(file), shelf.AddAsset(file)));
                }
                if (known.Contains(w.Id))
                {
                    if (w.Editions.Count > 0)
                    {
                        var existing = shelf.Open(w.Id);
                        var additions = w.Editions.Where(f => !existing.Work.Editions.Any(e => e.Key == f.Key)).ToList();
                        if (additions.Count > 0) { existing.Work.Editions.AddRange(additions); shelf.Save(existing.Work, existing.Revision); }
                    }
                    continue;
                }
                var collection = catalog.Collections.FirstOrDefault(g => g.Id == w.CollectionId); if (collection != null) w.CollectionId = collection.Name;
                foreach(var key in w.TexResources.Keys.ToList()){
                    var full=Path.GetFullPath(w.TexResources[key],root);
                    var relative=Path.GetRelativePath(root,full);
                    if(Path.IsPathRooted(relative)||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar))throw new InvalidDataException("Recurso fuera de la biblioteca.");
                    w.TexResources[key]=shelf.AddAsset(full);
                }
                if (w.BackImage != "") w.BackImage = Image(w.BackImage, root);
                if (w.CoverImage != "") w.CoverImage = Image(w.CoverImage, root);
                foreach (var node in w.Glossary.Where(n => n.ImagePath != "")) node.ImagePath = Image(node.ImagePath, root);
                foreach(var chapter in w.Chapters){
                    for(int index=0;index<chapter.Images.Count;index++)chapter.Images[index]=Image(chapter.Images[index],root);
                }
                foreach (var c in w.Chapters.Where(c => c.ContentPackage.StartsWith("LW5:") || c.ContentPackage.StartsWith("LW4:")))
                {
                    var xml = XDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(c.ContentPackage[4..])));
                    var pictures = new Dictionary<XElement, int>();
                    foreach (var img in xml.Descendants().Where(e => e.Name.LocalName == "Image"))
                    {
                        var path = (string?)img.Attribute("Tag") ?? (string?)img.Attribute("Source") ??
                            (string?)img.Descendants().FirstOrDefault(e => e.Name.LocalName == "BitmapImage")?.Attribute("UriSource");
                        if (!string.IsNullOrWhiteSpace(path)) { pictures[img] = c.Images.Count; c.Images.Add(Image(path, root)); }
                    }
                    var readable = LegacyText.Read(xml.Root!, pictures); if (c.PlainText.Length == 0 || LegacyText.SameWords(c.PlainText, readable)) c.PlainText = readable;
                }
                Save(w);
            }
        }
        if (File.Exists(documents))
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = documents, Mode = SqliteOpenMode.ReadOnly }.ToString());
            db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT contenido FROM documentos";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                using var document = JsonDocument.Parse(reader.GetString(0));
                var d = document.RootElement;
                static string Get(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
                var id = Get(d, "id"); if (known.Contains(id)) continue;
                var w = new Work
                {
                    Id = id,
                    Title = Get(d, "title"),
                    Author = Get(d, "author"),
                    Subtitle = Get(d, "subtitle"),
                    Type = Get(d, "type"),
                    Publication = new() { Paper = Get(d, "paper") },
                    Extra = new() { ["LegacyDocument"] = d.Clone() }
                };
                foreach (var b in d.GetProperty("blocks").EnumerateArray())
                {
                    var c = new Chapter { Id = Get(b, "id"), Title = Get(b, "title"), PlainText = Get(b, "body"), Detail = Get(b, "extra"), Summary = Get(b, "summary"), Layout = Get(b, "layout"), Order = w.Chapters.Count };
                    foreach (var key in b.GetProperty("images").EnumerateArray()) c.Images.Add(Image(key.GetString()!, Path.Combine(root, "Data", "Documentos", "Media")));
                    w.Chapters.Add(c);
                }
                Save(w);
            }
        }
        return added;
    }
}



