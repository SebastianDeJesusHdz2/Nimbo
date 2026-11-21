using Nimbo.Core;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Nimbo.Publishing;

public static class Publisher
{
    static string Escape(string value) => string.Concat(value.Select(c => c switch
    {
        '\\' => @"\textbackslash{}",
        '{' or '}' or '&' or '%' or '$' or '#' or '_' => "\\" + c,
        '^' => @"\textasciicircum{}",
        '~' => @"\textasciitilde{}",
        _ => c.ToString()
    }));
    static string InlineParagraphs(string value) => string.Join("\n\n", value.ReplaceLineEndings("\n").Split('\n').Select(line =>
        string.Concat(Regex.Split(line, @"(\*\*[^*]+\*\*|\*[^*]+\*)").Select(part =>
            part.Length > 4 && part.StartsWith("**") && part.EndsWith("**") ? @"\textbf{" + Escape(part[2..^2]) + "}" :
            part.Length > 2 && part.StartsWith('*') && part.EndsWith('*') ? @"\emph{" + Escape(part[1..^1]) + "}" : Escape(part)))));
    static string Paragraphs(string value,bool pageBreaks=false)
    {
        var output = new StringBuilder();
        var lines = value.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
            {
                var rows = new List<string[]>();
                do
                {
                    rows.Add(lines[i].Trim().Trim('|').Split('|'));
                    i++;
                } while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--; int columns = rows.Max(r => r.Length);
                output.AppendLine(@"\noindent\begin{tabularx}{\linewidth}{" + string.Concat(Enumerable.Repeat("X", columns)) + "}");
                foreach (var row in rows) output.AppendLine(string.Join(" & ", Enumerable.Range(0, columns).Select(c => c < row.Length ? InlineParagraphs(row[c].Trim()) : "")) + @"\\");
                output.AppendLine(@"\end{tabularx}\par");
            }
            else if(pageBreaks && line=="[[Página]]") output.AppendLine(@"\clearpage");
            else if (line.StartsWith("- ")) output.AppendLine(@"\noindent\textbullet\enspace " + InlineParagraphs(line[2..]) + @"\par");
            else if (line.StartsWith("> ")){
                output.AppendLine(@"\begin{quote}\itshape");
                do{output.AppendLine(InlineParagraphs(lines[i][2..])+@"\par");i++;}
                while(i<lines.Length && lines[i].StartsWith("> "));
                i--;output.AppendLine(@"\end{quote}");
            }
            else if (line.StartsWith("## ")) output.AppendLine(@"\par\medskip\noindent{\large\bfseries " + Escape(line[3..]) + @"}\par");
            else output.AppendLine(InlineParagraphs(line) + @"\par");
        }
        return output.ToString();
    }
    public static async Task Export(Work work, string media, string output, bool pdf, bool glossary = false, string? engine = null,string? nodeId=null,IReadOnlySet<string>? selection=null)
    {
        var temp = Directory.CreateTempSubdirectory("nimbo-").FullName;
        var pending = Path.GetFullPath(output) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var slide = work.Type == "Presentación" && !glossary;
            var novel = work.IsNovel && !glossary;
            if(nodeId!=null && (!glossary || !work.Glossary.Any(n=>n.Id==nodeId)))throw new ArgumentException("Ficha de glosario desconocida.");
            if(selection!=null && (!glossary || nodeId!=null || selection.Count==0 || !selection.IsSubsetOf(work.Glossary.Select(n=>n.Id))))throw new ArgumentException("Selecciona fichas existentes de esta obra.");
            var tex = new StringBuilder();
            var format=Format.All.FirstOrDefault(f=>f.Name==work.Type);
            var style=format==null?"Clásica":EditorialTemplate.For(format).FirstOrDefault(t=>t.Name==work.Template)?.Style??"Clásica";
            var paper=work.Publication.Paper switch {"Bolsillo" when novel=>"paperwidth=110mm,paperheight=175mm","A5"=>"a5paper","Carta"=>"letterpaper","A4"=>"a4paper",_=>novel?"a5paper":"a4paper"};
            tex.AppendLine(slide ? @"\documentclass[aspectratio=169]{beamer}" :
                novel ? @"\documentclass[11pt,a5paper,openany]{book}" : glossary ? @"\documentclass[11pt,twoside]{article}" : @"\documentclass[11pt]{article}");
            tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,tabularx}");
            var (leafWidth,leafHeight)=Dossiers.Size(work.GlossaryPaper);
            if (!slide) tex.AppendLine(glossary ? $@"\usepackage[paperwidth={leafWidth}mm,paperheight={leafHeight}mm,inner=12mm,outer=10mm,top=12mm,bottom=15mm,footskip=8mm]{{geometry}}" :
                novel ? @"\usepackage["+paper+@",inner=18mm,outer=15mm]{geometry}" :
                @"\usepackage[" + paper + @",margin=23mm]{geometry}");
            var accent = work.Type switch { "CV" => "65455B", "Informe" => "315671", "Artículo" => "4E476E", "Carta" => "784F3E", "Acta" => "5C634F", "Apuntes" => "506C46", "Propuesta" => "474F76", _ => "365E64" };
            accent=work.PrintAccent switch {"Petróleo"=>"245966","Vino"=>"843B59","Oliva"=>"51683C","Grafito"=>"414650",_=>accent};
            if(!glossary && style=="Moderna")tex.AppendLine(@"\renewcommand{\familydefault}{\sfdefault}");
            tex.AppendLine(@"\definecolor{accent}{HTML}{" + accent + "}");
            if(glossary){
                using var template=typeof(Publisher).Assembly.GetManifestResourceStream("Nimbo.Publishing.fichas.tex")??throw new IOException("Falta la plantilla del cuadernillo.");
                using var copy=File.Create(Path.Combine(temp,"fichas.tex"));template.CopyTo(copy);
                tex.AppendLine(@"\input{fichas.tex}");
            }
            if (slide) tex.AppendLine(@"\setbeamercolor{structure}{fg=accent}\setbeamercolor{frametitle}{bg=accent,fg=white}\setbeamertemplate{navigation symbols}{}");
            if (novel) tex.AppendLine(@"\renewcommand{\chaptername}{Capítulo}\renewcommand{\partname}{Parte}\renewcommand{\contentsname}{Contenido}");
            if (!slide && work.Chapters.Any(c => Enumerable.Range(0,c.Images.Count).Any(i=>Illustrations.Layout(c,i).StartsWith("Ajustar")))) tex.AppendLine(@"\usepackage{wrapfig}");
            if(!glossary && work.Type=="CV")tex.AppendLine(@"\setlength{\parindent}{0pt}");
            var title = glossary ? "Glosario de " + work.Title : work.Title;
            if(slide){
                var affiliation=string.Join(" · ",new[]{"Institución","Evento"}.Select(k=>work.DocumentFacts.GetValueOrDefault(k,"")).Where(v=>v!=""));
                tex.AppendLine(@"\institute{"+Escape(affiliation)+@"}\subtitle{"+Escape(work.Subtitle)+"}");
            }
            var date=slide?work.DocumentFacts.GetValueOrDefault("Fecha",""):"";
            tex.AppendLine(@"\title{" + Escape(title) + @"}\author{" + Escape(work.Author) + @"}\date{"+Escape(date)+@"}\begin{document}");
            if(!slide && !glossary){
                var leading=work.PrintSpacing switch {"Abierto"=>"1.2","Amplio"=>"1.5",_=>"1"};
                tex.AppendLine(@"\linespread{"+leading+@"}\selectfont");
                var indent=work.PrintIndent switch {"Sin sangría"=>"0mm","5 mm"=>"5mm","8 mm"=>"8mm",_=>null};
                if(indent!=null)tex.AppendLine(@"\setlength{\parindent}{"+indent+"}");
            }
            void CopyImage(string key)
            {
                if (Path.GetFileName(key) != key || !Regex.IsMatch(key, @"\A[0-9a-f]{64}\.(?:png|jpe?g)\z")) throw new InvalidDataException("Imagen no migrada: " + key);
                Directory.CreateDirectory(Path.Combine(temp, "media"));
                var dest=Path.Combine(temp,"media",key);
                if(!File.Exists(dest))File.Copy(Path.Combine(media,key),dest);
            }
            string Caption(string? text)=>string.IsNullOrWhiteSpace(text)?"":@"\par\smallskip{\small\itshape "+Escape(text)+@"\par}";
            void Image(string key, string layout = "Centrada",string? legend=null)
            {
                if(string.IsNullOrEmpty(key))return;
                CopyImage(key);var caption=Caption(legend);
                if (layout.StartsWith("Ajustar") && !slide)
                {
                    var side = layout.EndsWith("izquierda") ? "l" : "r";
                    tex.AppendLine(@"\begin{wrapfigure}{" + side + @"}{.36\linewidth}\centering\includegraphics[width=\linewidth,height=.35\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{wrapfigure}");
                }
                else if(layout == "Lámina ilustrada" && !slide)
                    tex.AppendLine(@"\vspace*{\fill}\noindent\includegraphics[width=\linewidth,height=.52\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\par\medskip");
                else if (layout == "Página completa" && !slide)
                    tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}\begin{center}\includegraphics[width=\linewidth,height="+(caption==""?".88":".76")+@"\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}\vspace*{\fill}\clearpage");
                else tex.AppendLine(@"\begin{center}\includegraphics[width=.84\linewidth,height=.47\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}");
            }
            if(novel && work.Publication.RunningHead=="Sin encabezado")tex.AppendLine(@"\pagestyle{plain}");
            if (novel && work.CoverImage != "")
            {
                tex.AppendLine(@"\begin{titlepage}\centering"); Image(work.CoverImage);
                tex.AppendLine(@"{\Huge " + Escape(work.Title) + @"\par}\vfill " + Escape(work.Author) + @"\end{titlepage}");
            }
            else if(!glossary && work.Type=="CV"){
                string Contact(string key)=>work.Contact.GetValueOrDefault(key,"").Trim();
                var name=Contact("Nombre profesional");if(name=="")name=work.Title;
                var profession=Contact("Profesión");if(profession=="")profession=work.Subtitle;
                if(style=="Académica")tex.AppendLine(@"\begin{center}");
                if(style=="Moderna")tex.AppendLine(@"\noindent\colorbox{accent}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\color{white}\Large\bfseries\strut "+Escape(name)+@"\strut}}\par");
                else tex.AppendLine(@"\noindent{\Huge\color{accent}"+Escape(name)+@"\par}");
                if(profession!="")tex.AppendLine(@"\medskip{\large "+Escape(profession)+@"\par}");
                if(Contact("Nombre profesional")=="" && work.Author!="" && work.Author!=name)tex.AppendLine(Escape(work.Author)+@"\par");
                var contact=new[]{"Correo","Teléfono","Ciudad","Portafolio","Perfil profesional en línea"}.Select(Contact).Where(v=>v!="");
                tex.AppendLine(@"\smallskip{\small "+(style=="Académica"?@"\centering ":@"\raggedright ")+string.Join(@" \enspace·\enspace ",contact.Select(Escape))+@"\par}");
                if(style=="Académica")tex.AppendLine(@"\end{center}");
                else tex.AppendLine(@"\noindent{\color{accent}\rule{\linewidth}{1pt}}\par");
            }
            else if (!glossary && work.Type is "Carta" or "Acta" or "Apuntes")
            {
                tex.AppendLine(@"\noindent{\sffamily\Huge\color{accent}" + Escape(work.Title) + @"\par}\medskip");
                if (work.Subtitle != "") tex.AppendLine(@"{\large " + Escape(work.Subtitle) + @"\par}");
                if (work.Author != "") tex.AppendLine(Escape(work.Author) + @"\par");
                tex.AppendLine(@"\noindent{\color{accent}\rule{\linewidth}{1pt}}\medskip");
            }
            else if(glossary){
                if(nodeId==null){
                    tex.AppendLine(@"\portadaglosario{"+Escape(work.Title)+"}{"+Escape(work.Author)+"}");
                    if(work.Glossary.Count>0)tex.AppendLine(@"\tableofcontents");
                }
            }
            else tex.AppendLine(slide ? @"\begin{frame}\titlepage\end{frame}" : @"\maketitle");
            var facts=EditorialTemplate.FrontMatter(work.Type).Where(k=>!string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k))).ToArray();
            if(!slide && !glossary && facts.Length>0){
                if(slide)tex.AppendLine(@"\begin{frame}{"+Escape(work.Subtitle==""?work.Title:work.Subtitle)+"}");
                foreach(var key in facts)tex.AppendLine(@"\noindent{\bfseries "+Escape(key)+@"}\enspace "+Escape(work.DocumentFacts[key])+@"\par");
                if(slide)tex.AppendLine(@"\end{frame}");
            }
            if (novel && work.Publication.Contents && work.Chapters.Count > 1) tex.AppendLine(@"\tableofcontents");
            if (glossary)
            {
                foreach (var node in work.Glossary.Where(n=>(nodeId==null || n.Id==nodeId) && (selection==null || selection.Contains(n.Id))).OrderBy(n=>n.Name,StringComparer.CurrentCultureIgnoreCase))
                {
                    tex.AppendLine(@"\abrirficha{"+Escape(node.NodeKind)+"}{"+Escape(node.Name)+"}");
                    if(node.ImagePath!=""){
                        CopyImage(node.ImagePath);
                        tex.AppendLine(@"\retratoficha{media/"+node.ImagePath+"}");
                    }
                    void Entry(string label,string value,bool compact=false){
                        if(string.IsNullOrWhiteSpace(value))return;
                        var command=compact && value.Length<=72 && !value.Contains('\n') && !value.Contains('\r')
                            ? @"\datoficha{" : @"\campoficha{";
                        tex.AppendLine(command+Escape(label)+"}{"+InlineParagraphs(value)+"}");
                    }
                    Entry("Función narrativa",node.Role);
                    foreach(var (label,value) in Dossiers.Details(work,node))Entry(label,value,true);
                    Entry("Trasfondo",node.Background);
                    foreach(var (label,value) in node.Sections)Entry(label,value);
                    Entry("Relaciones",node.Relations);
                    foreach(var (target,role) in Dossiers.Links(work,node))Entry(role,target.Name,true);
                    Entry("Notas de autor",node.Notes);
                }
            }
            else
            {
                static bool HasContent(Chapter c)=>c.PlainText!="" || c.Detail!="" || c.Summary!="" || c.Entries.Count>0 || c.Images.Count>0;
                var omitEmpty=work.Type=="CV" && work.Chapters.Any(HasContent);
                foreach (var chapter in WorldModel.Ordered(work))
                {
                    if(omitEmpty && !HasContent(chapter))continue;
                    var heading = Escape(chapter.Title);
                    tex.AppendLine(slide ? @"\begin{frame}[allowframebreaks]{" + heading + "}" : novel ?
                        (chapter.Level == "Arc" ? @"\part{" : chapter.Level == "Subchapter" ? @"\section{" : @"\chapter{") + heading + "}" :
                        work.Type is "Informe" or "Artículo" or "Propuesta" ? @"\section{" + heading + "}" : work.Type == "Carta" ? @"\medskip" : @"\section*{\color{accent}" + heading + "}");
                    if (work.Type == "Apuntes" && chapter.Detail != "") tex.AppendLine(@"\begin{quote}\small\textbf{Conceptos}\par " + Paragraphs(chapter.Detail) + @"\end{quote}");
                    else if (chapter.Detail != "") tex.AppendLine(@"\emph{" + Escape(chapter.Detail) + @"}\par");
                    bool columns = slide && chapter.Layout is "Texto e imagen" or "Dos columnas" or "Plano y leyenda" or "Moodboard y paleta" or "Moodboard y landmark";
                    if (columns) tex.AppendLine(@"\begin{columns}[T,onlytextwidth]\begin{column}{.46\textwidth}");
                    var rows = slide ? [] : Illustrations.Rows(chapter).ToList();
                    var used = new HashSet<int>();
                    int start = 0;
                    if (rows.Count == 0 && !slide && chapter.Images.Count > 0 && Illustrations.Layout(chapter,0).StartsWith("Ajustar"))
                    {
                        Image(chapter.Images[0], Illustrations.Layout(chapter,0),chapter.ImageCaptions.GetValueOrDefault(0)); used.Add(0);
                    }
                    foreach (var row in rows)
                    {
                        tex.AppendLine(Paragraphs(chapter.PlainText[start..row[0].Start],novel));
                        if(row.Count>1)tex.AppendLine(@"\begin{center}");
                        for(int col=0;col<row.Count;col++){
                            int index=row[col].Image;var key=chapter.Images[index];used.Add(index);
                            var caption=chapter.ImageCaptions.GetValueOrDefault(index);
                            if(row.Count==1){Image(key,Illustrations.Layout(chapter,index),caption);continue;}
                            CopyImage(key);
                            if(col>0)tex.AppendLine(@"\hfill%");
                            tex.AppendLine(@"\begin{minipage}[t]{"+(row.Count==2?".475":".31")+@"\linewidth}\centering\vspace{0pt}\includegraphics[width=\linewidth,height=.25\textheight,keepaspectratio]{media/"+key+"}"+Caption(caption)+@"\end{minipage}%");
                        }
                        if(row.Count>1)tex.AppendLine(@"\end{center}");
                        start=row[^1].End;
                    }
                    tex.AppendLine(Paragraphs(chapter.PlainText[start..],novel));
                    if (columns) tex.AppendLine(@"\end{column}\begin{column}{.50\textwidth}");
                    for (int i = 0; i < chapter.Images.Count; i++) if (!used.Contains(i)) Image(chapter.Images[i], Illustrations.Layout(chapter,i),chapter.ImageCaptions.GetValueOrDefault(i));
                    if(work.Type=="CV"){
                        foreach(var entry in chapter.Entries){
                            tex.AppendLine(@"\par\medskip\noindent{\bfseries "+Escape(entry.Heading)+@"}\hfill{\small "+Escape(entry.Period)+@"}\par\nopagebreak");
                            if(entry.Organization!="")tex.AppendLine(@"{\itshape "+Escape(entry.Organization)+@"\par}\nopagebreak");
                            tex.AppendLine(Paragraphs(entry.Description));
                        }
                    }
                    if (chapter.Summary != "") tex.AppendLine(@"\par\medskip\textit{" + Escape(chapter.Summary) + "}");
                    if (columns) tex.AppendLine(@"\end{column}\end{columns}");
                    if (slide) tex.AppendLine(@"\end{frame}");
                }
            }
            if (novel && work.Publication.BackCover && work.Description != "") tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}" + Paragraphs(work.Description) + @"\vspace*{\fill}");
            tex.AppendLine(@"\end{document}");
            File.WriteAllText(Path.Combine(temp, "obra.tex"), tex.ToString());
            if (pdf)
            {
                var referencesFile = Path.Combine(temp, "obra.aux");
                string? previousReferences = null;
                const int maximumPasses = 5;
                for (int pass = 1; pass <= maximumPasses; pass++)
                {
                    var info = new ProcessStartInfo(engine ?? "xelatex") { WorkingDirectory = temp, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    if (engine != null && Path.IsPathRooted(engine))
                    {
                        var bin = Path.GetDirectoryName(engine)!;
                        info.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
                        var fontconfig = Path.GetFullPath(Path.Combine(bin, "../../../../FontConfig/fonts.conf"));
                        if (File.Exists(fontconfig)) info.Environment["FONTCONFIG_FILE"] = fontconfig;
                        if (File.Exists(Path.Combine(bin, "miktex.exe"))) info.ArgumentList.Add("--disable-installer");
                    }
                    foreach (var arg in new[] { "-no-shell-escape", "-interaction=nonstopmode", "-halt-on-error", "obra.tex" }) info.ArgumentList.Add(arg);
                    using var process = Process.Start(info) ?? throw new IOException("No se pudo iniciar XeLaTeX.");
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(true); process.WaitForExit(); throw new TimeoutException("XeLaTeX superó tres minutos."); }
                    var log = await stdout + "\n" + await stderr;
                    if (process.ExitCode != 0) throw new IOException("XeLaTeX: " + log[^Math.Min(900, log.Length)..]);
                    var references = File.ReadAllText(referencesFile);
                    if (references == previousReferences) break;
                    if (pass == maximumPasses)
                        throw new IOException("La numeración del índice sigue cambiando. Revisa los saltos de página antes de imprimir.");
                    previousReferences = references;
                }
                File.Copy(Path.Combine(temp, "obra.pdf"), pending);
            }
            else ZipFile.CreateFromDirectory(temp, pending);
            File.Move(pending, output, true);
        }
        finally { File.Delete(pending); Directory.Delete(temp, true); }
    }
}











