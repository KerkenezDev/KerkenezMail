using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KerkenezMail.Models;

namespace KerkenezMail.Services
{
    public sealed class RenderedLinkSpan
    {
        public int HintIndex { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
    }

    public sealed class RenderedRtfResult
    {
        public string Rtf { get; init; } = string.Empty;
        public List<RenderedLinkSpan> Links { get; } = new List<RenderedLinkSpan>();
    }

    /// <summary>
    /// Converts Markdown responses (including headings, lists, blockquotes, code blocks,
    /// tables, and labeled hyperlinks) into WinForms RichTextBox-compatible RTF, plus shared
    /// link tooltip formatting and safe external URL launch helpers.
    /// </summary>
    public static class MarkdownRtfRenderer
    {
        private readonly struct InlineStyle
        {
            public bool Bold { get; init; }
            public bool Italic { get; init; }
            public bool Strike { get; init; }
            public int ColorIndex { get; init; }

            public InlineStyle WithBold() => new InlineStyle { Bold = true, Italic = Italic, Strike = Strike, ColorIndex = ColorIndex };
            public InlineStyle WithItalic() => new InlineStyle { Bold = Bold, Italic = true, Strike = Strike, ColorIndex = ColorIndex };
            public InlineStyle WithStrike() => new InlineStyle { Bold = Bold, Italic = Italic, Strike = true, ColorIndex = ColorIndex };
        }

        private sealed class RtfEmitContext
        {
            public StringBuilder Rtf { get; } = new StringBuilder();
            public List<RenderedLinkSpan> Links { get; } = new List<RenderedLinkSpan>();
            public Dictionary<string, string> KnownLabelsByUrl { get; }
            public int PlainLength { get; set; }

            public RtfEmitContext(Dictionary<string, string> knownLabelsByUrl)
            {
                KnownLabelsByUrl = knownLabelsByUrl;
            }

            public void AppendPlainRtf(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                Rtf.Append(EscapeRtf(text));
                PlainLength += text.Length;
            }

            public void AppendNewlinePar(string prefixControl = "")
            {
                if (!string.IsNullOrEmpty(prefixControl))
                {
                    Rtf.Append(prefixControl);
                }
                Rtf.AppendLine(@"\par");
                PlainLength += 1;
            }
        }

        private static readonly Regex HeaderRegex = new Regex(@"^(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new Regex(@"^(\s*)([-*+])\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^(\s*)(\d+)[.)]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex HorizontalRuleRegex = new Regex(@"^\s*([-*_])(?:\s*\1){2,}\s*$", RegexOptions.Compiled);
        private static readonly Regex FenceStartRegex = new Regex(@"^\s*(```|~~~)(.*)$", RegexOptions.Compiled);
        private static readonly Regex TableSeparatorCellRegex = new Regex(@"^:?-{2,}:?$", RegexOptions.Compiled);

        public static RenderedRtfResult ConvertToRtf(string? markdown, IEnumerable<EmailLink>? knownEmailLinks = null)
        {
            var result = new RenderedRtfResult();
            var labelMap = BuildKnownLinkLabelMap(knownEmailLinks);
            var ctx = new RtfEmitContext(labelMap);

            ctx.Rtf.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}{\f1\fnil\fcharset0 Consolas;}}");
            // Color table:
            // \cf1 = Link blue (#0066CC)
            // \cf2 = Body dark slate (#1E232D)
            // \cf3 = Muted gray (#5F6978)
            // \cf4 = Inline code crimson (#A02346)
            // \cf5 = Subtle border gray (#D2D8E0)
            // \cf6 = Code background tint (#F4F6F9)
            // \cf7 = Heading near-black (#121826)
            ctx.Rtf.AppendLine(@"{\colortbl ;\red0\green102\blue204;\red30\green35\blue45;\red95\green105\blue120;\red160\green35\blue70;\red210\green216\blue224;\red244\green246\blue249;\red18\green24\blue38;}");
            ctx.Rtf.AppendLine(@"\viewkind4\uc1");

            if (!string.IsNullOrWhiteSpace(markdown))
            {
                var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                EmitBlocks(lines, ctx);
            }
            else
            {
                ctx.Rtf.Append(@"\pard\cf2\f0\fs20\par");
            }

            ctx.Rtf.AppendLine("}");

            result = new RenderedRtfResult { Rtf = ctx.Rtf.ToString() };
            result.Links.AddRange(ctx.Links);
            return result;
        }

        private static void EmitBlocks(string[] lines, RtfEmitContext ctx)
        {
            bool lastWasBlank = true;
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // 1. Fenced code block (``` or ~~~)
                var fenceMatch = FenceStartRegex.Match(line);
                if (fenceMatch.Success)
                {
                    string fenceToken = fenceMatch.Groups[1].Value;
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length)
                    {
                        if (lines[i].TrimStart().StartsWith(fenceToken, StringComparison.Ordinal))
                        {
                            i++;
                            break;
                        }
                        codeLines.Add(lines[i]);
                        i++;
                    }

                    EmitCodeBlock(codeLines, ctx);
                    lastWasBlank = false;
                    continue;
                }

                // 2. Blank lines (collapse consecutive blank lines)
                if (string.IsNullOrEmpty(trimmed))
                {
                    if (!lastWasBlank)
                    {
                        ctx.Rtf.Append(@"\pard\sa60\fs10");
                        ctx.AppendNewlinePar();
                        lastWasBlank = true;
                    }
                    i++;
                    continue;
                }

                // 3. Horizontal rule
                if (HorizontalRuleRegex.IsMatch(trimmed))
                {
                    ctx.Rtf.Append(@"\pard\sb60\sa100\brdrb\brdrs\brdrw10\brdrcf5\fs4");
                    ctx.AppendNewlinePar();
                    lastWasBlank = false;
                    i++;
                    continue;
                }

                // 4. Pipe Table (header row + separator row)
                if (i + 1 < lines.Length &&
                    TrySplitTableRow(line, out var headerCells) &&
                    IsTableSeparatorRow(lines[i + 1], out int sepCols) &&
                    Math.Max(headerCells.Count, sepCols) >= 2)
                {
                    var bodyRows = new List<List<string>>();
                    i += 2;
                    while (i < lines.Length && TrySplitTableRow(lines[i], out var rowCells))
                    {
                        bodyRows.Add(rowCells);
                        i++;
                    }

                    EmitTable(headerCells, bodyRows, ctx);
                    lastWasBlank = false;
                    continue;
                }

                // 5. Headings (# .. ######)
                var headerMatch = HeaderRegex.Match(trimmed);
                if (headerMatch.Success)
                {
                    int level = Math.Clamp(headerMatch.Groups[1].Value.Length, 1, 6);
                    int fs = level switch
                    {
                        1 => 28,
                        2 => 24,
                        3 => 22,
                        _ => 20
                    };
                    int sb = lastWasBlank ? 60 : 140;
                    ctx.Rtf.Append($@"\pard\sb{sb}\sa80\f0\fs{fs}\cf7\b ");
                    EmitInline(headerMatch.Groups[2].Value.Trim(), new InlineStyle { Bold = true, ColorIndex = 7 }, ctx);
                    ctx.Rtf.Append(@"\b0\cf2\fs20");
                    ctx.AppendNewlinePar();
                    lastWasBlank = false;
                    i++;
                    continue;
                }

                // 6. Blockquote (> ...)
                if (trimmed.StartsWith(">"))
                {
                    string quoteText = trimmed.TrimStart('>').TrimStart();
                    ctx.Rtf.Append(@"\pard\li280\sa70\sl260\slmult1\f0\fs20 ");
                    ctx.Rtf.Append(@"{\cf1\b ");
                    ctx.AppendPlainRtf("│ ");
                    ctx.Rtf.Append(@"}");
                    ctx.Rtf.Append(@"{\cf3\i ");
                    EmitInline(quoteText, new InlineStyle { Italic = true, ColorIndex = 3 }, ctx);
                    ctx.Rtf.Append(@"}");
                    ctx.AppendNewlinePar();
                    lastWasBlank = false;
                    i++;
                    continue;
                }

                // 7. Unordered list
                var ulMatch = UnorderedListRegex.Match(line);
                if (ulMatch.Success)
                {
                    int indentSpaces = CountLeadingIndentSpaces(ulMatch.Groups[1].Value);
                    int level = Math.Clamp(indentSpaces / 2, 0, 4);
                    int li = 320 + (level * 240);
                    string bullet = level switch
                    {
                        0 => "• ",
                        1 => "◦ ",
                        _ => "▪ "
                    };

                    ctx.Rtf.Append($@"\pard\li{li}\fi-180\sa60\sl260\slmult1\f0\fs20\cf2 ");
                    ctx.AppendPlainRtf(bullet);
                    EmitInline(ulMatch.Groups[3].Value, new InlineStyle { ColorIndex = 2 }, ctx);
                    ctx.AppendNewlinePar();
                    lastWasBlank = false;
                    i++;
                    continue;
                }

                // 8. Ordered list
                var olMatch = OrderedListRegex.Match(line);
                if (olMatch.Success)
                {
                    int indentSpaces = CountLeadingIndentSpaces(olMatch.Groups[1].Value);
                    int level = Math.Clamp(indentSpaces / 2, 0, 4);
                    int li = 360 + (level * 240);
                    string numberPrefix = olMatch.Groups[2].Value + ". ";

                    ctx.Rtf.Append($@"\pard\li{li}\fi-240\sa60\sl260\slmult1\f0\fs20\cf2 ");
                    ctx.Rtf.Append(@"{\b ");
                    ctx.AppendPlainRtf(numberPrefix);
                    ctx.Rtf.Append(@"}");
                    EmitInline(olMatch.Groups[3].Value, new InlineStyle { ColorIndex = 2 }, ctx);
                    ctx.AppendNewlinePar();
                    lastWasBlank = false;
                    i++;
                    continue;
                }

                // 9. Standard paragraph line
                ctx.Rtf.Append(@"\pard\sa90\sl260\slmult1\f0\fs20\cf2 ");
                EmitInline(line, new InlineStyle { ColorIndex = 2 }, ctx);
                ctx.AppendNewlinePar();
                lastWasBlank = false;
                i++;
            }
        }

        private static void EmitCodeBlock(List<string> codeLines, RtfEmitContext ctx)
        {
            if (codeLines.Count == 0)
            {
                ctx.Rtf.Append(@"\pard\li220\sa80\f1\fs18\cf4\highlight6 ");
                ctx.AppendNewlinePar(@"\highlight0\f0\fs20\cf2");
                return;
            }

            for (int idx = 0; idx < codeLines.Count; idx++)
            {
                string cl = codeLines[idx].TrimEnd('\r');
                int sb = idx == 0 ? 60 : 0;
                int sa = idx == codeLines.Count - 1 ? 100 : 20;
                ctx.Rtf.Append($@"\pard\li220\sb{sb}\sa{sa}\f1\fs18\cf2\highlight6 ");
                ctx.AppendPlainRtf(string.IsNullOrEmpty(cl) ? " " : cl);
                ctx.Rtf.Append(@"\highlight0\f0\fs20");
                ctx.AppendNewlinePar();
            }
        }

        private static void EmitTable(List<string> headers, List<List<string>> rows, RtfEmitContext ctx)
        {
            int colCount = Math.Max(headers.Count, rows.Count > 0 ? rows.Max(r => r.Count) : 0);
            if (colCount < 2) return;

            int cellWidth = Math.Max(1400, 8400 / colCount);

            void EmitRow(List<string> cells, bool isHeader)
            {
                ctx.Rtf.Append(@"\trowd\trgaph90\trleft0");
                for (int c = 0; c < colCount; c++)
                {
                    int rightEdge = (c + 1) * cellWidth;
                    ctx.Rtf.Append(@"\clbrdrt\brdrs\brdrw10\brdrcf5\clbrdrl\brdrs\brdrw10\brdrcf5\clbrdrb\brdrs\brdrw10\brdrcf5\clbrdrr\brdrs\brdrw10\brdrcf5");
                    if (isHeader)
                    {
                        ctx.Rtf.Append(@"\clcbpat6");
                    }
                    ctx.Rtf.Append($@"\cellx{rightEdge}");
                }
                ctx.Rtf.AppendLine();

                for (int c = 0; c < colCount; c++)
                {
                    string cellText = c < cells.Count ? cells[c] : string.Empty;
                    if (isHeader)
                    {
                        ctx.Rtf.Append(@"\pard\intbl\sb40\sa40\f0\fs19\cf7\b ");
                        EmitInline(cellText, new InlineStyle { Bold = true, ColorIndex = 7 }, ctx);
                        ctx.Rtf.Append(@"\b0\cell ");
                    }
                    else
                    {
                        ctx.Rtf.Append(@"\pard\intbl\sb40\sa40\f0\fs19\cf2 ");
                        EmitInline(cellText, new InlineStyle { ColorIndex = 2 }, ctx);
                        ctx.Rtf.Append(@"\cell ");
                    }
                    ctx.PlainLength += 1; // cell separator in RichEdit text
                }
                ctx.Rtf.AppendLine(@"\row");
                ctx.PlainLength += 1;
            }

            EmitRow(headers, isHeader: true);
            foreach (var row in rows)
            {
                EmitRow(row, isHeader: false);
            }

            ctx.Rtf.Append(@"\pard\sa80\f0\fs20\cf2");
            ctx.AppendNewlinePar();
        }

        private static bool TrySplitTableRow(string line, out List<string> cells)
        {
            cells = new List<string>();
            string trimmed = line.Trim();
            if (!trimmed.Contains('|')) return false;

            // Require pipe formatting like "| a | b |" or "a | b"
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|") && !trimmed.EndsWith(@"\|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

            var parts = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                if (ch == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                    continue;
                }
                if (ch == '|')
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
                current.Append(ch);
            }
            parts.Add(current.ToString().Trim());

            if (parts.Count < 2) return false;
            cells = parts;
            return true;
        }

        private static bool IsTableSeparatorRow(string line, out int colCount)
        {
            colCount = 0;
            if (!TrySplitTableRow(line, out var cells)) return false;
            foreach (var cell in cells)
            {
                string c = cell.Replace(" ", "");
                if (!TableSeparatorCellRegex.IsMatch(c)) return false;
            }
            colCount = cells.Count;
            return colCount >= 2;
        }

        private static int CountLeadingIndentSpaces(string whitespace)
        {
            int count = 0;
            foreach (char ch in whitespace)
            {
                count += (ch == '\t') ? 4 : 1;
            }
            return count;
        }

        private static void EmitInline(string text, InlineStyle style, RtfEmitContext ctx)
        {
            if (string.IsNullOrEmpty(text)) return;

            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                char ch = text[i];

                // 1. Backslash escape for punctuation
                if (ch == '\\' && i + 1 < len && IsAsciiPunctuation(text[i + 1]))
                {
                    ctx.AppendPlainRtf(text[i + 1].ToString());
                    i += 2;
                    continue;
                }

                // 2. Inline code (`...`)
                if (ch == '`')
                {
                    int close = text.IndexOf('`', i + 1);
                    if (close > i)
                    {
                        string code = text.Substring(i + 1, close - i - 1);
                        ctx.Rtf.Append(@"{\f1\fs18\cf4\highlight6 ");
                        ctx.AppendPlainRtf(code);
                        ctx.Rtf.Append(@"\highlight0}");
                        i = close + 1;
                        continue;
                    }
                }

                // 3. Markdown image/link: ![label](url) or [label](url)
                if ((ch == '!' && i + 1 < len && text[i + 1] == '[') || ch == '[')
                {
                    int bracketStart = ch == '!' ? i + 1 : i;
                    if (TryParseMarkdownLink(text, bracketStart, out string label, out string url, out int nextIndex))
                    {
                        EmitHyperlink(label, url, style, ctx);
                        i = nextIndex;
                        continue;
                    }
                }

                // 4. Angle-bracket autolink: <https://...> or <mailto:...>
                if (ch == '<' && TryParseAngleAutolink(text, i, out string autoUrl, out int afterAngle))
                {
                    string displayLabel = ResolvePreferredLabel(null, autoUrl, ctx.KnownLabelsByUrl);
                    EmitHyperlink(displayLabel, autoUrl, style, ctx);
                    i = afterAngle;
                    continue;
                }

                // 5. Bare URL: https://... or http://...
                if ((ch == 'h' || ch == 'H') &&
                    (text.AsSpan(i).StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                     text.AsSpan(i).StartsWith("http://", StringComparison.OrdinalIgnoreCase)) &&
                    TryParseBareUrl(text, i, out string bareUrl, out int afterBare))
                {
                    string displayLabel = ResolvePreferredLabel(null, bareUrl, ctx.KnownLabelsByUrl);
                    EmitHyperlink(displayLabel, bareUrl, style, ctx);
                    i = afterBare;
                    continue;
                }

                // 6. Strikethrough: ~~...~~
                if (ch == '~' && i + 1 < len && text[i + 1] == '~')
                {
                    int close = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        string inner = text.Substring(i + 2, close - (i + 2));
                        ctx.Rtf.Append(@"{\strike ");
                        EmitInline(inner, style.WithStrike(), ctx);
                        ctx.Rtf.Append(@"}");
                        i = close + 2;
                        continue;
                    }
                }

                // 7. Bold + Italic (***...*** or ___...___)
                if ((ch == '*' || ch == '_') && i + 2 < len && text[i + 1] == ch && text[i + 2] == ch)
                {
                    string triple = new string(ch, 3);
                    if (TryFindEmphasisClose(text, i + 3, triple, ch == '_', out int close))
                    {
                        string inner = text.Substring(i + 3, close - (i + 3));
                        ctx.Rtf.Append(@"{\b\i ");
                        EmitInline(inner, style.WithBold().WithItalic(), ctx);
                        ctx.Rtf.Append(@"}");
                        i = close + 3;
                        continue;
                    }
                }

                // 8. Bold (**...** or __...__)
                if ((ch == '*' || ch == '_') && i + 1 < len && text[i + 1] == ch)
                {
                    string dbl = new string(ch, 2);
                    if (TryFindEmphasisClose(text, i + 2, dbl, ch == '_', out int close))
                    {
                        string inner = text.Substring(i + 2, close - (i + 2));
                        ctx.Rtf.Append(@"{\b ");
                        EmitInline(inner, style.WithBold(), ctx);
                        ctx.Rtf.Append(@"}");
                        i = close + 2;
                        continue;
                    }
                }

                // 9. Italic (*...* or _..._)
                if (ch == '*' || ch == '_')
                {
                    bool canOpen = !IsWordBoundaryBlockedBefore(text, i, ch == '_') &&
                                   i + 1 < len &&
                                   !char.IsWhiteSpace(text[i + 1]);
                    if (canOpen && TryFindEmphasisClose(text, i + 1, ch.ToString(), ch == '_', out int close))
                    {
                        string inner = text.Substring(i + 1, close - (i + 1));
                        ctx.Rtf.Append(@"{\i ");
                        EmitInline(inner, style.WithItalic(), ctx);
                        ctx.Rtf.Append(@"}");
                        i = close + 1;
                        continue;
                    }
                }

                // 10. Regular plain character
                ctx.AppendPlainRtf(ch.ToString());
                i++;
            }
        }

        private static bool IsAsciiPunctuation(char c) =>
            c is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '-' or '.' or '!' or '|' or '~';

        private static bool IsWordBoundaryBlockedBefore(string text, int index, bool isUnderscore)
        {
            if (!isUnderscore || index <= 0) return false;
            return char.IsLetterOrDigit(text[index - 1]);
        }

        private static bool TryFindEmphasisClose(string text, int searchStart, string token, bool isUnderscore, out int closeIndex)
        {
            closeIndex = -1;
            if (searchStart >= text.Length) return false;
            if (char.IsWhiteSpace(text[searchStart])) return false;

            int pos = searchStart;
            while (pos <= text.Length - token.Length)
            {
                int found = text.IndexOf(token, pos, StringComparison.Ordinal);
                if (found < 0) return false;

                if (found > searchStart && text[found - 1] == '\\')
                {
                    pos = found + 1;
                    continue;
                }

                // Skip over inline code spans or markdown link targets inside emphasis
                if (char.IsWhiteSpace(text[found - 1]))
                {
                    pos = found + 1;
                    continue;
                }

                if (isUnderscore)
                {
                    int after = found + token.Length;
                    if (after < text.Length && char.IsLetterOrDigit(text[after]))
                    {
                        pos = found + 1;
                        continue;
                    }
                }

                // Avoid matching ** as closing * when scanning single *
                if (token.Length == 1 && found + 1 < text.Length && text[found + 1] == token[0])
                {
                    pos = found + 2;
                    continue;
                }

                closeIndex = found;
                return true;
            }

            return false;
        }

        private static bool TryParseMarkdownLink(string text, int bracketStart, out string label, out string url, out int nextIndex)
        {
            label = string.Empty;
            url = string.Empty;
            nextIndex = bracketStart;

            if (bracketStart < 0 || bracketStart >= text.Length || text[bracketStart] != '[')
            {
                return false;
            }

            // Find matching closing ']', accounting for nested brackets and escapes
            int depth = 0;
            int closeBracket = -1;
            for (int i = bracketStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeBracket = i;
                        break;
                    }
                }
            }

            if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
            {
                return false;
            }

            int pos = closeBracket + 2;
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos >= text.Length) return false;

            string rawUrl;
            int closeParen = -1;

            if (text[pos] == '<')
            {
                int gt = text.IndexOf('>', pos + 1);
                if (gt < 0) return false;
                rawUrl = text.Substring(pos + 1, gt - pos - 1).Trim();
                int p = gt + 1;
                while (p < text.Length && text[p] != ')') p++;
                if (p >= text.Length) return false;
                closeParen = p;
            }
            else
            {
                int parenDepth = 1;
                int urlEnd = -1;
                for (int i = pos; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        continue;
                    }
                    if (c == '(')
                    {
                        parenDepth++;
                    }
                    else if (c == ')')
                    {
                        parenDepth--;
                        if (parenDepth == 0)
                        {
                            urlEnd = i;
                            closeParen = i;
                            break;
                        }
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        // Optional title after URL: [label](https://... "title")
                        urlEnd = i;
                        int p = i + 1;
                        while (p < text.Length && text[p] != ')') p++;
                        if (p >= text.Length) return false;
                        closeParen = p;
                        break;
                    }
                }

                if (urlEnd <= pos || closeParen < 0) return false;
                rawUrl = text.Substring(pos, urlEnd - pos).Trim();
            }

            if (!TryNormalizeWebOrMailtoUrl(rawUrl, out string normalizedUrl))
            {
                return false;
            }

            label = text.Substring(bracketStart + 1, closeBracket - bracketStart - 1).Trim();
            url = normalizedUrl;
            nextIndex = closeParen + 1;
            return true;
        }

        private static bool TryParseAngleAutolink(string text, int start, out string url, out int nextIndex)
        {
            url = string.Empty;
            nextIndex = start;
            if (start < 0 || start >= text.Length || text[start] != '<') return false;

            int close = text.IndexOf('>', start + 1);
            if (close < 0) return false;

            string inner = text.Substring(start + 1, close - start - 1).Trim();
            if (inner.Contains(' ') || inner.Contains('\t')) return false;

            if (!TryNormalizeWebOrMailtoUrl(inner, out string normalized)) return false;

            url = normalized;
            nextIndex = close + 1;
            return true;
        }

        private static bool TryParseBareUrl(string text, int start, out string url, out int nextIndex)
        {
            url = string.Empty;
            nextIndex = start;

            int end = start;
            while (end < text.Length)
            {
                char c = text[end];
                if (char.IsWhiteSpace(c) || c is '<' or '>' or '"' or '\'' or '{' or '}' or '|' or '^' or '[' or ']' or '`')
                {
                    break;
                }
                end++;
            }

            if (end <= start) return false;

            string candidate = text.Substring(start, end - start);
            // Strip trailing sentence punctuation while preserving balanced parentheses in URLs
            while (candidate.Length > 0)
            {
                char last = candidate[candidate.Length - 1];
                if (last is '.' or ',' or ';' or ':' or '!' or '?')
                {
                    candidate = candidate.Substring(0, candidate.Length - 1);
                    end--;
                    continue;
                }
                if (last == ')')
                {
                    int opens = candidate.Count(c => c == '(');
                    int closes = candidate.Count(c => c == ')');
                    if (closes > opens)
                    {
                        candidate = candidate.Substring(0, candidate.Length - 1);
                        end--;
                        continue;
                    }
                }
                break;
            }

            if (!TryNormalizeWebOrMailtoUrl(candidate, out string normalized)) return false;

            url = normalized;
            nextIndex = end;
            return true;
        }

        private static void EmitHyperlink(string? rawLabel, string url, InlineStyle outerStyle, RtfEmitContext ctx)
        {
            string displayLabel = ResolvePreferredLabel(rawLabel, url, ctx.KnownLabelsByUrl);
            string plainLabel = StripSimpleMarkdownTokens(displayLabel);
            if (string.IsNullOrWhiteSpace(plainLabel))
            {
                plainLabel = url;
            }

            int hintIndex = ctx.PlainLength;

            string stylePrefix = @"\cf1\ul";
            if (outerStyle.Bold) stylePrefix += @"\b";
            if (outerStyle.Italic) stylePrefix += @"\i";
            if (outerStyle.Strike) stylePrefix += @"\strike";

            string rtfHref = EscapeRtfUrl(url);
            string rtfLabel = EscapeRtf(plainLabel);

            ctx.Rtf.Append($@"{{\field{{\*\fldinst{{HYPERLINK ""{rtfHref}""}}}}{{\fldrslt{{{stylePrefix} {rtfLabel}}}}}}}");
            ctx.PlainLength += plainLabel.Length;
            ctx.Links.Add(new RenderedLinkSpan
            {
                HintIndex = hintIndex,
                Text = plainLabel,
                Url = url
            });
        }

        private static string ResolvePreferredLabel(string? rawLabel, string url, Dictionary<string, string> knownLabelsByUrl)
        {
            string trimmed = (rawLabel ?? string.Empty).Trim();
            if (!IsRawOrGenericLinkLabel(trimmed, url))
            {
                return trimmed;
            }

            if (knownLabelsByUrl.TryGetValue(url, out var known) && !IsRawOrGenericLinkLabel(known, url))
            {
                return known;
            }

            string trimmedSlash = url.TrimEnd('/');
            if (knownLabelsByUrl.TryGetValue(trimmedSlash, out var knownNoSlash) && !IsRawOrGenericLinkLabel(knownNoSlash, url))
            {
                return knownNoSlash;
            }

            return string.IsNullOrWhiteSpace(trimmed) ? url : trimmed;
        }

        private static bool IsRawOrGenericLinkLabel(string label, string url)
        {
            if (string.IsNullOrWhiteSpace(label)) return true;
            if (string.Equals(label, url, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(label.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(label, "[Remote Content]", StringComparison.OrdinalIgnoreCase)) return true;
            if (label.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static Dictionary<string, string> BuildKnownLinkLabelMap(IEnumerable<EmailLink>? knownEmailLinks)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (knownEmailLinks == null) return map;

            foreach (var link in knownEmailLinks)
            {
                if (string.IsNullOrWhiteSpace(link?.Url)) continue;
                if (!TryNormalizeWebOrMailtoUrl(link.Url, out string normalizedUrl)) continue;

                string cleanLabel = Regex.Replace((link.Text ?? string.Empty).Trim(), @"\s+", " ");
                if (IsRawOrGenericLinkLabel(cleanLabel, normalizedUrl)) continue;

                if (!map.ContainsKey(normalizedUrl))
                {
                    map[normalizedUrl] = cleanLabel;
                }
                string noSlash = normalizedUrl.TrimEnd('/');
                if (!map.ContainsKey(noSlash))
                {
                    map[noSlash] = cleanLabel;
                }
            }

            return map;
        }

        private static string StripSimpleMarkdownTokens(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;
            string s = label.Trim();
            s = Regex.Replace(s, @"(\*\*\*|\*\*|\*|__|~~)(.+?)\1", "$2");
            s = Regex.Replace(s, @"`([^`]+)`", "$1");
            s = Regex.Replace(s, @"\\([\\`*_[\]()#+\-.!|~])", "$1");
            return s.Trim();
        }

        public static bool TryNormalizeWebOrMailtoUrl(string? rawUrl, out string normalizedUrl)
        {
            normalizedUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(rawUrl)) return false;

            string candidate = rawUrl.Trim();
            if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                candidate = "https://" + candidate;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
            {
                normalizedUrl = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the target URL from RichEdit LinkClickedEventArgs.LinkText, handling
        /// friendly hyperlinks ("Label#https://...") where URLs may also contain '#fragment'.
        /// </summary>
        public static string ExtractUrlFromRichTextLinkText(string? linkText)
        {
            if (string.IsNullOrWhiteSpace(linkText)) return string.Empty;
            string trimmed = linkText.Trim();

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            string[] prefixes = { "#https://", "#http://", "#mailto:", "#www." };
            foreach (string prefix in prefixes)
            {
                int idx = trimmed.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return trimmed.Substring(idx + 1).Trim();
                }
            }

            return trimmed;
        }

        public static bool TryOpenExternalUrl(string? rawUrl, out string? errorMessage)
        {
            errorMessage = null;
            if (!TryNormalizeWebOrMailtoUrl(rawUrl, out string normalized) ||
                !Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string FormatUrlForToolTip(string url, int screenWidth)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            // Dynamically calculate max characters per line proportional to screen width (45 to 80 chars)
            int maxLineLength = Math.Clamp((int)(screenWidth * 0.042), 45, 80);
            if (url.Length <= maxLineLength)
            {
                return "🔗 " + url;
            }

            var lines = new List<string>();
            string remaining = url;

            while (remaining.Length > maxLineLength)
            {
                int minSplit = Math.Max(18, maxLineLength - 25);
                int breakIdx = -1;

                char[] delimiters = new[] { '&', '?', '/', '#', '=', ';' };
                foreach (char d in delimiters)
                {
                    int idx = remaining.LastIndexOf(d, maxLineLength, maxLineLength - minSplit);
                    if (idx > minSplit)
                    {
                        if (d == '&' || d == '?')
                        {
                            breakIdx = idx; // Break before '&' or '?' so it starts next line
                        }
                        else
                        {
                            breakIdx = idx + 1; // Include '/' or '=' in current line
                        }
                        break;
                    }
                }

                if (breakIdx <= 0)
                {
                    breakIdx = maxLineLength;
                }

                string part = remaining.Substring(0, breakIdx).TrimEnd();
                lines.Add(lines.Count == 0 ? "🔗 " + part : "   " + part);
                remaining = remaining.Substring(breakIdx).TrimStart();
            }

            if (remaining.Length > 0)
            {
                lines.Add(lines.Count == 0 ? "🔗 " + remaining : "   " + remaining);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string EscapeRtf(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length * 2);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '{': sb.Append(@"\{"); break;
                    case '}': sb.Append(@"\}"); break;
                    case '\r': break;
                    case '\n': sb.Append(@"\par" + Environment.NewLine); break;
                    case '\t': sb.Append(@"\tab "); break;
                    default:
                        if (c > 127)
                        {
                            sb.Append(@"\u").Append((short)c).Append('?');
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string EscapeRtfUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            return url.Replace(@"\", @"\\")
                      .Replace(@"""", @"\""")
                      .Replace("{", @"\{")
                      .Replace("}", @"\}");
        }
    }
}
