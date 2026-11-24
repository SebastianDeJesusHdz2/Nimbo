using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Nimbo.Core;
using System.Text.RegularExpressions;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    void CoverSettings(StackPanel panel,Work work)
    {
        if(work.Type!="Presentación"){
            Choice(panel,"Composición de portada",["Por formato","Al pie","Centrada","Cabecera"],work.CoverStyle,v=>{work.CoverStyle=v;Changed();});
            Choice(panel,"Marco de las páginas",["Sin marco","Filete","Doble filete","Esquinas"],work.FrameStyle,v=>{work.FrameStyle=v;Changed();});
            var subtitle=new CheckBox{Content="Imprimir el subtítulo en la portada",IsChecked=work.ShowCoverSubtitle};
            subtitle.IsCheckedChanged+=(_,_)=>{work.ShowCoverSubtitle=subtitle.IsChecked==true;Changed();};panel.Children.Add(subtitle);
        }
        if(work.IsNovel){
            var back=Row();panel.Children.Add(back);
            var preview=new ContentControl();panel.Children.Add(preview);
            void ShowBack(){preview.Content=work.BackImage==""?null:Thumbnail(Path.Combine(shelf.Media,Path.GetFileName(work.BackImage)),300,110);}
            ShowBack();
            back.Children.Add(Action("Fondo de contraportada…",async()=>await Guard(async()=>{
                var path=(await StorageProvider.OpenFilePickerAsync(new(){Title="Fondo de contraportada",FileTypeFilter=[FilePickerFileTypes.ImageAll]})).FirstOrDefault()?.TryGetLocalPath();
                if(path==null)return;work.BackImage=shelf.AddImage(path);Changed();ShowBack();
            })));
            back.Children.Add(QuietAction("Quitar fondo",()=>{work.BackImage="";Changed();ShowBack();}));
        }
        var scope=new ComboBox{ItemsSource=work.IsNovel && work.Glossary.Count>0?new[]{"Manuscrito completo","Glosario completo","Elegir fichas…"}:new[]{work.IsNovel?"Manuscrito completo":"Documento completo"},SelectedIndex=0};
        var export=Row();export.Children.Add(scope);export.Children.Add(Action("Crear PDF…",async()=>{
            if(scope.SelectedIndex==2)await ChooseCards(work);else await Export(true,scope.SelectedIndex==1);
        }));panel.Children.Add(export);
    }

    void SourceSettings(StackPanel panel,Work work)
    {
        var controls=Column(8);
        var enabled=new CheckBox{Content="Componer con mi documento LaTeX",IsChecked=work.UseTexSource};
        enabled.IsCheckedChanged+=(_,_)=>{work.UseTexSource=enabled.IsChecked==true;Changed();};controls.Children.Add(enabled);
        controls.Children.Add(Label("Documento completo, desde la clase hasta el final. Al activar este modo, el archivo define sus portadas y páginas."));
        Choice(controls,"Motor del proyecto",["Automático","xelatex","pdflatex","lualatex"],work.TexEngine,v=>{work.TexEngine=v;Changed();});
        var source=Field(controls,"Fuente LaTeX",work.TexSource,s=>work.TexSource=s,true);
        source.MaxHeight=370;
        var resources=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};
        void Refresh(){resources.ItemsSource=work.TexResources.Keys.Order().ToArray();resources.SelectedIndex=0;}
        void Attach(string path,string relative){
            if(relative.StartsWith(".."+Path.DirectorySeparatorChar)||Path.IsPathRooted(relative)||relative.Equals("obra.tex",StringComparison.OrdinalIgnoreCase))
                throw new IOException("Selecciona recursos dentro de la carpeta del documento. obra.tex está reservado para la fuente principal.");
            work.TexResources[relative.Replace(Path.DirectorySeparatorChar,'/')]=shelf.AddAsset(path);Changed();
        }
        controls.Children.Add(Action("Cargar fuente .tex…",async()=>await Guard(async()=>{
            var path=(await StorageProvider.OpenFilePickerAsync(new(){Title="Documento principal",FileTypeFilter=[new("LaTeX"){Patterns=["*.tex"]}]})).FirstOrDefault()?.TryGetLocalPath();
            if(path==null)return;
            var content=await File.ReadAllTextAsync(path);
            var folder=Path.GetDirectoryName(path)!;
            var resourcesToKeep=new Dictionary<string,string>();
            var pending=new Queue<string>();pending.Enqueue(content);
            void Discover(string file){
                if(file.IndexOfAny(['\\','$','#','{','}'])>=0 || Path.IsPathRooted(file))return;
                var full=Path.GetFullPath(file,folder);var rel=Path.GetRelativePath(folder,full).Replace('\\','/');
                if(rel.StartsWith("../")||!File.Exists(full)||full==path||resourcesToKeep.ContainsKey(rel))return;
                resourcesToKeep[rel]=shelf.AddAsset(full);
                if(Path.GetExtension(rel) is ".sty" or ".tex" or ".cls")pending.Enqueue(File.ReadAllText(full));
            }
            while(pending.TryDequeue(out var text)){
                text=Regex.Replace(text,@"(?<!\\)%[^\r\n]*","");
                foreach(Match m in Regex.Matches(text,@"\\(usepackage|RequirePackage|documentclass|input|include|includegraphics)(?:\[[^\]]*\])?\{([^{}]+)\}")){
                    var command=m.Groups[1].Value;
                    foreach(var part in m.Groups[2].Value.Split(',',StringSplitOptions.TrimEntries)){
                        if(command is "usepackage" or "RequirePackage")Discover(part+".sty");
                        else if(command=="documentclass")Discover(part+".cls");
                        else if(command=="includegraphics" && !Path.HasExtension(part)){foreach(var ext in new[]{".pdf",".png",".jpg",".jpeg"})Discover(part+ext);}
                        else Discover(Path.HasExtension(part)?part:part+".tex");
                    }
                }
                foreach(Match m in Regex.Matches(text,@"\{([^{}\\#\r\n]+\.(?:png|jpeg|jpg|pdf|otf|ttf|bib))\}",RegexOptions.IgnoreCase))Discover(m.Groups[1].Value);
            }
            work.TexDirectory=folder;work.TexResources=resourcesToKeep;source.Text=content;
            enabled.IsChecked=true;Changed();Refresh();
        })));
        controls.Children.Add(Action("Adjuntar paquetes, imágenes o archivos…",async()=>await Guard(async()=>{
            var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Recursos del proyecto LaTeX",AllowMultiple=true});
            foreach(var item in files){if(item.TryGetLocalPath() is string path)Attach(path,work.TexDirectory==""?Path.GetFileName(path):Path.GetRelativePath(work.TexDirectory,path));}
            Refresh();
        })));
        controls.Children.Add(QuietAction("Adjuntar carpeta de recursos…",async()=>await Guard(async()=>{
            var dir=(await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta con imágenes, fuentes o paquetes"})).FirstOrDefault()?.TryGetLocalPath();
            if(dir==null)return;
            var root=work.TexDirectory==""?Path.GetDirectoryName(dir)!:work.TexDirectory;
            var prefix=Path.GetRelativePath(root,dir);
            if(prefix==".."||prefix.StartsWith(".."+Path.DirectorySeparatorChar)||Path.IsPathRooted(prefix))throw new IOException("Elige una carpeta del proyecto.");
            var extensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".pdf",".sty",".cls",".otf",".ttf",".bib",".bst",".csv",".tex",".svg",".eps"};
            var added=await Task.Run(()=>{
                var files=new Dictionary<string,string>();
                foreach(var path in Directory.EnumerateFiles(dir,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint})){
                    var rel=Path.GetRelativePath(root,path).Replace('\\','/');
                    if(extensions.Contains(Path.GetExtension(path)) && !rel.Equals("obra.tex",StringComparison.OrdinalIgnoreCase))files[rel]=shelf.AddAsset(path);
                }
                return files;
            });
            foreach(var (name,key) in added)work.TexResources[name]=key;
            Changed();Refresh();status.Text=$"{added.Count} recursos adjuntados";
        })));
        Refresh();controls.Children.Add(resources);controls.Children.Add(QuietAction("Quitar recurso seleccionado",()=>{if(resources.SelectedItem is string key){work.TexResources.Remove(key);Changed();Refresh();}}));
        panel.Children.Add(new Expander{Header="Proyecto LaTeX avanzado",Content=controls,HorizontalContentAlignment=HorizontalAlignment.Stretch});
    }
}
