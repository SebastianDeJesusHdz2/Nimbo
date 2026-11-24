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
    static string RenderPalette(string spec)
    {
        var items = spec.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0) return "";
        double width = Math.Round(0.92 / Math.Max(1, items.Length), 3, MidpointRounding.ToZero);
        var sb = new StringBuilder(@"\par\smallskip\noindent");
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var match = Regex.Match(item, @"^#?([0-9A-Fa-f]{6})(?:\s+(.*))?$");
            var hex = match.Success ? match.Groups[1].Value.ToUpperInvariant() : "666666";
            var label = match.Success ? match.Groups[2].Value.Trim() : item;
            if (i > 0) sb.Append(@"\hfill");
            sb.Append(@"\begin{minipage}[t]{" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + @"\linewidth}\centering");
            sb.Append(@"\definecolor{nimboswatch}{HTML}{" + hex + @"}\colorbox{nimboswatch}{\rule{0pt}{7.5mm}\rule{.80\linewidth}{0pt}}\par");
            sb.Append(@"{\scriptsize\texttt{\#" + hex + @"}");
            if (label != "") sb.Append(@"\par " + Escape(label));
            sb.Append(@"}\end{minipage}");
        }
        sb.AppendLine(@"\par\smallskip");
        return sb.ToString();
    }
    static string RenderSignatures(string spec)
    {
        var signers = spec.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (signers.Length == 0) return "";
        double width = signers.Length == 1 ? 0.55 : signers.Length == 2 ? 0.46 : 0.31;
        var sb = new StringBuilder(@"\par\vspace{9mm}\noindent");
        if (signers.Length == 1) sb.Append(@"\begin{center}");
        for (int i = 0; i < signers.Length; i++)
        {
            if (i > 0) sb.Append(@"\hfill");
            sb.Append(@"\begin{minipage}[t]{" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + @"\linewidth}\centering");
            sb.Append(@"\rule{.84\linewidth}{.45pt}\\[3pt]{\small\bfseries " + Escape(signers[i]) + @"}\end{minipage}");
        }
        if (signers.Length == 1) sb.Append(@"\end{center}");
        sb.AppendLine(@"\par\medskip");
        return sb.ToString();
    }
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
                    var cells = lines[i].Trim().Trim('|').Split('|');
                    if (!cells.All(c => Regex.IsMatch(c.Trim(), @"^:?-+:?$")))
                        rows.Add(cells);
                    i++;
                } while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--;
                if (rows.Count == 0) continue;
                int columns = rows.Max(r => r.Length);
                output.AppendLine(@"\par\smallskip\noindent{\renewcommand{\arraystretch}{1.18}\begin{tabularx}{\linewidth}{|" + string.Join("|", Enumerable.Repeat("X", columns)) + @"|}\hline");
                for (int r = 0; r < rows.Count; r++)
                {
                    var row = rows[r];
                    bool header = r == 0 && rows.Count > 1;
                    if (header) output.Append(@"\rowcolor{accent!14}");
                    output.AppendLine(string.Join(" & ", Enumerable.Range(0, columns).Select(c =>
                    {
                        var cell = c < row.Length ? InlineParagraphs(row[c].Trim()) : "";
                        return header && !cell.Contains(@"\textbf{") ? @"\textbf{" + cell + "}" : cell;
                    })) + @"\\\hline");
                }
                output.AppendLine(@"\end{tabularx}}\par\smallskip");
            }
            else if(pageBreaks && line=="[[Página]]") output.AppendLine(@"\clearpage");
            else if(pageBreaks && line=="[[Índice]]") output.AppendLine(@"\tableofcontents\clearpage");
            else if(line.StartsWith("[[Paleta:") && line.EndsWith("]]")) output.Append(RenderPalette(line[9..^2]));
            else if(line.StartsWith("[[Firmas:") && line.EndsWith("]]")) output.Append(RenderSignatures(line[9..^2]));
            else if (line.StartsWith("- ")) output.AppendLine(@"\noindent\textcolor{accent}{\textbullet}\enspace " + InlineParagraphs(line[2..]) + @"\par");
            else if (Regex.Match(line, @"^([1-9][0-9]*)\.\s+(.*)$") is { Success: true } num)
                output.AppendLine(@"\noindent{\bfseries\color{accent}" + num.Groups[1].Value + @".}\enspace " + InlineParagraphs(num.Groups[2].Value) + @"\par");
            else if (line.StartsWith("> ")){
                output.AppendLine(@"\begin{quote}\itshape");
                do{output.AppendLine(InlineParagraphs(lines[i][2..])+@"\par");i++;}
                while(i<lines.Length && lines[i].StartsWith("> "));
                i--;output.AppendLine(@"\end{quote}");
            }
            else if (line.StartsWith("### ")) output.AppendLine(@"\par\smallskip\noindent{\normalsize\bfseries\color{accent} " + Escape(line[4..]) + @"}\par");
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
            if(!glossary && work.UseTexSource){
                if(string.IsNullOrWhiteSpace(work.TexSource))throw new InvalidDataException("El proyecto LaTeX está vacío.");
                foreach(var (relative,key) in work.TexResources){
                    var path=Path.GetFullPath(relative,temp);
                    if(Path.IsPathRooted(relative)||!path.StartsWith(temp+Path.DirectorySeparatorChar,(OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))||Path.GetFileName(key)!=key||!Regex.IsMatch(key,@"^[0-9a-f]{64}\.[a-z0-9]+$")||Path.GetFileName(path).Equals("obra.tex",StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Ruta de recurso no válida: "+relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.Copy(Path.Combine(media,key),path);
                }
                tex.Append(work.TexSource);
            }else{
            tex.AppendLine(slide ? @"\documentclass[aspectratio=169]{beamer}" :
                novel ? @"\documentclass[11pt,a5paper,openany]{book}" : glossary ? @"\documentclass[11pt,twoside]{article}" : @"\documentclass[11pt]{article}");
            tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,colortbl,tabularx}");
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
            if (!slide && !glossary && work.Chapters.Any(c => Enumerable.Range(0,c.Images.Count).Any(i=>Illustrations.Layout(c,i).StartsWith("Ajustar"))))
                tex.AppendLine(@"\usepackage{wrapfig}\newsavebox{\nimbofigure}");
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
                    // Measure the actual illustration and its caption before wrapping on a small page.
                    tex.AppendLine(@"\par\sbox{\nimbofigure}{\begin{minipage}{.36\linewidth}\centering\includegraphics[width=\linewidth,height=.35\textheight,keepaspectratio]{media/" + key + "}" + caption + @"\end{minipage}}");
                    tex.AppendLine(@"\ifdim\dimexpr\pagegoal-\pagetotal\relax<\dimexpr\ht\nimbofigure+\dp\nimbofigure+2\intextsep+\baselineskip\relax\newpage\fi");
                    tex.AppendLine(@"\begin{wrapfigure}{" + side + @"}{.36\linewidth}\usebox{\nimbofigure}\end{wrapfigure}");
                }
                else if(layout == "Lámina ilustrada" && !slide)
                    tex.AppendLine(@"\vspace*{\fill}\noindent\includegraphics[width=\linewidth,height=.52\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\par\medskip");
                else if (layout == "Página completa" && !slide)
                    tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}\begin{center}\includegraphics[width=\linewidth,height="+(caption==""?".88":".76")+@"\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}\vspace*{\fill}\clearpage");
                else tex.AppendLine(@"\begin{center}\includegraphics[width=.84\linewidth,height=.47\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}");
            }
            if(novel && work.Publication.RunningHead=="Sin encabezado")tex.AppendLine(@"\pagestyle{plain}");
            void Cover(string picture,bool back){
                tex.AppendLine(@"\clearpage\thispagestyle{empty}");
                if(picture.Length>0){
                    CopyImage(picture);
                    var file="media/"+picture;
                    tex.AppendLine(@"\AddToHookNext{shipout/background}{\put(0,-\paperheight){\vbox to \paperheight{\vss\hbox to \paperwidth{\hss\sbox0{\includegraphics[width=\paperwidth]{"+file+@"}}\ifdim\ht0<\paperheight\includegraphics[height=\paperheight]{"+file+@"}\else\usebox0\fi\hss}\vss}}}");
                }
                bool centered=work.CoverStyle=="Centrada";
                if(centered || work.CoverStyle!="Cabecera")tex.AppendLine(@"\null\vfill");
                tex.AppendLine(@"\noindent\colorbox{accent!75!black}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\color{white}\raggedright"+(centered?@"\centering":"")+@"\vspace{8pt}");
                if(back)tex.AppendLine(Paragraphs(work.Description.Length>900?"":work.Description));
                else{
                    tex.AppendLine(@"{\Huge\bfseries "+Escape(work.Title)+@"\par}");
                    if(work.ShowCoverSubtitle && work.Subtitle!="")tex.AppendLine(@"\medskip{\large "+Escape(work.Subtitle)+@"\par}");
                    if(work.Author!="")tex.AppendLine(@"\bigskip "+Escape(work.Author)+@"\par");
                }
                tex.AppendLine(@"\vspace{8pt}}}\par");
                if(back && work.Description.Length>900)tex.AppendLine(@"\medskip\noindent "+Paragraphs(work.Description));
                if(centered || work.CoverStyle=="Cabecera")tex.AppendLine(@"\vfill\null");
                tex.AppendLine(@"\clearpage");
            }
            if(!slide && !glossary && work.FrameStyle is "Filete" or "Doble filete" or "Esquinas"){
                tex.AppendLine(@"\newcommand{\nimboframe}[1]{\put(#1,-\dimexpr\paperheight-#1\relax){\framebox(\dimexpr\paperwidth-#1-#1\relax,\dimexpr\paperheight-#1-#1\relax){}}}");
                string lines=work.FrameStyle=="Esquinas"?@"\put(6mm,-6mm){\line(1,0){18mm}}\put(6mm,-6mm){\line(0,-1){18mm}}\put(\dimexpr\paperwidth-6mm\relax,-\dimexpr\paperheight-6mm\relax){\line(-1,0){18mm}}\put(\dimexpr\paperwidth-6mm\relax,-\dimexpr\paperheight-6mm\relax){\line(0,1){18mm}}":@"\nimboframe{5mm}"+(work.FrameStyle=="Doble filete"?@"\nimboframe{6mm}":"");
                tex.AppendLine(@"\AddToHook{shipout/foreground}{\color{accent}\linethickness{.45pt}"+lines+"}");
            }
            bool designedCover=!slide && !glossary && (novel || work.CoverStyle!="Por formato");
            if(designedCover)Cover(work.CoverImage,false);
            if(!glossary && work.Type=="CV"){
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
            else if (!designedCover && !glossary && work.Type == "Carta")
            {
                var placeDate = work.DocumentFacts.GetValueOrDefault("Lugar y fecha", "").Trim();
                if (placeDate != "") tex.AppendLine(@"\begin{flushright}{\small " + Escape(placeDate) + @"}\end{flushright}\smallskip");
                var subject = work.DocumentFacts.GetValueOrDefault("Asunto", "").Trim();
                if (subject == "") subject = work.Title;
                tex.AppendLine(@"\noindent{\bfseries\color{accent}ASUNTO: }{\bfseries " + Escape(subject) + @"}\par\medskip");
                var recipient = work.DocumentFacts.GetValueOrDefault("Destinatario", "").Trim();
                var address = work.DocumentFacts.GetValueOrDefault("Dirección", "").Trim();
                if (recipient != "" || address != "")
                {
                    tex.AppendLine(@"\noindent Dirigido a:\\");
                    if (recipient != "") tex.AppendLine(@"{\bfseries " + Escape(recipient) + @"}\\");
                    if (address != "") tex.AppendLine(Escape(address) + @"\\");
                    tex.AppendLine(@"{\bfseries PRESENTE.}\par\medskip");
                }
                tex.AppendLine(@"\noindent{\color{accent}\rule{\linewidth}{.8pt}}\medskip");
            }
            else if (!designedCover && !glossary && work.Type is "Acta" or "Apuntes")
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
            else if(!designedCover){
                if(slide && work.CoverImage!=""){
                    CopyImage(work.CoverImage);
                    tex.AppendLine(@"\begin{frame}[plain]\vspace*{2mm}\begin{center}\includegraphics[width=.72\linewidth,height=.42\textheight,keepaspectratio]{media/"+work.CoverImage+@"}\end{center}");
                    tex.AppendLine(@"\begin{center}{\Large\bfseries\color{accent}"+Escape(work.Title)+@"\par}");
                    if(work.Subtitle!="")tex.AppendLine(@"{\normalsize "+Escape(work.Subtitle)+@"\par}");
                    if(work.Author!="")tex.AppendLine(@"\smallskip{\small "+Escape(work.Author)+@"\par}");
                    tex.AppendLine(@"\end{center}\end{frame}");
                }else tex.AppendLine(slide ? @"\begin{frame}\titlepage\end{frame}" : @"\maketitle");
            }
            var facts=EditorialTemplate.FrontMatter(work.Type).Where(k=>!string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k))).ToArray();
            if(!slide && !glossary && work.Type!="Carta" && facts.Length>0){
                tex.AppendLine(@"\par\smallskip\noindent{\renewcommand{\arraystretch}{1.2}\begin{tabularx}{\linewidth}{|>{\columncolor{accent!12}\bfseries}p{4.2cm}|X|}\hline");
                foreach(var key in facts)tex.AppendLine(Escape(key)+@" & "+Escape(work.DocumentFacts[key])+@"\\\hline");
                tex.AppendLine(@"\end{tabularx}}\par\medskip");
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
                    if (work.Type == "Apuntes" && chapter.Detail != "")
                        tex.AppendLine(@"\par\smallskip\noindent\colorbox{accent!10}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\small{\bfseries\color{accent}Conceptos y preguntas}\par " + Paragraphs(chapter.Detail) + @"}}\par\smallskip");
                    else if (chapter.Detail != "")
                        tex.AppendLine(@"\par\smallskip\noindent{\small\bfseries\color{accent}" + Escape(EditorialTemplate.DetailLabel(work.Type)) + @": }\emph{" + Escape(chapter.Detail) + @"}\par\smallskip");
                    bool columns = slide && chapter.Layout is "Texto e imagen" or "Dos columnas" or "Plano y leyenda" or "Moodboard y paleta" or "Moodboard y landmark";
                    if (columns) tex.AppendLine(@"\begin{columns}[T,onlytextwidth]\begin{column}{.48\textwidth}");
                    var rows = slide ? [] : Illustrations.Rows(chapter).ToList();
                    var used = new HashSet<int>();
                    int start = 0;
                    if (rows.Count == 0 && !slide && chapter.Images.Count > 0 && Illustrations.Layout(chapter,0).StartsWith("Ajustar"))
                    {
                        Image(chapter.Images[0], Illustrations.Layout(chapter,0),chapter.ImageCaptions.GetValueOrDefault(0)); used.Add(0);
                    }
                    foreach (var row in rows)
                    {
                        tex.AppendLine(Paragraphs(chapter.PlainText[start..row[0].Start],!slide));
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
                    tex.AppendLine(Paragraphs(chapter.PlainText[start..],!slide));
                    if (columns) tex.AppendLine(@"\end{column}\begin{column}{.48\textwidth}");
                    if (slide && chapter.Images.Count > 1 && chapter.Layout is "Moodboard y paleta" or "Moodboard y landmark")
                    {
                        int perRow = chapter.Images.Count >= 3 ? 3 : 2;
                        string cellW = perRow == 3 ? ".31" : ".47";
                        string cellH = chapter.Images.Count > 3 ? ".20" : ".30";
                        tex.AppendLine(@"\begin{center}");
                        for (int i = 0; i < chapter.Images.Count; i++)
                        {
                            used.Add(i);
                            CopyImage(chapter.Images[i]);
                            if (i > 0 && i % perRow == 0) tex.AppendLine(@"\par\smallskip");
                            else if (i > 0) tex.AppendLine(@"\hfill%");
                            tex.AppendLine(@"\begin{minipage}[t]{" + cellW + @"\linewidth}\centering\vspace{0pt}\includegraphics[width=\linewidth,height=" + cellH + @"\textheight,keepaspectratio]{media/" + chapter.Images[i] + "}" + Caption(chapter.ImageCaptions.GetValueOrDefault(i)) + @"\end{minipage}%");
                        }
                        tex.AppendLine(@"\end{center}");
                    }
                    else if (slide && chapter.Layout == "Imagen protagonista" && chapter.Images.Count > 0)
                    {
                        used.Add(0); CopyImage(chapter.Images[0]);
                        tex.AppendLine(@"\begin{center}\includegraphics[width=.92\linewidth,height=.64\textheight,keepaspectratio]{media/" + chapter.Images[0] + "}" + Caption(chapter.ImageCaptions.GetValueOrDefault(0)) + @"\end{center}");
                    }
                    for (int i = 0; i < chapter.Images.Count; i++) if (!used.Contains(i)) Image(chapter.Images[i], Illustrations.Layout(chapter,i),chapter.ImageCaptions.GetValueOrDefault(i));
                    if(work.Type=="CV"){
                        foreach(var entry in chapter.Entries){
                            tex.AppendLine(@"\par\medskip\noindent{\bfseries "+Escape(entry.Heading)+@"}\hfill{\small "+Escape(entry.Period)+@"}\par\nopagebreak");
                            if(entry.Organization!="")tex.AppendLine(@"{\itshape "+Escape(entry.Organization)+@"\par}\nopagebreak");
                            tex.AppendLine(Paragraphs(entry.Description));
                        }
                    }
                    if (chapter.Summary != "")
                    {
                        if (work.Type == "Apuntes")
                            tex.AppendLine(@"\par\medskip\noindent\fbox{\parbox{\dimexpr\linewidth-2\fboxsep-2\fboxrule\relax}{\small{\bfseries\color{accent}Síntesis}\par " + Paragraphs(chapter.Summary) + @"}}\par");
                        else if (slide && columns)
                            tex.AppendLine(@"\par\smallskip{\small " + Paragraphs(chapter.Summary) + @"}");
                        else
                            tex.AppendLine(@"\par\medskip\textit{" + Escape(chapter.Summary) + "}");
                    }
                    if (columns) tex.AppendLine(@"\end{column}\end{columns}");
                    if (slide) tex.AppendLine(@"\end{frame}");
                }
            }
            if(novel && work.Publication.BackCover && (work.Description!="" || work.BackImage!=""))Cover(work.BackImage,true);
            tex.AppendLine(@"\end{document}");
            }
            File.WriteAllText(Path.Combine(temp, "obra.tex"), tex.ToString());
            if (pdf)
            {
                if(!glossary && work.UseTexSource){
                    var program=work.TexEngine=="Automático"?Regex.Match(work.TexSource,@"(?im)^%\s*!\s*TeX\s+program\s*=\s*(pdflatex|lualatex|xelatex)\b").Groups[1].Value.ToLowerInvariant():work.TexEngine;
                    if(program is "pdflatex" or "lualatex")
                        engine=engine!=null && Path.IsPathRooted(engine)?Path.Combine(Path.GetDirectoryName(engine)!,program+Path.GetExtension(engine)):program;
                }
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
                    if (process.ExitCode != 0) throw new IOException(Path.GetFileNameWithoutExtension(engine??"xelatex")+": " + log[^Math.Min(900, log.Length)..]);
                    var references = File.Exists(referencesFile)?File.ReadAllText(referencesFile):"";
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











