using Microsoft.Data.Sqlite;

namespace Nimbo.Storage;

public static class UserLibrary
{
    public static string OpenDirectory(string localData)
    {
        var parent=Path.Combine(localData,"NimboStudio");
        var target=Path.Combine(parent,"Biblioteca");
        Directory.CreateDirectory(parent);
        if(Directory.Exists(target))return target;
        var legacy=Path.Combine(localData,"Nimbo");
        var source=Path.Combine(legacy,"nimbo.sqlite");
        if(!File.Exists(source)){Directory.CreateDirectory(target);return target;}
        var staging=Directory.CreateDirectory(Path.Combine(parent,".import-"+Guid.NewGuid().ToString("N"))).FullName;
        try {
            using(var old=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()))
            using(var fresh=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(staging,"nimbo.sqlite"),Pooling=false}.ToString())){
                old.Open();fresh.Open();old.BackupDatabase(fresh);
            }
            var oldMedia=Path.Combine(legacy,"media");
            if(Directory.Exists(oldMedia)){
                var folder=Directory.CreateDirectory(Path.Combine(staging,"media")).FullName;
                foreach(var path in Directory.EnumerateFiles(oldMedia))
                    File.Copy(path,Path.Combine(folder,Path.GetFileName(path)));
            }
            foreach(var name in new[]{"autor.txt","tema.txt","xelatex.txt"}){
                var path=Path.Combine(legacy,name);
                if(File.Exists(path))File.Copy(path,Path.Combine(staging,name));
            }
            try{Directory.Move(staging,target);}catch(IOException) when(Directory.Exists(target)){}
            return target;
        }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
}
