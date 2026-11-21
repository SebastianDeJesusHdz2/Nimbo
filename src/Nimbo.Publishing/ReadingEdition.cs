using Nimbo.Core;

namespace Nimbo.Publishing;

public static class ReadingEdition
{
    public static async Task Write(Work work,string media,string newFolder,string? engine)
    {
        var destination=Path.GetFullPath(newFolder);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("Elige una carpeta nueva para esta edición.");
        var parent=Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var staging=parent.CreateSubdirectory(".nimbo-"+Guid.NewGuid().ToString("N")).FullName;
        try{
            Task Print(string file,bool glossary=false,string? id=null,IReadOnlySet<string>? selection=null)
                =>Publisher.Export(work,media,Path.Combine(staging,file),true,glossary,engine,id,selection);
            await Print("Manuscrito.pdf");
            await Publisher.Export(work,media,Path.Combine(staging,"Proyecto LaTeX.zip"),false);
            if(work.Glossary.Count>0){
                await Print("Glosario.pdf",true);
                var people=work.Glossary.Where(n=>n.NodeKind.Equals("Personaje",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(people.Length>0){
                    await Print("Personajes.pdf",true,selection:people.Select(n=>n.Id).ToHashSet());
                    Directory.CreateDirectory(Path.Combine(staging,"Fichas"));
                    int number=0;
                    foreach(var person in people){
                        var name=string.Concat(person.Name.Select(c=>c<' ' || "<>:\"/\\|?*".Contains(c)?'_':c));
                        if(name.Length>70)name=name[..70];
                        await Print(Path.Combine("Fichas",$"{++number:00} - {name}.pdf"),true,person.Id);
                    }
                }
            }
            Directory.Move(staging,destination);
        }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
}
