using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EmailSummarizer.Services
{
    public static class MarkdownEmailConverter
    {
        private static readonly Regex CodeBlockRegex = new Regex(@"```([a-zA-Z0-9_-]*)\r?\n([\s\S]*?)```", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new Regex(@"`([^`\r\n]+)`", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);
        private static readonly Regex BoldItalicRegex = new Regex(@"(\*\*\*|___)(.*?)\1", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new Regex(@"(\*\*|__)(.*?)\1", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new Regex(@"(\*|_)(.*?)\1", RegexOptions.Compiled);
        private static readonly Regex StrikethroughRegex = new Regex(@"~~(.*?)~~", RegexOptions.Compiled);
        private static readonly Regex HeaderRegex = new Regex(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new Regex(@"^(\s*)[*+-]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^(\s*)\d+\.\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex HorizontalRuleRegex = new Regex(@"^\s*([-*_]){3,}\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Converts Markdown text into clean, un-mangled plain text suitable for pure text email clients
        /// and terminal mail readers. Preserves blockquotes ('> ') for RFC thread standard compliance.
        /// </summary>
        public static string ConvertToPlainText(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

            // First, protect code blocks from inline processing
            var codeBlocks = new List<string>();
            string processed = CodeBlockRegex.Replace(markdown, m =>
            {
                string code = m.Groups[2].Value.TrimEnd();
                codeBlocks.Add(code);
                return $"%%CODEBLOCK_{codeBlocks.Count - 1}%%";
            });

            var lines = processed.Replace("\r\n", "\n").Split('\n');
            var result = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Check for placeholder code block
                var codeMatch = Regex.Match(line, @"%%CODEBLOCK_(\d+)%%");
                if (codeMatch.Success)
                {
                    int index = int.Parse(codeMatch.Groups[1].Value);
                    if (index >= 0 && index < codeBlocks.Count)
                    {
                        var codeLines = codeBlocks[index].Split('\n');
                        result.AppendLine("[Code]:");
                        foreach (var cl in codeLines)
                        {
                            result.AppendLine("    " + cl.TrimEnd('\r'));
                        }
                    }
                    continue;
                }

                // Check for horizontal rule
                if (HorizontalRuleRegex.IsMatch(line))
                {
                    result.AppendLine("--------------------------------------------------");
                    continue;
                }

                // Check for Headers
                var headerMatch = HeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    int level = headerMatch.Groups[1].Value.Length;
                    string headerText = CleanInlineMarkdown(headerMatch.Groups[2].Value.Trim());
                    if (level == 1)
                    {
                        result.AppendLine(headerText.ToUpperInvariant());
                        result.AppendLine(new string('=', Math.Max(headerText.Length, 20)));
                    }
                    else if (level == 2)
                    {
                        result.AppendLine(headerText);
                        result.AppendLine(new string('-', Math.Max(headerText.Length, 20)));
                    }
                    else
                    {
                        result.AppendLine($"=== {headerText} ===");
                    }
                    continue;
                }

                // Check for lists
                var ulMatch = UnorderedListRegex.Match(line);
                if (ulMatch.Success)
                {
                    string indent = ulMatch.Groups[1].Value;
                    string content = CleanInlineMarkdown(ulMatch.Groups[2].Value);
                    result.AppendLine($"{indent}• {content}");
                    continue;
                }

                var olMatch = OrderedListRegex.Match(line);
                if (olMatch.Success)
                {
                    result.AppendLine(CleanInlineMarkdown(line));
                    continue;
                }

                // Blockquotes: preserve '> ' prefix for RFC thread compliance
                if (line.TrimStart().StartsWith(">"))
                {
                    result.AppendLine(CleanInlineMarkdown(line));
                    continue;
                }

                // Standard paragraph line
                result.AppendLine(CleanInlineMarkdown(line));
            }

            return result.ToString().TrimEnd() + "\r\n";
        }

        private static string CleanInlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Links: [Text](Url) -> Text (Url) or just Url if text equals url
            text = LinkRegex.Replace(text, m =>
            {
                string linkText = m.Groups[1].Value;
                string url = m.Groups[2].Value;
                return string.Equals(linkText, url, StringComparison.OrdinalIgnoreCase) 
                    ? url 
                    : $"{linkText} ({url})";
            });

            // Inline code: `code` -> 'code'
            text = InlineCodeRegex.Replace(text, "'$1'");

            // Bold & Italic
            text = BoldItalicRegex.Replace(text, "$2");
            text = BoldRegex.Replace(text, "$2");
            text = ItalicRegex.Replace(text, "$2");
            text = StrikethroughRegex.Replace(text, "$1");

            return text;
        }

        /// <summary>
        /// Converts Markdown into clean, accessible, modern HTML suitable for multipart/alternative MIME emails.
        /// </summary>
        public static string ConvertToHtml(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "<p></p>";

            var codeBlocks = new List<(string lang, string code)>();
            string processed = CodeBlockRegex.Replace(markdown, m =>
            {
                codeBlocks.Add((m.Groups[1].Value, m.Groups[2].Value.TrimEnd()));
                return $"%%HTML_CODEBLOCK_{codeBlocks.Count - 1}%%";
            });

            var lines = processed.Replace("\r\n", "\n").Split('\n');
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset=\"utf-8\"></head>");
            html.AppendLine("<body style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.6; color: #202124; margin: 0; padding: 0;\">");

            bool inList = false;
            bool isOrdered = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmed = rawLine.Trim();

                // Check for placeholder code block
                var codeMatch = Regex.Match(rawLine, @"%%HTML_CODEBLOCK_(\d+)%%");
                if (codeMatch.Success)
                {
                    CloseListIfOpen(html, ref inList, ref isOrdered);
                    int index = int.Parse(codeMatch.Groups[1].Value);
                    if (index >= 0 && index < codeBlocks.Count)
                    {
                        var (_, code) = codeBlocks[index];
                        html.AppendLine($"<pre style=\"background-color: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px; padding: 12px; font-family: Consolas, 'Liberation Mono', Menlo, Courier, monospace; font-size: 13px; line-height: 1.45; overflow-x: auto; margin: 12px 0;\"><code>{WebUtility.HtmlEncode(code)}</code></pre>");
                    }
                    continue;
                }

                if (HorizontalRuleRegex.IsMatch(trimmed))
                {
                    CloseListIfOpen(html, ref inList, ref isOrdered);
                    html.AppendLine("<hr style=\"border: 0; border-top: 1px solid #e1e4e8; margin: 18px 0;\" />");
                    continue;
                }

                var headerMatch = HeaderRegex.Match(rawLine);
                if (headerMatch.Success)
                {
                    CloseListIfOpen(html, ref inList, ref isOrdered);
                    int level = Math.Clamp(headerMatch.Groups[1].Value.Length, 1, 6);
                    string text = ConvertInlineToHtml(headerMatch.Groups[2].Value.Trim());
                    string fontSize = level switch
                    {
                        1 => "22px",
                        2 => "18px",
                        3 => "16px",
                        _ => "14px"
                    };
                    html.AppendLine($"<h{level} style=\"font-size: {fontSize}; font-weight: 600; margin: 16px 0 8px 0; color: #1a1a1a;\">{text}</h{level}>");
                    continue;
                }

                // Check for lists
                var ulMatch = UnorderedListRegex.Match(rawLine);
                if (ulMatch.Success)
                {
                    if (!inList || isOrdered)
                    {
                        CloseListIfOpen(html, ref inList, ref isOrdered);
                        html.AppendLine("<ul style=\"margin: 6px 0 10px 0; padding-left: 24px;\">");
                        inList = true;
                        isOrdered = false;
                    }
                    string text = ConvertInlineToHtml(ulMatch.Groups[2].Value);
                    html.AppendLine($"<li style=\"margin: 3px 0;\">{text}</li>");
                    continue;
                }

                var olMatch = OrderedListRegex.Match(rawLine);
                if (olMatch.Success)
                {
                    if (!inList || !isOrdered)
                    {
                        CloseListIfOpen(html, ref inList, ref isOrdered);
                        html.AppendLine("<ol style=\"margin: 6px 0 10px 0; padding-left: 24px;\">");
                        inList = true;
                        isOrdered = true;
                    }
                    string text = ConvertInlineToHtml(olMatch.Groups[2].Value);
                    html.AppendLine($"<li style=\"margin: 3px 0;\">{text}</li>");
                    continue;
                }

                CloseListIfOpen(html, ref inList, ref isOrdered);

                // Blockquotes
                if (trimmed.StartsWith(">"))
                {
                    string quoteText = trimmed.TrimStart('>', ' ');
                    string text = ConvertInlineToHtml(quoteText);
                    html.AppendLine($"<blockquote style=\"margin: 8px 0; padding-left: 12px; border-left: 3px solid #0066cc; color: #555555;\">{text}</blockquote>");
                    continue;
                }

                // Blank line
                if (string.IsNullOrEmpty(trimmed))
                {
                    html.AppendLine("<div style=\"height: 8px;\"></div>");
                    continue;
                }

                // Normal Paragraph
                string paragraphText = ConvertInlineToHtml(rawLine);
                html.AppendLine($"<p style=\"margin: 4px 0 8px 0;\">{paragraphText}</p>");
            }

            CloseListIfOpen(html, ref inList, ref isOrdered);

            html.AppendLine("</body></html>");
            return html.ToString();
        }

        private static void CloseListIfOpen(StringBuilder html, ref bool inList, ref bool isOrdered)
        {
            if (inList)
            {
                html.AppendLine(isOrdered ? "</ol>" : "</ul>");
                inList = false;
            }
        }

        private static string ConvertInlineToHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // HTML encode first
            text = WebUtility.HtmlEncode(text);

            // Inline Code
            text = InlineCodeRegex.Replace(text, "<code style=\"background: #f0f2f5; padding: 2px 5px; border-radius: 3px; font-family: Consolas, monospace; font-size: 13px;\">$1</code>");

            // Links: [text](url)
            text = LinkRegex.Replace(text, "<a href=\"$2\" style=\"color: #0066cc; text-decoration: underline;\">$1</a>");

            // Bold & Italic
            text = BoldItalicRegex.Replace(text, "<strong><em>$2</em></strong>");
            text = BoldRegex.Replace(text, "<strong>$2</strong>");
            text = ItalicRegex.Replace(text, "<em>$2</em>");
            text = StrikethroughRegex.Replace(text, "<del>$1</del>");

            return text;
        }
    }
}
