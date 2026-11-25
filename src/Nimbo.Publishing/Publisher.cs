using Nimbo.Core;
using System.Diagnostics;
using System.Globalization;
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
            sb.Append(@"\begin{minipage}[t]{" + width.ToString(CultureInfo.InvariantCulture) + @"\linewidth}\centering");
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
            sb.Append(@"\begin{minipage}[t]{" + width.ToString(CultureInfo.InvariantCulture) + @"\linewidth}\centering");
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
                string tblFont = columns >= 5 ? @"\footnotesize" : columns >= 4 ? @"\small" : "";
                output.AppendLine(@"\par\smallskip\noindent{" + tblFont + @"\renewcommand{\arraystretch}{1.18}\setlength{\tabcolsep}{5pt}\begin{tabularx}{\linewidth}{|" + string.Join("|", Enumerable.Repeat("Y", columns)) + @"|}\hline");
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

    static readonly string[] ConceptOrder =
    [
        "Plataforma",
        "Target & ESRB",
        "Referencia visual 1",
        "Referencia visual 2",
        "Referencia visual 3",
        "Referencia visual 4",
        "Gameplay 1",
        "Gameplay 2",
        "Gameplay 3",
        "Fondo de diapositiva"
    ];

    static Dictionary<string, (string Key, string Caption, int Index)> MapSlideConceptImages(Chapter slide)
    {
        var map = new Dictionary<string, (string Key, string Caption, int Index)>();
        var unassigned = new List<int>();
        for (int i = 0; i < slide.Images.Count; i++)
        {
            var role = slide.ImageLayouts.GetValueOrDefault(i, "Automático");
            if (ConceptOrder.Contains(role) && !map.ContainsKey(role))
                map[role] = (slide.Images[i], slide.ImageCaptions.GetValueOrDefault(i, ""), i);
            else unassigned.Add(i);
        }
        int cursor = 0;
        foreach (var role in ConceptOrder)
        {
            if (!map.ContainsKey(role) && cursor < unassigned.Count)
            {
                int idx = unassigned[cursor++];
                map[role] = (slide.Images[idx], slide.ImageCaptions.GetValueOrDefault(idx, ""), idx);
            }
        }
        return map;
    }

    static (List<string> References, List<string> Keywords, List<string> ExtraLines) ParseSlideConceptLists(string text)
    {
        var refs = new List<string>();
        var keys = new List<string>();
        var extra = new List<string>();
        int bucket = 0;
        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line == "") continue;
            if (line.StartsWith("### ") || line.StartsWith("## "))
            {
                var head = line.TrimStart('#', ' ').ToLowerInvariant();
                if (head.Contains("key") || head.Contains("palabra")) bucket = 2;
                else if (head.Contains("referencia")) bucket = 1;
                continue;
            }
            string? item = null;
            if (line.StartsWith("- ")) item = line[2..].Trim();
            else if (Regex.Match(line, @"^[1-9][0-9]*\.\s+(.*)$") is { Success: true } m) item = m.Groups[1].Value.Trim();
            if (item != null)
            {
                if (bucket == 2 || (bucket == 0 && refs.Count >= 6)) keys.Add(item);
                else refs.Add(item);
            }
            else if (line.Contains('|') && !line.StartsWith('|'))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var p in parts)
                {
                    if (refs.Count < 6) refs.Add(p);
                    else keys.Add(p);
                }
            }
            else extra.Add(line);
        }
        return (refs, keys, extra);
    }

    static List<(string Title, string Subtitle, List<string> Lines)> ParseSlideCards(string plainText)
    {
        var cards = new List<(string Title, string Subtitle, List<string> Lines)>();
        var lines = plainText.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || (line.StartsWith("[[") && line.EndsWith("]]"))) continue;
            if (line.StartsWith('|') && line.EndsWith('|') && line.Length > 2)
            {
                var rows = new List<string[]>();
                do
                {
                    var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (!cells.All(c => Regex.IsMatch(c, @"^:?-+:?$"))) rows.Add(cells);
                    i++;
                } while (i < lines.Length && lines[i].Trim().StartsWith('|') && lines[i].Trim().EndsWith('|'));
                i--;
                int startRow = rows.Count > 1 && rows[0].Any(c => Regex.IsMatch(c, @"^(Indicador|Métrica|KPI|Fase|Etapa|Pilar|Columna|Cuadrante|Elemento|Título|Opción|Criterio|Antes)\b", RegexOptions.IgnoreCase)) ? 1 : 0;
                for (int r = startRow; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace)) continue;
                    string title = row[0].Replace("**", "").Trim();
                    string sub = row.Length > 1 ? row[1].Replace("**", "").Trim() : "";
                    var body = row.Skip(2).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Replace("**", "").Trim()).ToList();
                    cards.Add((title, sub, body));
                }
                continue;
            }
            if (line.StartsWith("### ") || line.StartsWith("## "))
            {
                var rawHead = line.TrimStart('#').Trim();
                var parts = rawHead.Split('|', 2, StringSplitOptions.TrimEntries);
                cards.Add((parts[0], parts.Length > 1 ? parts[1] : "", []));
                continue;
            }
            var cleaned = Regex.Replace(line, @"^(?:-\s*|[1-9][0-9]*[\.\)]\s*)", "").Trim();
            if (cards.Count > 0 && (line.StartsWith("- ") || !line.Contains(':')))
            {
                if (cards[^1].Subtitle == "" && cards[^1].Lines.Count == 0 && cleaned.Length <= 28)
                {
                    var last = cards[^1];
                    cards[^1] = (last.Title, cleaned.Replace("**", ""), last.Lines);
                }
                else
                {
                    cards[^1].Lines.Add(cleaned.Replace("**", ""));
                }
            }
            else if (cleaned.Contains(':'))
            {
                var colon = cleaned.Split(':', 2, StringSplitOptions.TrimEntries);
                var rhs = colon[1].Split('|', StringSplitOptions.TrimEntries);
                cards.Add((colon[0].Replace("**", ""), rhs[0].Replace("**", ""), rhs.Skip(1).ToList()));
            }
            else
            {
                cards.Add((cleaned.Replace("**", ""), "", []));
            }
        }
        return cards;
    }

    static void BuildPresentationTex(StringBuilder tex, Work work, string accent, Action<string> copyImage)
    {
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        tex.AppendLine(@"\documentclass[11pt]{article}");
        tex.AppendLine(@"\usepackage[paperwidth=960bp,paperheight=540bp,margin=0pt]{geometry}");
        tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,colortbl,tabularx,array,tikz}");
        tex.AppendLine(@"\renewcommand{\familydefault}{\sfdefault}");
        tex.AppendLine(@"\pagestyle{empty}\setlength{\parindent}{0pt}");
        tex.AppendLine(@"\hyphenpenalty=10000\exhyphenpenalty=10000\sloppy");
        tex.AppendLine(@"\definecolor{accent}{HTML}{" + accent + "}");
        tex.AppendLine(@"\definecolor{SlideNavy}{HTML}{243750}");
        tex.AppendLine(@"\definecolor{SlideCream}{HTML}{E4DAD3}");
        tex.AppendLine(@"\definecolor{SlideCard}{HTML}{EFECE7}");
        tex.AppendLine(@"\definecolor{SlideGold}{HTML}{E6AA68}");
        tex.AppendLine(@"\definecolor{SlideTeal}{HTML}{2C6467}");
        tex.AppendLine(@"\definecolor{SlideRow}{HTML}{EAF2F2}");
        tex.AppendLine(@"\definecolor{SlideInk}{HTML}{1E2328}");
        tex.AppendLine(@"\definecolor{SlideMuted}{HTML}{526068}");
        tex.AppendLine(@"\newcolumntype{Y}{>{\raggedright\arraybackslash}X}");
        tex.AppendLine(@"\newsavebox{\nimboslidebox}");
        tex.AppendLine(@"\newcommand{\nimbofitbox}[3]{%");
        tex.AppendLine(@"  \sbox{\nimboslidebox}{\parbox{#1}{#3}}%");
        tex.AppendLine(@"  \ifdim\dimexpr\ht\nimboslidebox+\dp\nimboslidebox\relax>#2\relax");
        tex.AppendLine(@"    \resizebox{!}{#2}{\usebox{\nimboslidebox}}%");
        tex.AppendLine(@"  \else");
        tex.AppendLine(@"    \usebox{\nimboslidebox}%");
        tex.AppendLine(@"  \fi");
        tex.AppendLine(@"}");
        tex.AppendLine(@"\begin{document}");

        void BeginSlide(string bgColor = "SlideCream", string? bgImage = null, double bgOpacity = 0.17)
        {
            tex.AppendLine(@"\noindent\begin{tikzpicture}[remember picture,overlay]");
            tex.AppendLine(@"\begin{scope}[shift={(current page.north west)},x=1bp,y=-1bp]");
            tex.AppendLine(@"\clip (0,0) rectangle (960,540);");
            tex.AppendLine(@"\fill[" + bgColor + @"] (0,0) rectangle (960,540);");
            if (!string.IsNullOrEmpty(bgImage))
            {
                copyImage(bgImage);
                tex.AppendLine(@"\begin{scope}[opacity=" + F(bgOpacity) + @"]");
                tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (480,270) {\includegraphics[width=960bp,height=540bp]{media/" + bgImage + @"}};");
                tex.AppendLine(@"\end{scope}");
            }
            tex.AppendLine(@"\fill[SlideNavy] (0,0) rectangle (960,6);");
            tex.AppendLine(@"\fill[SlideGold] (0,536) rectangle (960,540);");
        }

        void EndSlide()
        {
            tex.AppendLine(@"\end{scope}");
            tex.AppendLine(@"\end{tikzpicture}\par\null\clearpage");
        }

        void RenderCoverSlide(string titleText, string subtitleText, string authorText, string metaText, string? coverKey)
        {
            tex.AppendLine(@"\noindent\begin{tikzpicture}[remember picture,overlay]");
            tex.AppendLine(@"\begin{scope}[shift={(current page.north west)},x=0.96bp,y=-0.72bp]");
            tex.AppendLine(@"\clip (0,0) rectangle (1000,750);");
            tex.AppendLine(@"\fill[SlideNavy] (0,0) rectangle (1000,750);");
            if (!string.IsNullOrEmpty(coverKey))
            {
                copyImage(coverKey);
                tex.AppendLine(@"\node[anchor=north west,inner sep=0pt] at (0,0) {\includegraphics[width=960bp,height=540bp]{media/" + coverKey + @"}};");
            }
            tex.AppendLine(@"\fill[SlideNavy,opacity=0.68] (0,0) rectangle (1000,750);");
            tex.AppendLine(@"\fill[SlideNavy] (0,0) -- (380,0) .. controls (310,120) and (210,130) .. (140,210) .. controls (95,260) and (60,340) .. (0,375) -- cycle;");
            tex.AppendLine(@"\fill[SlideCream] (0,0) -- (285,0) .. controls (235,88) and (150,96) .. (98,162) .. controls (64,205) and (42,262) .. (0,292) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (0,0) -- (195,0) .. controls (165,58) and (105,64) .. (66,112) .. controls (40,145) and (24,185) .. (0,210) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (1000,0) -- (620,0) .. controls (690,120) and (790,130) .. (860,210) .. controls (905,260) and (940,340) .. (1000,375) -- cycle;");
            tex.AppendLine(@"\fill[SlideCream] (1000,0) -- (715,0) .. controls (765,88) and (850,96) .. (902,162) .. controls (936,205) and (958,262) .. (1000,292) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (1000,0) -- (805,0) .. controls (835,58) and (895,64) .. (934,112) .. controls (960,145) and (976,185) .. (1000,210) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (0,750) -- (0,415) .. controls (65,450) and (110,535) .. (175,585) .. controls (250,645) and (360,645) .. (430,750) -- cycle;");
            tex.AppendLine(@"\fill[SlideCream] (0,750) -- (0,500) .. controls (50,530) and (82,595) .. (132,635) .. controls (190,680) and (270,680) .. (325,750) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (0,750) -- (0,575) .. controls (35,595) and (58,642) .. (95,672) .. controls (138,705) and (195,705) .. (235,750) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (1000,750) -- (1000,415) .. controls (935,450) and (890,535) .. (825,585) .. controls (750,645) and (640,645) .. (570,750) -- cycle;");
            tex.AppendLine(@"\fill[SlideCream] (1000,750) -- (1000,500) .. controls (950,530) and (918,595) .. (868,635) .. controls (810,680) and (730,680) .. (675,750) -- cycle;");
            tex.AppendLine(@"\fill[SlideNavy] (1000,750) -- (1000,575) .. controls (965,595) and (942,642) .. (905,672) .. controls (862,705) and (805,705) .. (765,750) -- cycle;");
            tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (325,0) .. controls (265,105) and (175,112) .. (115,186) .. controls (78,232) and (48,298) .. (0,330);");
            tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (675,0) .. controls (735,105) and (825,112) .. (885,186) .. controls (922,232) and (952,298) .. (1000,330);");
            tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (0,460) .. controls (58,492) and (96,565) .. (152,612) .. controls (218,665) and (312,665) .. (375,750);");
            tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (1000,460) .. controls (942,492) and (904,565) .. (848,612) .. controls (782,665) and (688,665) .. (625,750);");
            tex.AppendLine(@"\node[anchor=center] at (500,375) {\nimbofitbox{620bp}{340bp}{\centering\color{white}");
            tex.AppendLine(@"{\fontsize{33}{38}\selectfont\bfseries " + Escape(titleText) + @"\par}");
            if (!string.IsNullOrWhiteSpace(subtitleText))
                tex.AppendLine(@"\vspace{12bp}{\fontsize{15}{20}\selectfont\color{SlideCream} " + Escape(subtitleText) + @"\par}");
            tex.AppendLine(@"\vspace{14bp}{\color{SlideGold}\rule{240bp}{2bp}\par}");
            if (!string.IsNullOrWhiteSpace(authorText))
                tex.AppendLine(@"\vspace{14bp}{\fontsize{16}{21}\selectfont\bfseries " + Escape(authorText) + @"\par}");
            if (!string.IsNullOrWhiteSpace(metaText))
                tex.AppendLine(@"\vspace{8bp}{\fontsize{12}{16}\selectfont\color{SlideCream} " + Escape(metaText) + @"\par}");
            tex.AppendLine(@"}};");
            tex.AppendLine(@"\end{scope}");
            tex.AppendLine(@"\end{tikzpicture}\par\null\clearpage");
        }

        string FormatSlideCell(string raw, Chapter slide, HashSet<int> usedImages, double maxImgH = 44)
        {
            var matches = Regex.Matches(raw, @"\[\[Imagen\s+([1-9][0-9]*)\]\]");
            var parts = new List<string>();
            foreach (Match m in matches)
            {
                int idx = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - 1;
                if (idx >= 0 && idx < slide.Images.Count)
                {
                    usedImages.Add(idx);
                    copyImage(slide.Images[idx]);
                    parts.Add(@"\raisebox{-.35\height}{\includegraphics[width=76bp,height=" + F(maxImgH) + @"bp,keepaspectratio]{media/" + slide.Images[idx] + @"}}");
                }
            }
            var clean = Regex.Replace(raw, @"\[\[Imagen\s+[1-9][0-9]*\]\]", "").Trim();
            if (clean != "") parts.Add(InlineParagraphs(clean));
            return string.Join(" ", parts);
        }

        string SlideRichText(string value, Chapter slide, HashSet<int> usedImages, double boxWidthBp)
        {
            var output = new StringBuilder();
            output.AppendLine(@"\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright");
            var lines = value.ReplaceLineEndings("\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (Regex.IsMatch(line.Trim(), @"^(\[\[Imagen\s+[1-9][0-9]*\]\]\s*)+$")) continue;
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
                    string fontCmd = rows.Count >= 6 ? @"\footnotesize" : @"\small";
                    string stretch = rows.Count >= 6 ? "1.14" : "1.24";
                    output.AppendLine(@"\par\smallskip\noindent{" + fontCmd + @"\renewcommand{\arraystretch}{" + stretch + @"}\setlength{\tabcolsep}{6pt}\begin{tabularx}{\linewidth}{|" + string.Join("|", Enumerable.Repeat("Y", columns)) + @"|}\hline");
                    for (int r = 0; r < rows.Count; r++)
                    {
                        var row = rows[r];
                        bool header = r == 0 && rows.Count > 1;
                        if (header) output.Append(@"\rowcolor{SlideTeal}");
                        else if (r % 2 == 1) output.Append(@"\rowcolor{SlideRow}");
                        else output.Append(@"\rowcolor{white}");
                        output.AppendLine(string.Join(" & ", Enumerable.Range(0, columns).Select(c =>
                        {
                            var cell = c < row.Length ? FormatSlideCell(row[c].Trim(), slide, usedImages, rows.Count >= 6 ? 34 : 44) : "";
                            if (header) return @"{\color{white}\bfseries " + cell + "}";
                            return c == 0 && !cell.Contains(@"\includegraphics") && !cell.Contains(@"\textbf{") ? @"{\bfseries\color{SlideNavy} " + cell + "}" : cell;
                        })) + @"\\\hline");
                    }
                    output.AppendLine(@"\end{tabularx}}\par\smallskip");
                }
                else if (line.StartsWith("[[Paleta:") && line.EndsWith("]]")) output.Append(RenderPalette(line[9..^2]));
                else if (line.StartsWith("[[Firmas:") && line.EndsWith("]]")) output.Append(RenderSignatures(line[9..^2]));
                else if (line.StartsWith("- ")) output.AppendLine(@"\noindent{\color{SlideGold}\textbullet}\enspace " + FormatSlideCell(line[2..], slide, usedImages) + @"\par\vspace{3bp}");
                else if (Regex.Match(line, @"^([1-9][0-9]*)\.\s+(.*)$") is { Success: true } num)
                    output.AppendLine(@"\noindent{\bfseries\color{SlideTeal}" + num.Groups[1].Value + @".}\enspace " + FormatSlideCell(num.Groups[2].Value, slide, usedImages) + @"\par\vspace{3bp}");
                else if (line.StartsWith("> "))
                {
                    output.AppendLine(@"\par\smallskip\noindent\colorbox{SlideCard}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\itshape\color{SlideNavy}");
                    do { output.AppendLine(FormatSlideCell(lines[i][2..], slide, usedImages) + @"\par"); i++; }
                    while (i < lines.Length && lines[i].StartsWith("> "));
                    i--; output.AppendLine(@"}}\par\smallskip");
                }
                else if (line.StartsWith("### ")) output.AppendLine(@"\par\smallskip\noindent{\normalsize\bfseries\color{SlideTeal} " + Escape(line[4..]) + @"}\par\vspace{2bp}");
                else if (line.StartsWith("## ")) output.AppendLine(@"\par\medskip\noindent{\large\bfseries\color{SlideNavy} " + Escape(line[3..]) + @"}\par\vspace{3bp}");
                else output.AppendLine(FormatSlideCell(line, slide, usedImages) + @"\par\vspace{3bp}");
            }
            return output.ToString();
        }

        var ordered = WorldModel.Ordered(work).ToList();
        var metaLine = string.Join(" · ", new[]
        {
            work.DocumentFacts.GetValueOrDefault("Institución", ""),
            work.DocumentFacts.GetValueOrDefault("Evento", ""),
            work.DocumentFacts.GetValueOrDefault("Fecha", "")
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        if (ordered.Count == 0 || ordered[0].Layout != "Portada")
        {
            RenderCoverSlide(work.Title, work.Subtitle, work.Author, metaLine, work.CoverImage);
        }

        foreach (var chapter in ordered)
        {
            var layout = string.IsNullOrWhiteSpace(chapter.Layout) ? "Texto e imagen" : chapter.Layout;
            if (layout == "Portada")
            {
                var coverImg = chapter.Images.Count > 0 ? chapter.Images[0] : work.CoverImage;
                var sub = chapter.Detail != "" ? chapter.Detail : (chapter.Summary != "" ? chapter.Summary : work.Subtitle);
                var bodyExtra = string.Join(" · ", new[] { chapter.PlainText.Replace('\n', ' ').Trim(), metaLine }.Where(s => s != ""));
                RenderCoverSlide(chapter.Title != "" ? chapter.Title : work.Title, sub, work.Author, bodyExtra, coverImg);
                continue;
            }

            if (layout == "Ficha de concepto")
            {
                var map = MapSlideConceptImages(chapter);
                var (refs, keys, extra) = ParseSlideConceptLists(chapter.PlainText);
                string? bgKey = map.TryGetValue("Fondo de diapositiva", out var bgSlot) ? bgSlot.Key : null;
                BeginSlide("SlideCream", bgKey, 0.17);

                tex.AppendLine(@"\node[anchor=north west,text=SlideNavy] at (32,26) {\nimbofitbox{320bp}{48bp}{\raggedright\fontsize{25}{28}\selectfont\bfseries " + Escape(chapter.Title) + @"}};");
                if (chapter.Detail != "")
                    tex.AppendLine(@"\node[anchor=north west,text=SlideTeal] at (32,76) {\nimbofitbox{320bp}{28bp}{\raggedright\fontsize{13.5}{16}\selectfont\bfseries (" + Escape(chapter.Detail) + @")}};");

                var conceptBody = chapter.Summary != "" ? chapter.Summary : string.Join(" ", extra);
                tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (365,26) {\nimbofitbox{563bp}{78bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{12}{15.2}\selectfont{\bfseries\color{SlideNavy}Concepto:} " + InlineParagraphs(conceptBody) + @"}};");
                tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (32,112) -- (928,112);");

                tex.AppendLine(@"\node[anchor=north,text=SlideNavy,font=\fontsize{10.5}{12}\selectfont\bfseries] at (74,132) {Plataforma};");
                tex.AppendLine(@"\fill[white,rounded corners=4bp,draw=SlideGold,line width=0.6bp] (35,148) rectangle (113,205);");
                if (map.TryGetValue("Plataforma", out var plat))
                {
                    copyImage(plat.Key);
                    tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (74,176.5) {\includegraphics[width=68bp,height=48bp,keepaspectratio]{media/" + plat.Key + @"}};");
                }

                tex.AppendLine(@"\node[anchor=north,text=SlideNavy,font=\fontsize{10.5}{12}\selectfont\bfseries] at (74,222) {Target \& ESRB};");
                tex.AppendLine(@"\fill[white,rounded corners=4bp,draw=SlideGold,line width=0.6bp] (35,238) rectangle (113,295);");
                if (map.TryGetValue("Target & ESRB", out var esrb))
                {
                    copyImage(esrb.Key);
                    tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (74,266.5) {\includegraphics[width=68bp,height=48bp,keepaspectratio]{media/" + esrb.Key + @"}};");
                }

                tex.AppendLine(@"\node[anchor=north,text=SlideNavy,font=\fontsize{10.5}{12}\selectfont\bfseries] at (256,132) {Referencias visuales};");
                (string Role, double X, double Y)[] refGrid =
                [
                    ("Referencia visual 1", 136, 152),
                    ("Referencia visual 2", 260, 152),
                    ("Referencia visual 3", 136, 228),
                    ("Referencia visual 4", 260, 228)
                ];
                foreach (var (role, rx, ry) in refGrid)
                {
                    tex.AppendLine(@"\fill[SlideCard,draw=SlideNavy!30,line width=0.5bp] (" + F(rx) + "," + F(ry) + ") rectangle (" + F(rx + 116) + "," + F(ry + 66) + ");");
                    if (map.TryGetValue(role, out var rimg))
                    {
                        copyImage(rimg.Key);
                        tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (" + F(rx + 58) + "," + F(ry + 33) + @") {\includegraphics[width=114bp,height=64bp,keepaspectratio]{media/" + rimg.Key + @"}};");
                    }
                }

                tex.AppendLine(@"\node[anchor=north west,text=SlideNavy,font=\fontsize{11}{13}\selectfont\bfseries] at (402,130) {Referencias:};");
                for (int i = 0; i < Math.Min(6, refs.Count); i++)
                {
                    double ry = 154 + i * 24.5;
                    tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (402," + F(ry) + @") {\parbox{340bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{12.5}\selectfont{\bfseries\color{SlideTeal}" + (i + 1) + @".} " + InlineParagraphs(refs[i]) + @"}};");
                }

                tex.AppendLine(@"\node[anchor=north west,text=SlideNavy,font=\fontsize{11}{13}\selectfont\bfseries] at (758,130) {Key Words:};");
                for (int i = 0; i < Math.Min(6, keys.Count); i++)
                {
                    double ky = 154 + i * 24.5;
                    tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (758," + F(ky) + @") {\parbox{175bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{12.5}\selectfont{\bfseries\color{SlideGold}" + (i + 1) + @".} " + InlineParagraphs(keys[i]) + @"}};");
                }

                double[] gx = [32, 336, 640];
                for (int g = 0; g < 3; g++)
                {
                    var role = "Gameplay " + (g + 1);
                    double x = gx[g];
                    tex.AppendLine(@"\fill[SlideNavy!90,draw=SlideNavy,line width=0.8bp] (" + F(x) + ",314) rectangle (" + F(x + 288) + ",502);");
                    if (map.TryGetValue(role, out var gimg))
                    {
                        copyImage(gimg.Key);
                        tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (" + F(x + 144) + @",408) {\includegraphics[width=286bp,height=186bp,keepaspectratio]{media/" + gimg.Key + @"}};");
                        var cap = gimg.Caption != "" ? gimg.Caption : role;
                        tex.AppendLine(@"\node[anchor=north,text=SlideNavy] at (" + F(x + 144) + @",508) {\parbox{284bp}{\centering\fontsize{10}{12}\selectfont\bfseries " + Escape(cap) + @"}};");
                    }
                }
                EndSlide();
                continue;
            }

            BeginSlide("SlideCream");
            double headerTitleW = chapter.Detail != "" ? 540 : 884;
            tex.AppendLine(@"\node[anchor=north west,text=SlideNavy] at (38,22) {\nimbofitbox{" + F(headerTitleW) + @"bp}{38bp}{\raggedright\fontsize{24}{27}\selectfont\bfseries " + Escape(chapter.Title) + @"}};");
            if (chapter.Detail != "")
                tex.AppendLine(@"\node[anchor=north east,text=SlideTeal] at (922,28) {\nimbofitbox{330bp}{30bp}{\raggedleft\fontsize{11.5}{14}\selectfont\bfseries " + Escape(chapter.Detail) + @"}};");
            tex.AppendLine(@"\draw[SlideGold,line width=1.6bp] (38,66) -- (922,66);");

            void RenderBottomSummary(string label, string text)
            {
                tex.AppendLine(@"\fill[SlideCard,rounded corners=4bp,draw=SlideGold,line width=0.7bp] (38,476) rectangle (922,520);");
                tex.AppendLine(@"\node[anchor=west,text=SlideNavy] at (50,498) {\nimbofitbox{860bp}{34bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{13}\selectfont{\bfseries\color{SlideTeal}" + Escape(label) + @":} " + InlineParagraphs(text) + @"}};");
            }

            var used = new HashSet<int>();
            if (layout is "Divisor de sección / Impacto" or "Cierre y contacto")
            {
                tex.AppendLine(@"\fill[SlideNavy,rounded corners=8bp,draw=SlideGold,line width=1.8bp] (38,84) rectangle (922,498);");
                var bodyLines = chapter.PlainText.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var pills = string.Join(@" \enspace$\cdot$\enspace ", bodyLines.Take(6).Select(l => Escape(l.TrimStart('-', '#', ' ').Replace("**", ""))));
                tex.AppendLine(@"\node[anchor=center,text=white] at (480,291) {\nimbofitbox{780bp}{360bp}{\centering\hyphenpenalty=10000\exhyphenpenalty=10000");
                if (chapter.Detail != "")
                    tex.AppendLine(@"{\fontsize{13}{16}\selectfont\bfseries\color{SlideGold} " + Escape(chapter.Detail.ToUpperInvariant()) + @"\par}\vspace{10bp}");
                tex.AppendLine(@"{\fontsize{30}{35}\selectfont\bfseries\color{white} " + Escape(chapter.Title) + @"\par}\vspace{12bp}");
                tex.AppendLine(@"{\color{SlideGold}\rule{200bp}{2bp}\par}");
                if (chapter.Summary != "")
                    tex.AppendLine(@"\vspace{12bp}{\fontsize{14.5}{19}\selectfont\color{SlideCream} " + InlineParagraphs(chapter.Summary) + @"\par}");
                if (pills != "")
                    tex.AppendLine(@"\vspace{14bp}{\fontsize{11.5}{15}\selectfont\bfseries\color{SlideGold} " + pills + @"\par}");
                tex.AppendLine(@"}};");
                EndSlide();
                continue;
            }

            if (layout == "Ficha de personaje / Equipo")
            {
                double areaBottom = chapter.Summary != "" ? 464 : 512;
                double areaH = areaBottom - 82;
                tex.AppendLine(@"\fill[SlideNavy,rounded corners=6bp,draw=SlideGold,line width=1.1bp] (38,82) rectangle (300," + F(areaBottom) + ");");
                if (chapter.Images.Count > 0)
                {
                    used.Add(0);
                    copyImage(chapter.Images[0]);
                    tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (169," + F(82 + (areaH - 42) / 2) + @") {\includegraphics[width=236bp,height=" + F(areaH - 58) + @"bp,keepaspectratio]{media/" + chapter.Images[0] + @"}};");
                }
                var roleCap = chapter.ImageCaptions.GetValueOrDefault(0, chapter.Detail != "" ? chapter.Detail : chapter.Title);
                tex.AppendLine(@"\node[anchor=south,text=white] at (169," + F(areaBottom - 10) + @") {\nimbofitbox{236bp}{26bp}{\centering\fontsize{11.5}{14}\selectfont\bfseries " + Escape(roleCap) + @"}};");

                var rightTex = SlideRichText(chapter.PlainText, chapter, used, 574);
                tex.AppendLine(@"\fill[white,rounded corners=6bp,draw=SlideNavy!30,line width=0.7bp] (318,82) rectangle (922," + F(areaBottom) + ");");
                tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (334,94) {\nimbofitbox{572bp}{" + F(areaH - 24) + @"bp}{" + rightTex + @"}};");
                if (chapter.Summary != "")
                    RenderBottomSummary("Perfil", chapter.Summary);
                EndSlide();
                continue;
            }

            if (layout == "Cita o conclusión destacada")
            {
                var mainQuote = chapter.PlainText.Replace("> ", "").Trim();
                if (mainQuote == "") mainQuote = chapter.Summary != "" ? chapter.Summary : chapter.Title;
                tex.AppendLine(@"\fill[SlideNavy,rounded corners=8bp,draw=SlideGold,line width=1.6bp] (38,88) rectangle (922,474);");
                tex.AppendLine(@"\node[anchor=center,text=white] at (480,281) {\nimbofitbox{780bp}{340bp}{\centering\hyphenpenalty=10000\exhyphenpenalty=10000{\fontsize{21}{28}\selectfont\itshape\bfseries ``" + InlineParagraphs(mainQuote) + @"''\par}");
                tex.AppendLine(@"\vspace{14bp}{\color{SlideGold}\rule{180bp}{2bp}\par}");
                if (chapter.Summary != "" && chapter.PlainText.Trim() != "")
                    tex.AppendLine(@"\vspace{12bp}{\fontsize{13}{17}\selectfont\color{SlideCream} " + InlineParagraphs(chapter.Summary) + @"\par}");
                tex.AppendLine(@"}};");
                EndSlide();
                continue;
            }

            if (layout is "Tres columnas" or "Cuatro columnas" or "Cuadrícula 2x2" or "Métricas e indicadores (KPIs)" or "Línea de tiempo / Roadmap" or "Flujo de proceso (Pipeline)" or "Arquitectura en capas" or "Embudo o pirámide estratégica" or "Checklist de validación" or "Comparativa (Antes / Después)")
            {
                var cards = ParseSlideCards(chapter.PlainText);
                if (cards.Count == 0)
                    cards.Add((chapter.Title, chapter.Detail, [chapter.PlainText]));
                double areaBottom = chapter.Summary != "" ? 464 : 512;
                double areaH = areaBottom - 82;

                if (layout == "Métricas e indicadores (KPIs)")
                {
                    int kCount = Math.Clamp(cards.Count, 1, 4);
                    double gap = 14;
                    double cardW = (884 - gap * (kCount - 1)) / kCount;
                    for (int i = 0; i < kCount; i++)
                    {
                        var item = cards[i];
                        var bigVal = item.Subtitle != "" ? item.Subtitle : item.Title;
                        var lbl = item.Subtitle != "" ? item.Title : (item.Lines.FirstOrDefault() ?? $"Indicador {i + 1}");
                        var desc = item.Subtitle != "" ? string.Join(" · ", item.Lines) : string.Join(" · ", item.Lines.Skip(1));
                        double x = 38 + i * (cardW + gap);
                        tex.AppendLine(@"\fill[white,rounded corners=6bp,draw=SlideGold,line width=1bp] (" + F(x) + ",84) rectangle (" + F(x + cardW) + "," + F(areaBottom) + ");");
                        tex.AppendLine(@"\node[anchor=center,text=SlideInk] at (" + F(x + cardW / 2) + "," + F(84 + areaH / 2) + @") {\nimbofitbox{" + F(cardW - 22) + @"bp}{" + F(areaH - 28) + @"bp}{\centering\hyphenpenalty=10000\exhyphenpenalty=10000");
                        tex.AppendLine(@"{\fontsize{11}{14}\selectfont\bfseries\color{SlideTeal} " + Escape(lbl.ToUpperInvariant()) + @"\par}\vspace{10bp}");
                        tex.AppendLine(@"{\fontsize{31}{35}\selectfont\bfseries\color{SlideNavy} " + Escape(bigVal) + @"\par}\vspace{10bp}");
                        tex.AppendLine(@"{\color{SlideGold}\rule{64bp}{1.6bp}\par}");
                        if (desc != "")
                            tex.AppendLine(@"\vspace{10bp}{\fontsize{11}{14.5}\selectfont\color{SlideMuted} " + InlineParagraphs(desc) + @"\par}");
                        tex.AppendLine(@"}};");
                    }
                }
                else if (layout is "Línea de tiempo / Roadmap" or "Flujo de proceso (Pipeline)")
                {
                    bool isPipeline = layout == "Flujo de proceso (Pipeline)";
                    int tCount = Math.Clamp(cards.Count, 1, 5);
                    double gap = 12;
                    double cardW = (884 - gap * (tCount - 1)) / tCount;
                    for (int i = 0; i < tCount; i++)
                    {
                        var item = cards[i];
                        double x = 38 + i * (cardW + gap);
                        string badgeCol = i % 2 == 0 ? "SlideNavy" : "SlideTeal";
                        string prefix = isPipeline ? $"PASO {i + 1} · " : $"{i + 1:D2} · ";
                        tex.AppendLine(@"\fill[white,rounded corners=6bp,draw=" + (isPipeline ? "SlideGold" : "SlideNavy!30") + @",line width=0.8bp] (" + F(x) + ",84) rectangle (" + F(x + cardW) + "," + F(areaBottom) + ");");
                        tex.AppendLine(@"\fill[" + badgeCol + @",rounded corners=4bp] (" + F(x + 8) + ",92) rectangle (" + F(x + cardW - 8) + ",126);");
                        tex.AppendLine(@"\node[anchor=west,text=white] at (" + F(x + 14) + @",109) {\nimbofitbox{" + F(cardW - 28) + @"bp}{24bp}{\raggedright\fontsize{10.5}{12.5}\selectfont\bfseries " + Escape(prefix + item.Title) + @"}};");
                        var bodySb = new StringBuilder();
                        if (item.Subtitle != "")
                            bodySb.AppendLine(@"{\fontsize{10.5}{13}\selectfont\bfseries\color{SlideGold} " + Escape(item.Subtitle) + @"\par}\vspace{4bp}");
                        foreach (var l in item.Lines)
                            bodySb.AppendLine(@"\noindent{\color{SlideTeal}\textbullet}\enspace " + InlineParagraphs(l) + @"\par\vspace{3bp}");
                        tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (" + F(x + 10) + @",136) {\nimbofitbox{" + F(cardW - 20) + @"bp}{" + F(areaBottom - 146) + @"bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{13.5}\selectfont " + bodySb + @"}};");
                    }
                }
                else if (layout is "Arquitectura en capas" or "Embudo o pirámide estratégica")
                {
                    bool isFunnel = layout == "Embudo o pirámide estratégica";
                    int lCount = Math.Clamp(cards.Count, 1, 5);
                    double gap = 8;
                    double rowH = (areaH - gap * (lCount - 1)) / lCount;
                    for (int i = 0; i < lCount; i++)
                    {
                        var item = cards[i];
                        double widthFactor = isFunnel ? (1.0 - i * 0.11) : 1.0;
                        double barW = Math.Round(884 * widthFactor);
                        double x = 480 - barW / 2;
                        double y = 84 + i * (rowH + gap);
                        string badgeCol = i % 2 == 0 ? "SlideNavy" : "SlideTeal";
                        tex.AppendLine(@"\fill[white,rounded corners=5bp,draw=SlideGold,line width=0.9bp] (" + F(x) + "," + F(y) + ") rectangle (" + F(x + barW) + "," + F(y + rowH) + ");");
                        tex.AppendLine(@"\fill[" + badgeCol + @",rounded corners=4bp] (" + F(x + 8) + "," + F(y + 6) + ") rectangle (" + F(x + 258) + "," + F(y + rowH - 6) + ");");
                        tex.AppendLine(@"\node[anchor=west,text=white] at (" + F(x + 16) + "," + F(y + rowH / 2) + @") {\nimbofitbox{234bp}{" + F(rowH - 16) + @"bp}{\raggedright{\fontsize{11.5}{14}\selectfont\bfseries " + Escape(item.Title) + @"}" + (item.Subtitle != "" ? @"\par{\fontsize{9.5}{11.5}\selectfont\color{SlideGold} " + Escape(item.Subtitle) + "}" : "") + @"}};");
                        var descText = string.Join(" · ", item.Lines);
                        if (descText == "") descText = item.Subtitle;
                        tex.AppendLine(@"\node[anchor=west,text=SlideInk] at (" + F(x + 272) + "," + F(y + rowH / 2) + @") {\nimbofitbox{" + F(barW - 286) + @"bp}{" + F(rowH - 14) + @"bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{13.5}\selectfont " + InlineParagraphs(descText) + @"}};");
                    }
                }
                else if (layout is "Cuadrícula 2x2" or "Checklist de validación")
                {
                    bool isCheck = layout == "Checklist de validación";
                    double gapX = 16, gapY = 14;
                    double cardW = (884 - gapX) / 2;
                    double cardH = (areaH - gapY) / 2;
                    for (int i = 0; i < Math.Min(4, cards.Count); i++)
                    {
                        var item = cards[i];
                        int r = i / 2, c = i % 2;
                        double x = 38 + c * (cardW + gapX);
                        double y = 84 + r * (cardH + gapY);
                        tex.AppendLine(@"\fill[white,rounded corners=6bp,draw=" + (isCheck ? "SlideTeal" : "SlideNavy!30") + @",line width=0.8bp] (" + F(x) + "," + F(y) + ") rectangle (" + F(x + cardW) + "," + F(y + cardH) + ");");
                        var bodySb = new StringBuilder();
                        bodySb.AppendLine(@"{\fontsize{13.5}{16.5}\selectfont\bfseries\color{" + (i % 2 == 0 ? "SlideNavy" : "SlideTeal") + @"} " + Escape((isCheck ? "[OK] " : "") + item.Title) + @"\par}");
                        if (item.Subtitle != "")
                            bodySb.AppendLine(@"\vspace{2bp}{\fontsize{10.5}{13}\selectfont\bfseries\color{SlideMuted} " + Escape(item.Subtitle) + @"\par}");
                        bodySb.AppendLine(@"\vspace{4bp}{\color{SlideGold}\rule{\linewidth}{1bp}\par}\vspace{4bp}");
                        foreach (var l in item.Lines)
                            bodySb.AppendLine(@"\noindent{\color{" + (isCheck ? "SlideTeal" : "SlideGold") + @"}\textbullet}\enspace " + InlineParagraphs(l) + @"\par\vspace{2.5bp}");
                        tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (" + F(x + 14) + "," + F(y + 10) + @") {\nimbofitbox{" + F(cardW - 28) + @"bp}{" + F(cardH - 20) + @"bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{13.5}\selectfont " + bodySb + @"}};");
                    }
                }
                else
                {
                    int colCount = layout == "Comparativa (Antes / Después)" ? 2 : (layout == "Cuatro columnas" ? 4 : 3);
                    double gap = colCount == 4 ? 12 : 16;
                    double cardW = (884 - gap * (colCount - 1)) / colCount;
                    for (int i = 0; i < Math.Min(colCount, cards.Count); i++)
                    {
                        var item = cards[i];
                        double x = 38 + i * (cardW + gap);
                        string headCol = i % 2 == 0 ? "SlideNavy" : "SlideTeal";
                        tex.AppendLine(@"\fill[white,rounded corners=6bp,draw=SlideNavy!30,line width=0.7bp] (" + F(x) + ",84) rectangle (" + F(x + cardW) + "," + F(areaBottom) + ");");
                        tex.AppendLine(@"\fill[" + headCol + @",rounded corners=4bp] (" + F(x + 10) + ",94) rectangle (" + F(x + cardW - 10) + ",130);");
                        tex.AppendLine(@"\node[anchor=west,text=white] at (" + F(x + 16) + @",112) {\nimbofitbox{" + F(cardW - 32) + @"bp}{26bp}{\raggedright\fontsize{12}{14.5}\selectfont\bfseries " + Escape(item.Title) + @"}};");
                        double contentTop = 140;
                        if (i < chapter.Images.Count)
                        {
                            copyImage(chapter.Images[i]);
                            tex.AppendLine(@"\node[anchor=north,inner sep=0pt] at (" + F(x + cardW / 2) + @",140) {\includegraphics[width=" + F(cardW - 24) + @"bp,height=105bp,keepaspectratio]{media/" + chapter.Images[i] + @"}};");
                            contentTop = 252;
                        }
                        var bodySb = new StringBuilder();
                        if (item.Subtitle != "")
                            bodySb.AppendLine(@"{\fontsize{11}{13.5}\selectfont\bfseries\color{SlideTeal} " + Escape(item.Subtitle) + @"\par}\vspace{4bp}");
                        foreach (var l in item.Lines)
                            bodySb.AppendLine(@"\noindent{\color{SlideGold}\textbullet}\enspace " + InlineParagraphs(l) + @"\par\vspace{3bp}");
                        tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (" + F(x + 14) + "," + F(contentTop) + @") {\nimbofitbox{" + F(cardW - 28) + @"bp}{" + F(areaBottom - contentTop - 10) + @"bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{14}\selectfont " + bodySb + @"}};");
                    }
                }
                if (chapter.Summary != "")
                    RenderBottomSummary("Síntesis", chapter.Summary);
                EndSlide();
                continue;
            }

            if (layout == "Galería triple")
            {
                double gap = 16;
                double cardW = (884 - gap * 2) / 3;
                double boxH = chapter.Summary != "" ? 380 : 426;
                for (int i = 0; i < 3; i++)
                {
                    double x = 38 + i * (cardW + gap);
                    tex.AppendLine(@"\fill[white,rounded corners=5bp,draw=SlideGold,line width=0.8bp] (" + F(x) + ",84) rectangle (" + F(x + cardW) + "," + F(84 + boxH) + ");");
                    if (i < chapter.Images.Count)
                    {
                        copyImage(chapter.Images[i]);
                        tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (" + F(x + cardW / 2) + "," + F(84 + boxH / 2 - 12) + @") {\includegraphics[width=" + F(cardW - 14) + @"bp,height=" + F(boxH - 42) + @"bp,keepaspectratio]{media/" + chapter.Images[i] + @"}};");
                    }
                    var cap = chapter.ImageCaptions.GetValueOrDefault(i, $"Lámina {i + 1}");
                    tex.AppendLine(@"\node[anchor=south,text=SlideNavy] at (" + F(x + cardW / 2) + "," + F(84 + boxH - 6) + @") {\nimbofitbox{" + F(cardW - 12) + @"bp}{24bp}{\centering\fontsize{10.5}{13}\selectfont\bfseries " + Escape(cap) + @"}};");
                }
                if (chapter.Summary != "")
                    RenderBottomSummary("Síntesis visual", chapter.Summary);
                EndSlide();
                continue;
            }

            if (layout is "Tabla visual" or "Matriz de riesgos y seguridad")
            {
                double maxTblH = chapter.Summary != "" ? 384 : 434;
                var bodyTex = SlideRichText(chapter.PlainText, chapter, used, 884);
                tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (38,78) {\nimbofitbox{884bp}{" + F(maxTblH) + @"bp}{" + bodyTex + @"}};");
                if (chapter.Summary != "")
                    RenderBottomSummary(layout == "Matriz de riesgos y seguridad" ? "Protocolo de seguridad" : "Nota clave", chapter.Summary);
                EndSlide();
                continue;
            }

            if (layout == "Imagen protagonista" && chapter.Images.Count > 0)
            {
                used.Add(0);
                copyImage(chapter.Images[0]);
                tex.AppendLine(@"\fill[SlideNavy!92,rounded corners=5bp,draw=SlideGold,line width=1bp] (38,78) rectangle (922,458);");
                tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (480,268) {\includegraphics[width=876bp,height=372bp,keepaspectratio]{media/" + chapter.Images[0] + @"}};");
                var cap = chapter.ImageCaptions.GetValueOrDefault(0, "");
                var footer = string.Join(" — ", new[] { cap, chapter.PlainText.Replace('\n', ' ').Trim(), chapter.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (footer != "")
                    tex.AppendLine(@"\node[anchor=north,text=SlideNavy] at (480,466) {\nimbofitbox{860bp}{52bp}{\centering\fontsize{11.5}{14.5}\selectfont\bfseries " + InlineParagraphs(footer) + @"}};");
                EndSlide();
                continue;
            }

            if (layout == "Texto" || (chapter.Images.Count == 0 && layout != "Dos columnas"))
            {
                double maxTxtH = chapter.Summary != "" ? 384 : 434;
                var bodyTex = SlideRichText(chapter.PlainText, chapter, used, 884);
                tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (38,80) {\nimbofitbox{884bp}{" + F(maxTxtH) + @"bp}{" + bodyTex + @"}};");
                if (chapter.Summary != "")
                    RenderBottomSummary("Síntesis", chapter.Summary);
                EndSlide();
                continue;
            }

            var leftTex = SlideRichText(chapter.PlainText, chapter, used, 424);
            tex.AppendLine(@"\node[anchor=north west,text=SlideInk] at (38,80) {\nimbofitbox{424bp}{436bp}{" + leftTex + @"}};");

            var remainingImages = Enumerable.Range(0, chapter.Images.Count).Where(i => !used.Contains(i)).ToList();
            if (layout is "Moodboard y paleta" or "Moodboard y landmark" && remainingImages.Count > 1)
            {
                var mosaicImgs = remainingImages.Take(6).ToList();
                int cols = mosaicImgs.Count >= 5 ? 3 : 2;
                int rows = (int)Math.Ceiling(mosaicImgs.Count / (double)cols);
                double areaW = 436;
                double areaH = chapter.Summary != "" ? 344 : 420;
                double gap = 10;
                double cellW = (areaW - gap * (cols - 1)) / cols;
                double cellH = (areaH - gap * (rows - 1)) / rows;
                for (int idx = 0; idx < mosaicImgs.Count; idx++)
                {
                    int imgIdx = mosaicImgs[idx];
                    copyImage(chapter.Images[imgIdx]);
                    int r = idx / cols;
                    int c = idx % cols;
                    double cx = 486 + c * (cellW + gap);
                    double cy = 82 + r * (cellH + gap);
                    var cap = chapter.ImageCaptions.GetValueOrDefault(imgIdx, "");
                    double imgBoxH = cap != "" ? cellH - 18 : cellH - 4;
                    tex.AppendLine(@"\fill[white,rounded corners=3bp,draw=SlideNavy!25,line width=0.5bp] (" + F(cx) + "," + F(cy) + ") rectangle (" + F(cx + cellW) + "," + F(cy + cellH) + ");");
                    tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (" + F(cx + cellW / 2) + "," + F(cy + imgBoxH / 2 + 2) + @") {\includegraphics[width=" + F(cellW - 6) + @"bp,height=" + F(imgBoxH - 4) + @"bp,keepaspectratio]{media/" + chapter.Images[imgIdx] + @"}};");
                    if (cap != "")
                        tex.AppendLine(@"\node[anchor=north,text=SlideMuted] at (" + F(cx + cellW / 2) + "," + F(cy + imgBoxH + 2) + @") {\nimbofitbox{" + F(cellW - 4) + @"bp}{14bp}{\centering\fontsize{8.5}{10}\selectfont\bfseries " + Escape(cap) + @"}};");
                }
            }
            else if (remainingImages.Count > 0)
            {
                int imgIdx = remainingImages[0];
                copyImage(chapter.Images[imgIdx]);
                double boxH = chapter.Summary != "" ? 344 : 420;
                var cap = chapter.ImageCaptions.GetValueOrDefault(imgIdx, "");
                tex.AppendLine(@"\fill[white,rounded corners=5bp,draw=SlideGold,line width=0.8bp] (486,82) rectangle (922," + F(82 + boxH) + ");");
                tex.AppendLine(@"\node[anchor=center,inner sep=0pt] at (704," + F(82 + (boxH - (cap != "" ? 24 : 8)) / 2) + @") {\includegraphics[width=424bp,height=" + F(boxH - (cap != "" ? 32 : 14)) + @"bp,keepaspectratio]{media/" + chapter.Images[imgIdx] + @"}};");
                if (cap != "")
                    tex.AppendLine(@"\node[anchor=south,text=SlideMuted] at (704," + F(82 + boxH - 5) + @") {\nimbofitbox{420bp}{18bp}{\centering\fontsize{9.5}{11.5}\selectfont\itshape " + Escape(cap) + @"}};");
            }

            if (chapter.Summary != "")
            {
                double sumY = remainingImages.Count > 0 ? 438 : 82;
                double sumH = remainingImages.Count > 0 ? 80 : 420;
                tex.AppendLine(@"\fill[SlideCard,rounded corners=4bp,draw=SlideGold,line width=0.7bp] (486," + F(sumY) + ") rectangle (922," + F(sumY + sumH) + ");");
                tex.AppendLine(@"\node[anchor=north west,text=SlideNavy] at (498," + F(sumY + 8) + @") {\nimbofitbox{412bp}{" + F(sumH - 16) + @"bp}{\hyphenpenalty=10000\exhyphenpenalty=10000\raggedright\fontsize{10.5}{13.5}\selectfont{\bfseries\color{SlideTeal}Síntesis visual:}\par\vspace{2bp}" + InlineParagraphs(chapter.Summary) + @"}};");
            }
            EndSlide();
        }
        tex.AppendLine(@"\end{document}");
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
            var accent = work.Type switch { "CV" => "65455B", "Informe" => "315671", "Artículo" => "4E476E", "Carta" => "784F3E", "Acta" => "5C634F", "Apuntes" => "506C46", "Propuesta" => "474F76", _ => "365E64" };
            accent=work.PrintAccent switch {"Petróleo"=>"245966","Vino"=>"843B59","Oliva"=>"51683C","Grafito"=>"414650",_=>accent};
            void CopyImage(string key)
            {
                if (Path.GetFileName(key) != key || !Regex.IsMatch(key, @"\A[0-9a-f]{64}\.(?:png|jpe?g)\z")) throw new InvalidDataException("Imagen no migrada: " + key);
                Directory.CreateDirectory(Path.Combine(temp, "media"));
                var dest=Path.Combine(temp,"media",key);
                if(!File.Exists(dest))File.Copy(Path.Combine(media,key),dest);
            }
            if(!glossary && work.UseTexSource){
                if(string.IsNullOrWhiteSpace(work.TexSource))throw new InvalidDataException("El proyecto LaTeX está vacío.");
                foreach(var (relative,key) in work.TexResources){
                    var path=Path.GetFullPath(relative,temp);
                    if(Path.IsPathRooted(relative)||!path.StartsWith(temp+Path.DirectorySeparatorChar,(OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))||Path.GetFileName(key)!=key||!Regex.IsMatch(key,@"^[0-9a-f]{64}\.[a-z0-9]+$")||Path.GetFileName(path).Equals("obra.tex",StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Ruta de recurso no válida: "+relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.Copy(Path.Combine(media,key),path);
                }
                tex.Append(work.TexSource);
            }
            else if (slide)
            {
                BuildPresentationTex(tex, work, accent, CopyImage);
            }
            else{
            tex.AppendLine(novel ? @"\documentclass[11pt,a5paper,openany]{book}" : glossary ? @"\documentclass[11pt,twoside]{article}" : @"\documentclass[11pt]{article}");
            tex.AppendLine(@"\usepackage{fontspec,graphicx,xcolor,colortbl,tabularx,array,tikz}");
            tex.AppendLine(@"\newcolumntype{Y}{>{\raggedright\arraybackslash}X}");
            var (leafWidth,leafHeight)=Dossiers.Size(work.GlossaryPaper);
            bool isLibreta = !glossary && work.Type == "Apuntes" && (style == "LibretaUASLP" || work.CoverStyle == "Libreta universitaria (UASLP)" || !string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault("Universidad")));
            bool isPractica = !glossary && work.Type == "Informe" && (style == "PracticaRedes" || work.CoverStyle == "Portada de Práctica (P3/P4/P5)" || !string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault("Materia y ciclo")));
            tex.AppendLine(glossary ? $@"\usepackage[paperwidth={leafWidth}mm,paperheight={leafHeight}mm,inner=12mm,outer=10mm,top=12mm,bottom=15mm,footskip=8mm]{{geometry}}" :
                novel ? @"\usepackage["+paper+@",inner=18mm,outer=15mm]{geometry}" :
                @"\usepackage[" + paper + @",margin=23mm]{geometry}");
            if(!glossary && style is "Moderna" or "Ejecutiva" or "BandaLateral" or "LibretaUASLP")tex.AppendLine(@"\renewcommand{\familydefault}{\sfdefault}");
            tex.AppendLine(@"\definecolor{accent}{HTML}{" + accent + "}");
            tex.AppendLine(@"\definecolor{UASLPblue}{HTML}{004785}");
            tex.AppendLine(@"\definecolor{UASLPgreen}{HTML}{22714B}");
            tex.AppendLine(@"\definecolor{DarkGray}{HTML}{464646}");
            if(glossary){
                using var template=typeof(Publisher).Assembly.GetManifestResourceStream("Nimbo.Publishing.fichas.tex")??throw new IOException("Falta la plantilla del cuadernillo.");
                using var copy=File.Create(Path.Combine(temp,"fichas.tex"));template.CopyTo(copy);
                tex.AppendLine(@"\input{fichas.tex}");
            }
            if (novel) tex.AppendLine(@"\renewcommand{\chaptername}{Capítulo}\renewcommand{\partname}{Parte}\renewcommand{\contentsname}{Contenido}");
            else if (isLibreta) tex.AppendLine(@"\renewcommand{\contentsname}{\color{UASLPblue}Índice}");
            else if (!glossary) tex.AppendLine(@"\renewcommand{\contentsname}{Contenido}");
            if (!glossary && work.Chapters.Any(c => Enumerable.Range(0,c.Images.Count).Any(i=>Illustrations.Layout(c,i).StartsWith("Ajustar"))))
                tex.AppendLine(@"\usepackage{wrapfig}\newsavebox{\nimbofigure}");
            if(!glossary && work.Type=="CV")tex.AppendLine(@"\setlength{\parindent}{0pt}");
            var title = glossary ? "Glosario de " + work.Title : work.Title;
            tex.AppendLine(@"\title{" + Escape(title) + @"}\author{" + Escape(work.Author) + @"}\date{}\begin{document}");
            if(!glossary){
                var leading=work.PrintSpacing switch {"Abierto"=>"1.2","Amplio"=>"1.5",_=>"1"};
                tex.AppendLine(@"\linespread{"+leading+@"}\selectfont");
                var indent=work.PrintIndent switch {"Sin sangría"=>"0mm","5 mm"=>"5mm","8 mm"=>"8mm",_=>null};
                if(indent!=null)tex.AppendLine(@"\setlength{\parindent}{"+indent+"}");
            }
            string Caption(string? text)=>string.IsNullOrWhiteSpace(text)?"":@"\par\smallskip{\small\itshape "+Escape(text)+@"\par}";
            void Image(string key, string layout = "Centrada",string? legend=null)
            {
                if(string.IsNullOrEmpty(key))return;
                CopyImage(key);var caption=Caption(legend);
                if (layout.StartsWith("Ajustar"))
                {
                    var side = layout.EndsWith("izquierda") ? "l" : "r";
                    tex.AppendLine(@"\par\sbox{\nimbofigure}{\begin{minipage}{.36\linewidth}\centering\includegraphics[width=\linewidth,height=.35\textheight,keepaspectratio]{media/" + key + "}" + caption + @"\end{minipage}}");
                    tex.AppendLine(@"\ifdim\dimexpr\pagegoal-\pagetotal\relax<\dimexpr\ht\nimbofigure+\dp\nimbofigure+2\intextsep+\baselineskip\relax\newpage\fi");
                    tex.AppendLine(@"\begin{wrapfigure}{" + side + @"}{.36\linewidth}\usebox{\nimbofigure}\end{wrapfigure}");
                }
                else if(layout == "Lámina ilustrada")
                    tex.AppendLine(@"\vspace*{\fill}\noindent\includegraphics[width=\linewidth,height=.52\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\par\medskip");
                else if (layout == "Página completa")
                    tex.AppendLine(@"\clearpage\thispagestyle{empty}\vspace*{\fill}\begin{center}\includegraphics[width=\linewidth,height="+(caption==""?".88":".76")+@"\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}\vspace*{\fill}\clearpage");
                else tex.AppendLine(@"\begin{center}\includegraphics[width=.84\linewidth,height=.47\textheight,keepaspectratio]{media/" + key + "}"+caption+@"\end{center}");
            }
            if(novel && work.Publication.RunningHead=="Sin encabezado")tex.AppendLine(@"\pagestyle{plain}");
            tex.AppendLine(@"\newsavebox{\nimbocoverbox}");
            void Cover(string picture,bool back){
                tex.AppendLine(@"\clearpage\thispagestyle{empty}");
                if(picture.Length>0){
                    CopyImage(picture);
                    var file="media/"+picture;
                    tex.AppendLine(@"\AddToHookNext{shipout/background}{\put(0,-\paperheight){\vbox to \paperheight{\vss\hbox to \paperwidth{\hss\sbox0{\includegraphics[width=\paperwidth]{"+file+@"}}\ifdim\ht0<\paperheight\includegraphics[height=\paperheight]{"+file+@"}\else\usebox0\fi\hss}\vss}}}");
                }
                else{
                    tex.AppendLine(@"\AddToHookNext{shipout/background}{\put(0,-\paperheight){\textcolor{accent!82!black}{\rule{\paperwidth}{\paperheight}}}}");
                }
                var coverMode = work.CoverStyle switch
                {
                    "Centrada" or "Cartela central difuminada" => "Centrada",
                    "Cabecera" or "Franja superior de autor" => "Cabecera",
                    "Marco áureo" or "Marco áureo con velo" => "Marco áureo",
                    "Banda completa" or "Banda diagonal moderna" or "Banda completa translúcida" => "Banda completa",
                    "Tejuelo clásico" or "Tejuelo clásico translúcido" => "Tejuelo clásico",
                    _ => "Al pie"
                };
                if (!back && coverMode == "Marco áureo")
                {
                    tex.AppendLine(@"\AddToHookNext{shipout/foreground}{\color{white!75!yellow}\linethickness{.8pt}\put(7mm,-\dimexpr\paperheight-7mm\relax){\framebox(\dimexpr\paperwidth-14mm\relax,\dimexpr\paperheight-14mm\relax){}}}");
                }
                bool topPos = !back && coverMode == "Cabecera";
                bool midPos = !back && coverMode == "Centrada";
                bool fullBand = !back && coverMode == "Banda completa";
                if (!topPos) tex.AppendLine(@"\null\vfill");
                if (midPos) tex.AppendLine(@"\vspace*{10mm}");
                var boxWidth = fullBand ? @"\dimexpr\linewidth-12pt\relax" : @"\dimexpr\linewidth-24pt\relax";
                tex.AppendLine(@"\sbox{\nimbocoverbox}{\parbox{" + boxWidth + @"}{\color{white}\centering\vspace{10pt}");
                if(back){
                    tex.AppendLine(@"{\footnotesize\bfseries\color{white!80!yellow}SINOPSIS\par}\smallskip");
                    tex.AppendLine(Paragraphs(work.Description.Length>900?"":work.Description));
                }
                else{
                    if(novel) tex.AppendLine(@"{\footnotesize\bfseries\color{white!78!yellow}NOVELA\par}\smallskip");
                    tex.AppendLine(@"{\Huge\bfseries "+Escape(work.Title)+@"\par}");
                    if(work.ShowCoverSubtitle && work.Subtitle!="")tex.AppendLine(@"\medskip{\large\itshape "+Escape(work.Subtitle)+@"\par}");
                    tex.AppendLine(@"\medskip{\color{white!75!yellow}\rule{38mm}{.7pt}\par}");
                    if(work.Author!="")tex.AppendLine(@"\medskip{\large\bfseries "+Escape(work.Author)+@"\par}");
                }
                tex.AppendLine(@"\vspace{10pt}}}");
                tex.AppendLine(@"\noindent\begin{tikzpicture}");
                tex.AppendLine(@"\path (0,0) node[inner sep=0pt] (box) {\phantom{\usebox{\nimbocoverbox}}};");
                for (int b = 6; b >= 1; b--)
                {
                    var pad = (10 + b * 1.8).ToString("0.0", CultureInfo.InvariantCulture);
                    var vpad = (6 + b * 1.8).ToString("0.0", CultureInfo.InvariantCulture);
                    tex.AppendLine(@"\draw[black,opacity=0.06,line width=2.4pt,rounded corners=6pt] ([xshift=-" + pad + @"pt,yshift=-" + vpad + @"pt]box.south west) rectangle ([xshift=" + pad + @"pt,yshift=" + vpad + @"pt]box.north east);");
                }
                tex.AppendLine(@"\fill[accent!24!black,opacity=0.62,rounded corners=4pt] ([xshift=-10pt,yshift=-6pt]box.south west) rectangle ([xshift=10pt,yshift=6pt]box.north east);");
                tex.AppendLine(@"\draw[white!75!yellow,opacity=0.82,line width=0.85pt,rounded corners=4pt] ([xshift=-10pt,yshift=-6pt]box.south west) rectangle ([xshift=10pt,yshift=6pt]box.north east);");
                tex.AppendLine(@"\draw[white!75!yellow,opacity=0.48,line width=0.45pt,rounded corners=3pt] ([xshift=-7pt,yshift=-3pt]box.south west) rectangle ([xshift=7pt,yshift=3pt]box.north east);");
                tex.AppendLine(@"\node[inner sep=0pt] at (box.center) {\usebox{\nimbocoverbox}};");
                tex.AppendLine(@"\end{tikzpicture}\par");
                if(back && work.Description.Length>900)tex.AppendLine(@"\medskip\noindent "+Paragraphs(work.Description));
                if(midPos || topPos)tex.AppendLine(@"\vfill\null");
                tex.AppendLine(@"\clearpage");
            }
            if(!glossary && work.FrameStyle is "Filete" or "Doble filete" or "Esquinas"){
                tex.AppendLine(@"\newcommand{\nimboframe}[1]{\put(#1,-\dimexpr\paperheight-#1\relax){\framebox(\dimexpr\paperwidth-#1-#1\relax,\dimexpr\paperheight-#1-#1\relax){}}}");
                string lines=work.FrameStyle=="Esquinas"?@"\put(6mm,-6mm){\line(1,0){18mm}}\put(6mm,-6mm){\line(0,-1){18mm}}\put(\dimexpr\paperwidth-6mm\relax,-\dimexpr\paperheight-6mm\relax){\line(-1,0){18mm}}\put(\dimexpr\paperwidth-6mm\relax,-\dimexpr\paperheight-6mm\relax){\line(0,1){18mm}}":@"\nimboframe{5mm}"+(work.FrameStyle=="Doble filete"?@"\nimboframe{6mm}":"");
                tex.AppendLine(@"\AddToHook{shipout/foreground}{\color{accent}\linethickness{.45pt}"+lines+"}");
            }
            var facts=EditorialTemplate.FrontMatter(work.Type).Where(k=>!string.IsNullOrWhiteSpace(work.DocumentFacts.GetValueOrDefault(k))).ToArray();
            bool designedCover=!glossary && (novel || (!isLibreta && !isPractica && work.CoverStyle is "Al pie" or "Centrada" or "Cabecera"));
            if(designedCover)Cover(work.CoverImage,false);
            if(!glossary && isLibreta){
                string Fact(string k, string def = "") => work.DocumentFacts.TryGetValue(k, out var val) && !string.IsNullOrWhiteSpace(val) ? val.Trim() : def;
                var univ = Fact("Universidad", "Universidad Autónoma de San Luis Potosí");
                var fac = Fact("Facultad", "Facultad de Ingeniería");
                var asig = Fact("Asignatura", work.Subtitle != "" ? work.Subtitle : work.Title);
                var clave = Fact("Clave de clase", "");
                var prof = Fact("Docente", "");
                var carrera = Fact("Carrera", "");
                var claveU = Fact("Clave U", "");
                var alumno = Fact("Alumno", work.Author);
                var lugarFecha = Fact("Lugar y fecha", Fact("Periodo", ""));

                tex.AppendLine(@"\begin{titlepage}\centering\vspace*{0.6cm}");
                tex.AppendLine(@"{\LARGE\bfseries\color{UASLPblue} " + Escape(univ) + @"\par}\vspace{0.25cm}");
                tex.AppendLine(@"{\Large\color{DarkGray} " + Escape(fac) + @"\par}\vspace{0.35cm}");
                tex.AppendLine(@"{\large\bfseries " + Escape(asig) + @"\par}\vspace{0.8cm}");
                tex.AppendLine(@"\colorbox{UASLPblue!10}{\parbox{0.86\textwidth}{\centering\vspace{0.45cm}");
                tex.AppendLine(@"{\Large\bfseries\color{UASLPblue} Apuntes:\par}\vspace{0.25cm}");
                tex.AppendLine(@"{\large\bfseries " + Escape(work.Title) + @"\par}");
                if (work.Subtitle != "" && work.Subtitle != asig)
                    tex.AppendLine(@"\vspace{0.15cm}{\normalsize\color{DarkGray} " + Escape(work.Subtitle) + @"\par}");
                tex.AppendLine(@"\vspace{0.45cm}}}\par\vspace{0.7cm}");
                if (work.CoverImage != "")
                {
                    CopyImage(work.CoverImage);
                    tex.AppendLine(@"\includegraphics[width=.55\linewidth,height=.20\textheight,keepaspectratio]{media/" + work.CoverImage + @"}\par\vspace{0.5cm}");
                }
                tex.AppendLine(@"{\large\renewcommand{\arraystretch}{1.28}\begin{tabularx}{0.86\textwidth}{>{\bfseries\color{UASLPblue}}l X}");
                tex.AppendLine(@"Clase: & " + Escape(asig + (clave != "" ? " (" + clave + ")" : "")) + @" \\");
                if (prof != "") tex.AppendLine(@"Profesor: & " + Escape(prof) + @" \\");
                if (carrera != "") tex.AppendLine(@"Carrera: & " + Escape(carrera) + @" \\");
                if (claveU != "") tex.AppendLine(@"Clave U: & " + Escape(claveU) + @" \\");
                foreach (var k in facts)
                {
                    if (k is not ("Universidad" or "Facultad" or "Asignatura" or "Clave de clase" or "Docente" or "Carrera" or "Clave U" or "Alumno" or "Lugar y fecha"))
                        tex.AppendLine(Escape(k) + @": & " + Escape(work.DocumentFacts[k]) + @" \\");
                }
                tex.AppendLine(@"\end{tabularx}}\par\vspace{0.6cm}");
                if (alumno != "")
                {
                    tex.AppendLine(@"\begin{minipage}{0.82\textwidth}{\large\bfseries\color{UASLPblue} Alumno:\par}\vspace{0.2cm}");
                    tex.AppendLine(@"\noindent\textcolor{UASLPblue}{\textbullet}\enspace {\large " + Escape(alumno) + @"}\end{minipage}\par");
                }
                tex.AppendLine(@"\vfill");
                if (lugarFecha != "") tex.AppendLine(@"{\small\color{DarkGray} " + Escape(lugarFecha) + @"\par}");
                tex.AppendLine(@"\end{titlepage}");
                if (work.Publication.Contents || work.Chapters.Count > 1)
                    tex.AppendLine(@"\tableofcontents\newpage");
            }
            else if(!glossary && isPractica){
                string Fact(string k, string def = "") => work.DocumentFacts.TryGetValue(k, out var val) && !string.IsNullOrWhiteSpace(val) ? val.Trim() : def;
                var materia = Fact("Materia y ciclo", Fact("Departamento", ""));
                var realizada = Fact("Realizada por", Fact("Alumno", work.Author));
                tex.AppendLine(@"\begin{center}");
                if (materia != "") tex.AppendLine(@"{\large " + Escape(materia) + @"\par}\vspace{0.25cm}");
                if (realizada != "")
                {
                    tex.AppendLine(@"Realizada por:\par\vspace{0.15cm}");
                    tex.AppendLine(@"{\bfseries " + Escape(realizada) + @"\par}\vspace{0.35cm}");
                }
                tex.AppendLine(@"{\LARGE\bfseries " + Escape(work.Title) + @"\par}");
                if (work.Subtitle != "") tex.AppendLine(@"\vspace{0.2cm}{\large\itshape " + Escape(work.Subtitle) + @"\par}");
                tex.AppendLine(@"\vspace{0.35cm}");
                foreach (var k in facts)
                {
                    if (k is "Materia y ciclo" or "Realizada por") continue;
                    tex.AppendLine(@"{\normalsize\bfseries " + Escape(k) + @": } " + Escape(work.DocumentFacts[k]) + @"\par\vspace{0.08cm}");
                }
                tex.AppendLine(@"\end{center}\medskip");
                if (work.CoverImage != "")
                {
                    CopyImage(work.CoverImage);
                    tex.AppendLine(@"\begin{center}\includegraphics[width=.72\linewidth,height=.26\textheight,keepaspectratio]{media/" + work.CoverImage + @"}\end{center}\medskip");
                }
                if (work.Publication.Contents && work.Chapters.Count > 1)
                    tex.AppendLine(@"\tableofcontents\bigskip");
            }
            else if(!glossary && work.Type=="CV"){
                string Contact(string key)=>work.Contact.GetValueOrDefault(key,"").Trim();
                var name=Contact("Nombre profesional");if(name=="")name=work.Title;
                var profession=Contact("Profesión");if(profession=="")profession=work.Subtitle;
                if(style=="Académica")tex.AppendLine(@"\begin{center}");
                if(style is "Moderna" or "Ejecutiva" or "BandaLateral")
                    tex.AppendLine(@"\noindent\colorbox{accent}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\color{white}\vspace{4pt}{\LARGE\bfseries\strut "+Escape(name)+@"\strut}"+(profession!=""?@"\\[2pt]{\normalsize "+Escape(profession)+"}":"")+@"\vspace{4pt}}}\par");
                else tex.AppendLine(@"\noindent{\Huge\bfseries\color{accent}"+Escape(name)+@"\par}");
                if(profession!="" && style is not ("Moderna" or "Ejecutiva" or "BandaLateral"))tex.AppendLine(@"\medskip{\large "+Escape(profession)+@"\par}");
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
                var cargo = work.DocumentFacts.GetValueOrDefault("Cargo o institución", "").Trim();
                var refCode = work.DocumentFacts.GetValueOrDefault("Referencia u oficio", "").Trim();
                if (refCode != "") tex.AppendLine(@"\noindent{\small\bfseries Referencia: }{" + Escape(refCode) + @"}\par\smallskip");
                if (recipient != "" || address != "" || cargo != "")
                {
                    tex.AppendLine(@"\noindent Dirigido a:\\");
                    if (recipient != "") tex.AppendLine(@"{\bfseries " + Escape(recipient) + @"}\\");
                    if (cargo != "") tex.AppendLine(Escape(cargo) + @"\\");
                    if (address != "") tex.AppendLine(Escape(address) + @"\\");
                    tex.AppendLine(@"{\bfseries PRESENTE.}\par\medskip");
                }
                tex.AppendLine(@"\noindent{\color{accent}\rule{\linewidth}{.8pt}}\par\medskip");
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
                tex.AppendLine(@"\maketitle");
            }
            if(!glossary && !isLibreta && !isPractica && work.Type!="Carta" && facts.Length>0){
                tex.AppendLine(@"\par\smallskip\noindent{\renewcommand{\arraystretch}{1.2}\begin{tabularx}{\linewidth}{|>{\columncolor{accent!12}\bfseries}p{4.2cm}|Y|}\hline");
                foreach(var key in facts)tex.AppendLine(Escape(key)+@" & "+Escape(work.DocumentFacts[key])+@"\\\hline");
                tex.AppendLine(@"\end{tabularx}}\par\medskip");
            }
            if (!glossary && !isLibreta && !isPractica && work.Type is not ("CV" or "Carta" or "Acta") && work.Publication.Contents && work.Chapters.Count > 1) tex.AppendLine(@"\tableofcontents\medskip");
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
                    if (isLibreta)
                    {
                        tex.AppendLine(@"\section{\color{UASLPblue}" + heading + @"}\vspace{-0.22cm}\noindent{\color{UASLPgreen}\rule{\linewidth}{1.5pt}}\par\smallskip");
                    }
                    else
                    {
                        tex.AppendLine(novel ?
                            (chapter.Level == "Arc" ? @"\part{" : chapter.Level == "Subchapter" ? @"\section{" : @"\chapter{") + heading + "}" :
                            work.Type is "Informe" or "Artículo" or "Propuesta" or "Documento" ? @"\section{\color{accent}" + heading + "}" : work.Type == "Carta" ? @"\medskip" : @"\section*{\color{accent}" + heading + @"}\vspace{-0.18cm}\noindent{\color{accent!45}\rule{\linewidth}{0.7pt}}\par\smallskip");
                    }
                    if (work.Type == "Apuntes" && chapter.Detail != "")
                        tex.AppendLine(@"\par\smallskip\noindent\colorbox{accent!10}{\parbox{\dimexpr\linewidth-2\fboxsep\relax}{\small{\bfseries\color{accent}Conceptos y preguntas}\par " + Paragraphs(chapter.Detail) + @"}}\par\smallskip");
                    else if (chapter.Detail != "")
                        tex.AppendLine(@"\par\smallskip\noindent{\small\bfseries\color{accent}" + Escape(EditorialTemplate.DetailLabel(work.Type)) + @": }\emph{" + Escape(chapter.Detail) + @"}\par\smallskip");
                    var rows = Illustrations.Rows(chapter).ToList();
                    var used = new HashSet<int>();
                    int start = 0;
                    if (rows.Count == 0 && chapter.Images.Count > 0 && Illustrations.Layout(chapter,0).StartsWith("Ajustar"))
                    {
                        Image(chapter.Images[0], Illustrations.Layout(chapter,0),chapter.ImageCaptions.GetValueOrDefault(0)); used.Add(0);
                    }
                    foreach (var row in rows)
                    {
                        tex.AppendLine(Paragraphs(chapter.PlainText[start..row[0].Start],true));
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
                    tex.AppendLine(Paragraphs(chapter.PlainText[start..],true));
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
                        else
                            tex.AppendLine(@"\par\medskip\textit{" + Escape(chapter.Summary) + "}");
                    }
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
