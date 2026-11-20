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
    static string Paragraphs(string value)
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
            else if (line.StartsWith("- ")) output.AppendLine(@"\noindent\textbullet\enspace " + InlineParagraphs(line[2..]) + @"\par");
            else if (line.StartsWith("## ")) output.AppendLine(@"\par\medskip\noindent{\large\bfseries " + Escape(line[3..]) + @"}\par");
            else output.AppendLine(InlineParagraphs(line) + @"\par");
        }
        return output.ToString();
    }
    public static async Task Export(Work work, string media, string output, bool pdf, bool glossary = false, string? engine = null)
    {
        var temp = Directory.CreateTempSubdirectory("nimbo-").FullName;
        var pending = Path.GetFullPath(output) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var slide = work.Type == "Presentación" && !glossary;
            var novel = work.IsNovel && !glossary;
            var tex = new StringBuilder();
            var format=Format.All.FirstOrDefault(f=>f.Name==work.Type);
            var style=format==null?"Clásica":EditorialTemplate.For(format).FirstOrDefault(t=>t.Name==work.Template)?.Style??"Clásica";
            var paper=work.Publication.Paper switch {"A5"=>"a5paper","Carta"=>"letterpaper","A4"=>"a4paper",_=>novel?"a5paper":"a4paper"};
            tex.AppendLine(slide ? @"\documentclass[aspectratio=169]{beamer}" :
                novel ? @"\documentclass[11pt,a5paper,openany]{book}" : @"\documentclass[11pt]{article}");
            tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,tabularx}");
            if (!slide) tex.AppendLine(glossary ? @"\usepackage[a6paper,margin=12mm]{geometry}" :
                novel ? @"\usepackage["+paper+@",inner=18mm,outer=15mm]{geometry}" :
                @"\usepackage[" + paper + @",margin=23mm]{geometry}");
            var accent = work.Type switch { "CV" => "65455B", "Informe" => "315671", "Artículo" => "4E476E", "Carta" => "784F3E", "Acta" => "5C634F", "Apuntes" => "506C46", "Propuesta" => "474F76", _ => "365E64" };
            accent=work.PrintAccent switch {"Petróleo"=>"245966","Vino"=>"843B59","Oliva"=>"51683C","Grafito"=>"414650",_=>accent};
            if(!glossary && style=="Moderna")tex.AppendLine(@"\renewcommand{\familydefault}{\sfdefault}");
            tex.AppendLine(@"\definecolor{accent}{HTML}{" + accent + "}");
            if (slide) tex.AppendLine(@"\setbeamercolor{structure}{fg=accent}\setbeamercolor{frametitle}{bg=accent,fg=white}\setbeamertemplate{navigation symbols}{}");
            if (novel) tex.AppendLine(@"\renewcommand{\chaptername}{Capítulo}\renewcommand{\partname}{Parte}\renewcommand{\contentsname}{Contenido}");
            if (!slide && work.Chapters.Any(c => c.Layout.StartsWith("Ajustar") && c.Images.Count > 0)) tex.AppendLine(@"\usepackage{wrapfig}");
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
            void Image(string key, string layout = "Centrada")
            {
                if (string.IsNullOrEmpty(key)) return;
                if (Path.GetFileName(key) != key || !Regex.IsMatch(key, @"\A[0-9a-f]{64}\.(?:png|jpe?g)\z")) throw new InvalidDataException("Imagen no migrada: " + key);
                Directory.CreateDirectory(Path.Combine(temp, "media"));
                File.Copy(Path.Combine(media, key), Path.Combine(temp, "media", key), true);
                if (layout.StartsWith("Ajustar") && !slide)
                {
                    var side = layout.EndsWith("izquierda") ? "l" : "r";
                    tex.AppendLine(@"\begin{wrapfigure}{" + side + @"}{.36\linewidth}\centering\includegraphics[width=.33\linewidth,height=.35\textheight,keepaspectratio]{media/" + key + @"}\end{wrapfigure}");
                }
                else if (layout == "Página completa" && !slide)
                    tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}\begin{center}\includegraphics[width=\linewidth,height=.88\textheight,keepaspectratio]{media/" + key + @"}\end{center}\vspace*{\fill}\clearpage");
                else tex.AppendLine(@"\begin{center}\includegraphics[width=.84\linewidth,height=.47\textheight,keepaspectratio]{media/" + key + @"}\end{center}");
            }
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
                foreach (var node in work.Glossary)
                {
                    tex.AppendLine(@"\clearpage\section*{" + Escape(node.Name) + "}"); Image(node.ImagePath);
                    foreach (var field in new[] { ("Clase", node.NodeKind), ("Alias", node.Aliases), ("Edad / época", node.Age), ("Medidas", node.Measures), ("Función narrativa", node.Role), ("Tipo", node.Type), ("Etiquetas", node.Tags), ("Trasfondo", node.Background), ("Notas", node.Notes), ("Relaciones", node.Relations) }
                        .Concat(WorldModel.Effective(work, node).Select(f => (f.Definition.Name, f.Value))).Where(f => f.Item2 != ""))
                        tex.AppendLine(@"\paragraph{" + Escape(field.Item1) + "} " + Paragraphs(field.Item2));
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
                    var markers = Illustrations.Marker().Matches(chapter.PlainText)
                        .Where(m => int.TryParse(m.Groups[1].Value, out var n) && n <= chapter.Images.Count && !slide).ToList();
                    var used = new HashSet<int>();
                    int start = 0;
                    if (markers.Count == 0 && chapter.Layout.StartsWith("Ajustar") && !slide && chapter.Images.Count > 0)
                    {
                        Image(chapter.Images[0], chapter.Layout); used.Add(0);
                    }
                    foreach (var marker in markers)
                    {
                        tex.AppendLine(Paragraphs(chapter.PlainText[start..marker.Index]));
                        var index = int.Parse(marker.Groups[1].Value) - 1; Image(chapter.Images[index], chapter.Layout);
                        used.Add(index); start = marker.Index + marker.Length;
                    }
                    tex.AppendLine(Paragraphs(chapter.PlainText[start..]));
                    if (columns) tex.AppendLine(@"\end{column}\begin{column}{.50\textwidth}");
                    for (int i = 0; i < chapter.Images.Count; i++) if (!used.Contains(i)) Image(chapter.Images[i], chapter.Layout);
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
                for (int i = 0; i < 2; i++)
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
                }
                File.Copy(Path.Combine(temp, "obra.pdf"), pending);
            }
            else ZipFile.CreateFromDirectory(temp, pending);
            File.Move(pending, output, true);
        }
        finally { File.Delete(pending); Directory.Delete(temp, true); }
    }
}











