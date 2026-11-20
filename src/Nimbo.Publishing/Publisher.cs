using Nimbo.Core;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Nimbo.Publishing;

public static class Publisher
{
    static string Escape(string value) => string.Concat(value.Select(c => c switch {
        '\\'=>@"\textbackslash{}", '{' or '}' or '&' or '%' or '$' or '#' or '_'=>"\\"+c,
        '^'=>@"\textasciicircum{}", '~'=>@"\textasciitilde{}", _=>c.ToString()}));
    static string InlineParagraphs(string value) => string.Join("\n\n",value.ReplaceLineEndings("\n").Split('\n').Select(line =>
        string.Concat(Regex.Split(line,@"(\*\*[^*]+\*\*|\*[^*]+\*)").Select(part =>
            part.Length>4 && part.StartsWith("**") && part.EndsWith("**") ? @"\textbf{"+Escape(part[2..^2])+"}" :
            part.Length>2 && part.StartsWith('*') && part.EndsWith('*') ? @"\emph{"+Escape(part[1..^1])+"}" : Escape(part)))));
    static string Paragraphs(string value)
    {
        var output=new StringBuilder();
        var lines=value.ReplaceLineEndings("\n").Split('\n');
        for(int i=0;i<lines.Length;i++){
            var line=lines[i].TrimEnd();
            if(line.StartsWith('|') && line.EndsWith('|') && line.Length>2){
                var rows=new List<string[]>();
                do {
                    rows.Add(lines[i].Trim().Trim('|').Split('|'));
                    i++;
                } while(i<lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--;int columns=rows.Max(r=>r.Length);
                output.AppendLine(@"\noindent\begin{tabularx}{\linewidth}{"+string.Concat(Enumerable.Repeat("X",columns))+"}");
                foreach(var row in rows)output.AppendLine(string.Join(" & ",Enumerable.Range(0,columns).Select(c=>c<row.Length?InlineParagraphs(row[c].Trim()):""))+@"\\");
                output.AppendLine(@"\end{tabularx}\par");
            }else if(line.StartsWith("- "))output.AppendLine(@"\noindent\textbullet\enspace "+InlineParagraphs(line[2..])+@"\par");
            else if(line.StartsWith("## "))output.AppendLine(@"\par\medskip\noindent{\large\bfseries "+Escape(line[3..])+@"}\par");
            else output.AppendLine(InlineParagraphs(line)+@"\par");
        }
        return output.ToString();
    }
    public static async Task Export(Work work,string media,string output,bool pdf,bool glossary=false,string? engine=null)
    {
        var temp=Directory.CreateTempSubdirectory("nimbo-").FullName;
        var pending=Path.GetFullPath(output)+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            var slide=work.Type=="Presentación" && !glossary;
            var novel=work.IsNovel && !glossary;
            var tex=new StringBuilder();
            tex.AppendLine(slide ? @"\documentclass[aspectratio=169]{beamer}" :
                novel ? @"\documentclass[11pt,a5paper,openany]{book}" : @"\documentclass[11pt]{article}");
            tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,tabularx}");
            if(!slide) tex.AppendLine(glossary ? @"\usepackage[a6paper,margin=12mm]{geometry}" :
                novel ? @"\usepackage[a5paper,inner=18mm,outer=15mm]{geometry}" :
                @"\usepackage["+(work.Publication.Paper=="Carta"?"letterpaper":"a4paper")+@",margin=23mm]{geometry}");
            tex.AppendLine(@"\definecolor{accent}{HTML}{365E64}");
            if(!slide && work.Chapters.Any(c=>c.Layout.StartsWith("Ajustar") && c.Images.Count>0)) tex.AppendLine(@"\usepackage{wrapfig}");
            var title=glossary ? "Glosario de "+work.Title : work.Title;
            tex.AppendLine(@"\title{"+Escape(title)+@"}\author{"+Escape(work.Author)+@"}\date{}\begin{document}");
            void Image(string key,string layout="Centrada") {
                if(string.IsNullOrEmpty(key)) return;
                if(Path.GetFileName(key)!=key || !Regex.IsMatch(key,@"\A[0-9a-f]{64}\.(?:png|jpe?g)\z")) throw new InvalidDataException("Imagen no migrada: "+key);
                Directory.CreateDirectory(Path.Combine(temp,"media"));
                File.Copy(Path.Combine(media,key),Path.Combine(temp,"media",key),true);
                if(layout.StartsWith("Ajustar") && !slide){
                    var side=layout.EndsWith("izquierda")?"l":"r";
                    tex.AppendLine(@"\begin{wrapfigure}{"+side+@"}{.36\linewidth}\centering\includegraphics[width=.33\linewidth,height=.35\textheight,keepaspectratio]{media/"+key+@"}\end{wrapfigure}");
                } else if(layout=="Página completa" && !slide)
                    tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}\begin{center}\includegraphics[width=\linewidth,height=.88\textheight,keepaspectratio]{media/"+key+@"}\end{center}\vspace*{\fill}\clearpage");
                else tex.AppendLine(@"\begin{center}\includegraphics[width=.84\linewidth,height=.47\textheight,keepaspectratio]{media/"+key+@"}\end{center}");
            }
            if(novel && work.CoverImage!=""){
                tex.AppendLine(@"\begin{titlepage}\centering");Image(work.CoverImage);
                tex.AppendLine(@"{\Huge "+Escape(work.Title)+@"\par}\vfill "+Escape(work.Author)+@"\end{titlepage}");
            } else tex.AppendLine(slide ? @"\begin{frame}\titlepage\end{frame}" : @"\maketitle");
            if(glossary) {
                foreach(var node in work.Glossary) {
                    tex.AppendLine(@"\clearpage\section*{"+Escape(node.Name)+"}"); Image(node.ImagePath);
                    foreach(var field in new[]{("Clase",node.NodeKind),("Alias",node.Aliases),("Trasfondo",node.Background),("Notas",node.Notes),("Relaciones",node.Relations)}
                        .Concat(WorldModel.Effective(work,node).Select(f=>(f.Definition.Name,f.Value))).Where(f=>f.Item2!=""))
                        tex.AppendLine(@"\paragraph{"+Escape(field.Item1)+"} "+Paragraphs(field.Item2));
                }
            } else {
                foreach(var chapter in WorldModel.Ordered(work)) {
                    var heading=Escape(chapter.Title);
                    tex.AppendLine(slide ? @"\begin{frame}[allowframebreaks]{"+heading+"}" : novel ?
                        (chapter.Level=="Arc" ? @"\part{" : chapter.Level=="Subchapter" ? @"\section{" : @"\chapter{")+heading+"}" :
                        @"\section*{\color{accent}"+heading+"}");
                    if(work.Type=="Apuntes" && chapter.Detail!="")tex.AppendLine(@"\begin{quote}\small\textbf{Conceptos}\par "+Paragraphs(chapter.Detail)+@"\end{quote}");
                    else if(chapter.Detail!="") tex.AppendLine(@"\emph{"+Escape(chapter.Detail)+@"}\par");
                    var wrap=chapter.Layout.StartsWith("Ajustar") && !slide && chapter.Images.Count>0;
                    if(wrap)Image(chapter.Images[0],chapter.Layout);
                    tex.AppendLine(Paragraphs(chapter.PlainText));
                    foreach(var key in chapter.Images.Skip(wrap?1:0)) Image(key,wrap?"Centrada":chapter.Layout);
                    if(chapter.Summary!="") tex.AppendLine(@"\par\medskip\textit{"+Escape(chapter.Summary)+"}");
                    if(slide) tex.AppendLine(@"\end{frame}");
                }
            }
            if(novel && work.Publication.BackCover && work.Description!="")tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}"+Paragraphs(work.Description)+@"\vspace*{\fill}");
            tex.AppendLine(@"\end{document}");
            File.WriteAllText(Path.Combine(temp,"obra.tex"),tex.ToString());
            if(pdf) {
                for(int i=0;i<2;i++) {
                    var info=new ProcessStartInfo(engine??"xelatex"){WorkingDirectory=temp,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                    if(engine!=null && Path.IsPathRooted(engine)){
                        var bin=Path.GetDirectoryName(engine)!;
                        info.Environment["PATH"]=bin+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH");
                        var fontconfig=Path.GetFullPath(Path.Combine(bin,"../../../../FontConfig/fonts.conf"));
                        if(File.Exists(fontconfig))info.Environment["FONTCONFIG_FILE"]=fontconfig;
                        if(File.Exists(Path.Combine(bin,"miktex.exe")))info.ArgumentList.Add("--disable-installer");
                    }
                    foreach(var arg in new[]{"-no-shell-escape","-interaction=nonstopmode","-halt-on-error","obra.tex"}) info.ArgumentList.Add(arg);
                    using var process=Process.Start(info)??throw new IOException("No se pudo iniciar XeLaTeX.");
                    var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
                    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    try {await process.WaitForExitAsync(timeout.Token);} catch(OperationCanceledException){process.Kill(true);process.WaitForExit();throw new TimeoutException("XeLaTeX superó tres minutos.");}
                    var log=await stdout+"\n"+await stderr;
                    if(process.ExitCode!=0) throw new IOException("XeLaTeX: "+log[^Math.Min(900,log.Length)..]);
                }
                File.Copy(Path.Combine(temp,"obra.pdf"),pending);
            } else ZipFile.CreateFromDirectory(temp,pending);
            File.Move(pending,output,true);
        } finally { File.Delete(pending);Directory.Delete(temp,true); }
    }
}



